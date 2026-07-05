using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using MarketProHunter.BrandIntelligence;
using MarketProHunter.Deduplication;
using MarketProHunter.Export;
using MarketProHunter.Filters;
using MarketProHunter.Models;
using MarketProHunter.Profit;
using MarketProHunter.Scoring;

namespace MarketProHunter.Amazon;

public sealed class AmazonSearchService
{
    public async Task<SearchRunResult> RunAsync(
        string keyword,
        int maxPages,
        SearchSettings settings,
        IProgress<string>? logProgress = null,
        IProgress<ProductResult>? acceptedProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await RunManyAsync(new[] { keyword }, maxPages, settings, ProfitSettings.Default, logProgress, acceptedProgress, cancellationToken);
    }

    public async Task<SearchRunResult> RunManyAsync(
        IEnumerable<string> keywords,
        int maxPages,
        SearchSettings settings,
        IProgress<string>? logProgress = null,
        IProgress<ProductResult>? acceptedProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await RunManyAsync(keywords, maxPages, settings, ProfitSettings.Default, logProgress, acceptedProgress, cancellationToken);
    }

    public async Task<SearchRunResult> RunManyAsync(
        IEnumerable<string> keywords,
        int maxPages,
        SearchSettings settings,
        ProfitSettings profitSettings,
        IProgress<string>? logProgress = null,
        IProgress<ProductResult>? acceptedProgress = null,
        CancellationToken cancellationToken = default)
    {
        var keywordList = keywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (keywordList.Count == 0) throw new ArgumentException("En az bir arama kelimesi gerekli.", nameof(keywords));
        if (maxPages < 1) maxPages = 1;

        var veroFilter = new VeroBrandFilter("config/vero-brands.txt");
        var brandEngine = new BrandIntelligenceEngine();
        var productFilter = new ProductFilter(settings, veroFilter);
        var scoringEngine = new ScoringEngine();
        var listingQualityAnalyzer = new ListingQualityAnalyzer();
        var productPageParser = new AmazonProductPageParser();
        var productPageQualityAnalyzer = new ProductPageQualityAnalyzer();
        var opportunityAnalyzer = new OpportunityAnalyzer();
        var smartQueueEngine = new SmartQueueEngine();
        var profitEngine = new EbayProfitEngine();
        var exporter = new CsvExporter();
        var excelExporter = new ExcelExporter();
        var accepted = new ConcurrentBag<ProductResult>();
        var acceptedAsins = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var scannedCount = 0;
        var acceptedCount = 0;
        var skippedCount = 0;
        var failedPageCount = 0;
        var failedProductPageCount = 0;
        var maxParallel = Math.Clamp(settings.MaxParallelSearches, 1, 8);

        logProgress?.Report($"Paralel tarama başlıyor. Anahtar kelime: {keywordList.Count}, paralel görev: {maxParallel}");
        logProgress?.Report($"Smart Queue hedefi: {SmartQueueEngine.DefaultQueueSize} ürün");
        logProgress?.Report($"Kâr ayarları: eBay %{profitSettings.EbayFinalValueFeePercent}, Promoted %{profitSettings.PromotedPercent}, hedef %{profitSettings.TargetProfitPercent}, min ${profitSettings.MinimumNetProfit}");

        await Parallel.ForEachAsync(keywordList, new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = cancellationToken }, async (keyword, token) =>
        {
            using var client = new AmazonSearchClient(settings);
            var parser = new AmazonSearchParser();
            logProgress?.Report($"Arama başlıyor: {keyword}");

            for (var page = 1; page <= maxPages; page++)
            {
                token.ThrowIfCancellationRequested();
                logProgress?.Report($"{keyword} | Sayfa: {page}/{maxPages}");

                var html = await FetchPageWithRetryAsync(client, keyword, page, logProgress, token);
                if (string.IsNullOrWhiteSpace(html))
                {
                    Interlocked.Increment(ref failedPageCount);
                    logProgress?.Report($"WARN {keyword} | Sayfa {page}: alınamadı, sonraki sayfaya geçiliyor.");
                    continue;
                }

                var products = parser.Parse(html, keyword, page);
                if (products.Count == 0)
                {
                    if (LooksLikeBlockedPage(html))
                    {
                        Interlocked.Increment(ref failedPageCount);
                        logProgress?.Report($"WARN {keyword} | Sayfa {page}: Amazon koruma/CAPTCHA sayfası olabilir, atlandı.");
                    }
                    else
                    {
                        logProgress?.Report($"WARN {keyword} | Sayfa {page}: ürün kartı bulunamadı, atlandı.");
                    }

                    continue;
                }

                logProgress?.Report($"{keyword} | Sayfa {page}: {products.Count} ürün kartı bulundu.");

                foreach (var product in products)
                {
                    token.ThrowIfCancellationRequested();
                    var currentScanned = Interlocked.Increment(ref scannedCount);
                    var decision = productFilter.Evaluate(product);

                    if (decision.Accepted && acceptedAsins.TryAdd(product.Asin, 0))
                    {
                        var brandProfile = brandEngine.Analyze(product.Brand);
                        var score = scoringEngine.Score(product);
                        var quality = listingQualityAnalyzer.Analyze(product);
                        var pageData = await FetchAndParseProductPageAsync(client, productPageParser, product, logProgress, token);
                        if (pageData.BulletPointCount == 0 && pageData.SpecificationCount == 0 && string.IsNullOrWhiteSpace(pageData.Description))
                        {
                            Interlocked.Increment(ref failedProductPageCount);
                        }

                        var pageQuality = productPageQualityAnalyzer.Analyze(pageData);
                        var profit = profitEngine.Calculate(product, profitSettings);
                        var enrichedProduct = product with
                        {
                            TitleQualityScore = quality.TitleQualityScore,
                            ImageQualityScore = quality.ImageQualityScore,
                            ContentQualityScore = quality.ContentQualityScore,
                            BulletPointCount = pageData.BulletPointCount,
                            SpecificationCount = pageData.SpecificationCount,
                            BulletPointQualityScore = pageQuality.BulletPointQualityScore,
                            DescriptionQualityScore = pageQuality.DescriptionQualityScore,
                            SpecificationQualityScore = pageQuality.SpecificationQualityScore,
                            HasAPlusContent = pageData.HasAPlusContent,
                            ProductPageQualityNotes = pageQuality.Notes,
                            ListingQualityNotes = quality.Notes,
                            BrandScore = 100 - brandProfile.RiskScore,
                            BrandLevel = brandProfile.RiskLevel,
                            BrandAction = brandProfile.Recommendation,
                            BrandProfileNotes = brandProfile.Notes,
                            SafetyScore = score.SafetyScore,
                            SalesScore = score.SalesScore,
                            ProfitScore = score.ProfitScore,
                            OverallScore = score.OverallScore,
                            ConfidenceScore = score.ConfidenceScore,
                            CompetitionScore = score.CompetitionScore,
                            UploadScore = score.UploadScore,
                            UploadDecision = score.UploadDecision,
                            Recommendation = score.Recommendation,
                            Stars = score.Stars,
                            RecommendedSalePrice = profit.RecommendedSalePrice,
                            EbayFee = profit.EbayFee,
                            PromotedFee = profit.PromotedFee,
                            NetProfit = profit.NetProfit,
                            NetMarginPercent = profit.NetMarginPercent,
                            ProfitDecision = profit.ProfitDecision,
                            Notes = decision.Reason
                        };
                        var opportunity = opportunityAnalyzer.Analyze(enrichedProduct);
                        var acceptedProduct = enrichedProduct with
                        {
                            RiskLevel = opportunity.RiskLevel,
                            SweetSpot = opportunity.SweetSpot,
                            OpportunitySummary = opportunity.Summary
                        };

                        accepted.Add(acceptedProduct);
                        Interlocked.Increment(ref acceptedCount);
                        acceptedProgress?.Report(acceptedProduct);
                        logProgress?.Report($"OK  {product.Asin} | {score.UploadDecision} | Upload {score.UploadScore} | Brand {acceptedProduct.BrandScore} {acceptedProduct.BrandLevel} | Title {quality.TitleQualityScore} | Img {quality.ImageQualityScore} | Bullets {pageData.BulletPointCount} | Specs {pageData.SpecificationCount} | Net ${profit.NetProfit}");
                    }
                    else
                    {
                        Interlocked.Increment(ref skippedCount);
                        var reason = decision.Accepted ? "Tekrar eden ASIN" : decision.Reason;
                        logProgress?.Report($"SKIP {product.Asin} | {reason}");
                    }

                    if (currentScanned % 25 == 0)
                    {
                        logProgress?.Report($"İlerleme: {currentScanned} tarandı | {Volatile.Read(ref acceptedCount)} kabul | {Volatile.Read(ref skippedCount)} elendi | Kabul oranı: {CalculateAcceptanceRate(Volatile.Read(ref acceptedCount), currentScanned):0.00}%");
                    }
                }

                await Task.Delay(settings.DelayBetweenPagesMs, token);
            }
        });

        var orderedAccepted = OrderForOutput(accepted).ToList();
        var duplicateSafeAccepted = DuplicateKeyBuilder.KeepBestCandidates(orderedAccepted).ToList();
        var duplicateRemovedCount = orderedAccepted.Count - duplicateSafeAccepted.Count;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(AppContext.BaseDirectory, "output", $"amazon_results_{timestamp}.csv");
        var smartQueuePath = Path.Combine(AppContext.BaseDirectory, "output", $"smart_queue_top200_{timestamp}.csv");
        var excelPath = Path.Combine(AppContext.BaseDirectory, "output", $"marketprohunter_report_{timestamp}.xlsx");
        var summaryPath = Path.Combine(AppContext.BaseDirectory, "output", $"run_summary_{timestamp}.txt");
        await exporter.WriteAsync(outputPath, orderedAccepted, cancellationToken);

        var smartQueue = smartQueueEngine.Build(duplicateSafeAccepted, SmartQueueEngine.DefaultQueueSize);
        await exporter.WriteSmartQueueAsync(smartQueuePath, smartQueue, cancellationToken);
        await excelExporter.WriteWorkbookAsync(excelPath, orderedAccepted, smartQueue, cancellationToken);
        await WriteSummaryAsync(summaryPath, keywordList, maxPages, scannedCount, skippedCount, failedPageCount, failedProductPageCount, orderedAccepted, smartQueue, duplicateRemovedCount, cancellationToken);
        logProgress?.Report($"Duplicate Killer: {duplicateRemovedCount} varyasyon/tekrar Smart Queue dışına alındı.");
        logProgress?.Report($"Smart Queue hazır: {smartQueue.SelectedCount}/{smartQueue.RequestedCount} ürün | Beklenen net kâr: ${smartQueue.ExpectedNetProfit} | Ortalama Upload: {smartQueue.AverageUploadScore} | Ortalama Confidence: {smartQueue.AverageConfidenceScore}% | Ortalama Quality: {smartQueue.AverageListingQualityScore}");
        logProgress?.Report($"Kabul oranı: {CalculateAcceptanceRate(orderedAccepted.Count, scannedCount):0.00}%");
        logProgress?.Report($"Başarısız sayfa: {failedPageCount}");
        logProgress?.Report($"Detay sayfası zayıf/alınamadı: {failedProductPageCount}");
        logProgress?.Report($"Smart Queue CSV: {smartQueuePath}");
        logProgress?.Report($"Excel raporu: {excelPath}");
        logProgress?.Report($"Özet rapor: {summaryPath}");

        return new SearchRunResult(
            scannedCount,
            orderedAccepted.Count,
            skippedCount,
            outputPath,
            smartQueuePath,
            summaryPath,
            smartQueue.SelectedCount,
            smartQueue.ExpectedNetProfit,
            smartQueue.AverageUploadScore,
            smartQueue.AverageConfidenceScore,
            failedPageCount,
            ExcelPath: excelPath);
    }

    private static async Task<string> FetchPageWithRetryAsync(
        AmazonSearchClient client,
        string keyword,
        int page,
        IProgress<string>? logProgress,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await client.FetchSearchPageAsync(keyword, page, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logProgress?.Report($"WARN {keyword} | Sayfa {page}: {ex.Message}. Tekrar deneniyor...");
                await Task.Delay(1500, cancellationToken);
            }
            catch (Exception ex)
            {
                logProgress?.Report($"WARN {keyword} | Sayfa {page}: {ex.Message}");
                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static async Task<AmazonProductPageData> FetchAndParseProductPageAsync(
        AmazonSearchClient client,
        AmazonProductPageParser parser,
        ProductResult product,
        IProgress<string>? logProgress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(product.ProductUrl))
        {
            return new AmazonProductPageData(product.Asin, Array.Empty<string>(), string.Empty, new Dictionary<string, string>(), false);
        }

        try
        {
            var html = await client.FetchProductPageAsync(product.ProductUrl, cancellationToken);
            return parser.Parse(html, product.Asin);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logProgress?.Report($"WARN {product.Asin} | Detay sayfası okunamadı: {ex.Message}");
            return new AmazonProductPageData(product.Asin, Array.Empty<string>(), string.Empty, new Dictionary<string, string>(), false);
        }
    }

    private static IOrderedEnumerable<ProductResult> OrderForOutput(IEnumerable<ProductResult> products)
    {
        return products
            .OrderByDescending(p => p.UploadScore)
            .ThenByDescending(p => p.BrandScore)
            .ThenByDescending(p => p.NetProfit)
            .ThenByDescending(p => p.BulletPointQualityScore)
            .ThenByDescending(p => p.DescriptionQualityScore)
            .ThenByDescending(p => p.SpecificationQualityScore)
            .ThenByDescending(p => p.ContentQualityScore)
            .ThenByDescending(p => p.TitleQualityScore)
            .ThenByDescending(p => p.ImageQualityScore)
            .ThenBy(p => p.CompetitionScore)
            .ThenByDescending(p => p.ConfidenceScore)
            .ThenByDescending(p => p.ImageCount);
    }

    private static decimal CalculateAcceptanceRate(int acceptedCount, int scannedCount)
    {
        return scannedCount <= 0 ? 0m : Math.Round(acceptedCount * 100m / scannedCount, 2);
    }

    private static bool LooksLikeBlockedPage(string html)
    {
        return html.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("Enter the characters you see below", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("automated access", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("Sorry, we just need to make sure", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteSummaryAsync(
        string path,
        IReadOnlyList<string> keywords,
        int maxPages,
        int scannedCount,
        int skippedCount,
        int failedPageCount,
        int failedProductPageCount,
        IReadOnlyList<ProductResult> acceptedProducts,
        SmartQueueResult smartQueue,
        int duplicateRemovedCount,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("MarketProHunter Run Summary");
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Keywords: {keywords.Count}");
        builder.AppendLine($"Pages per keyword: {maxPages}");
        builder.AppendLine($"Scanned: {scannedCount}");
        builder.AppendLine($"Accepted: {acceptedProducts.Count}");
        builder.AppendLine($"Skipped: {skippedCount}");
        builder.AppendLine($"Acceptance rate: {CalculateAcceptanceRate(acceptedProducts.Count, scannedCount):0.00}%");
        builder.AppendLine($"Failed pages: {failedPageCount}");
        builder.AppendLine($"Weak or failed product pages: {failedProductPageCount}");
        builder.AppendLine($"Duplicate Killer removed from Smart Queue: {duplicateRemovedCount}");
        builder.AppendLine($"Smart Queue: {smartQueue.SelectedCount}/{smartQueue.RequestedCount}");
        builder.AppendLine($"Expected net profit: ${smartQueue.ExpectedNetProfit:0.00}");
        builder.AppendLine($"Average upload score: {smartQueue.AverageUploadScore:0.00}");
        builder.AppendLine($"Average confidence: {smartQueue.AverageConfidenceScore:0.00}%");
        builder.AppendLine($"Average listing quality: {smartQueue.AverageListingQualityScore:0.00}");
        builder.AppendLine();
        builder.AppendLine("Top 10 Smart Queue Products");
        builder.AppendLine("---------------------------");

        foreach (var item in smartQueue.Items.Take(10))
        {
            var p = item.Product;
            builder.AppendLine($"#{item.Rank} {item.Tier} | {p.Asin} | Upload {p.UploadScore} | Brand {p.BrandScore} {p.BrandLevel} | Title {p.TitleQualityScore} | Images {p.ImageQualityScore} | Bullets {p.BulletPointQualityScore} | Desc {p.DescriptionQualityScore} | Specs {p.SpecificationQualityScore} | Net ${p.NetProfit:0.00}");
            builder.AppendLine($"Brand: {p.Brand}");
            builder.AppendLine($"Title: {Shorten(p.Title)}");
            builder.AppendLine($"Quality: {p.ListingQualityNotes}");
            builder.AppendLine($"Brand Notes: {p.BrandProfileNotes}");
            builder.AppendLine($"Page: {p.ProductPageQualityNotes}");
            builder.AppendLine($"Why: {p.OpportunitySummary}");
            builder.AppendLine();
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, cancellationToken);
    }

    private static string Shorten(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Length <= 80 ? value : value[..80] + "...";
    }
}
