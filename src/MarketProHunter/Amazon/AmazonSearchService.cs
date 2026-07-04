using System.Collections.Concurrent;
using System.Text;
using System.Threading;
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
        var productFilter = new ProductFilter(settings, veroFilter);
        var scoringEngine = new ScoringEngine();
        var opportunityAnalyzer = new OpportunityAnalyzer();
        var smartQueueEngine = new SmartQueueEngine();
        var profitEngine = new EbayProfitEngine();
        var exporter = new CsvExporter();
        var accepted = new ConcurrentBag<ProductResult>();
        var acceptedAsins = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var scannedCount = 0;
        var skippedCount = 0;
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

                var html = await client.FetchSearchPageAsync(keyword, page, token);
                var products = parser.Parse(html, keyword, page);
                logProgress?.Report($"{keyword} | Sayfa {page}: {products.Count} ürün kartı bulundu.");

                foreach (var product in products)
                {
                    token.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref scannedCount);
                    var decision = productFilter.Evaluate(product);

                    if (decision.Accepted && acceptedAsins.TryAdd(product.Asin, 0))
                    {
                        var score = scoringEngine.Score(product);
                        var profit = profitEngine.Calculate(product, profitSettings);
                        var enrichedProduct = product with
                        {
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
                        acceptedProgress?.Report(acceptedProduct);
                        logProgress?.Report($"OK  {product.Asin} | {score.UploadDecision} | Upload {score.UploadScore} | Risk {opportunity.RiskLevel} | {opportunity.SweetSpot} | Net ${profit.NetProfit}");
                    }
                    else
                    {
                        Interlocked.Increment(ref skippedCount);
                        var reason = decision.Accepted ? "Tekrar eden ASIN" : decision.Reason;
                        logProgress?.Report($"SKIP {product.Asin} | {reason}");
                    }
                }

                await Task.Delay(settings.DelayBetweenPagesMs, token);
            }
        });

        var orderedAccepted = OrderForOutput(accepted).ToList();
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(AppContext.BaseDirectory, "output", $"amazon_results_{timestamp}.csv");
        var smartQueuePath = Path.Combine(AppContext.BaseDirectory, "output", $"smart_queue_top50_{timestamp}.csv");
        var summaryPath = Path.Combine(AppContext.BaseDirectory, "output", $"run_summary_{timestamp}.txt");
        await exporter.WriteAsync(outputPath, orderedAccepted, cancellationToken);

        var smartQueue = smartQueueEngine.Build(orderedAccepted, SmartQueueEngine.DefaultQueueSize);
        await exporter.WriteSmartQueueAsync(smartQueuePath, smartQueue, cancellationToken);
        await WriteSummaryAsync(summaryPath, keywordList, maxPages, scannedCount, skippedCount, orderedAccepted, smartQueue, cancellationToken);
        logProgress?.Report($"Smart Queue hazır: {smartQueue.SelectedCount}/{smartQueue.RequestedCount} ürün | Beklenen net kâr: ${smartQueue.ExpectedNetProfit} | Ortalama Upload: {smartQueue.AverageUploadScore} | Ortalama Confidence: {smartQueue.AverageConfidenceScore}%");
        logProgress?.Report($"Smart Queue CSV: {smartQueuePath}");
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
            smartQueue.AverageConfidenceScore);
    }

    private static IOrderedEnumerable<ProductResult> OrderForOutput(IEnumerable<ProductResult> products)
    {
        return products
            .OrderByDescending(p => p.UploadScore)
            .ThenByDescending(p => p.NetProfit)
            .ThenBy(p => p.CompetitionScore)
            .ThenByDescending(p => p.ConfidenceScore)
            .ThenByDescending(p => p.ImageCount);
    }

    private static async Task WriteSummaryAsync(
        string path,
        IReadOnlyList<string> keywords,
        int maxPages,
        int scannedCount,
        int skippedCount,
        IReadOnlyList<ProductResult> acceptedProducts,
        SmartQueueResult smartQueue,
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
        builder.AppendLine($"Smart Queue: {smartQueue.SelectedCount}/{smartQueue.RequestedCount}");
        builder.AppendLine($"Expected net profit: ${smartQueue.ExpectedNetProfit:0.00}");
        builder.AppendLine($"Average upload score: {smartQueue.AverageUploadScore:0.00}");
        builder.AppendLine($"Average confidence: {smartQueue.AverageConfidenceScore:0.00}%");
        builder.AppendLine();
        builder.AppendLine("Top 10 Smart Queue Products");
        builder.AppendLine("---------------------------");

        foreach (var item in smartQueue.Items.Take(10))
        {
            var p = item.Product;
            builder.AppendLine($"#{item.Rank} {item.Tier} | {p.Asin} | Upload {p.UploadScore} | Risk {p.RiskLevel} | Net ${p.NetProfit:0.00}");
            builder.AppendLine($"Brand: {p.Brand}");
            builder.AppendLine($"Title: {Shorten(p.Title)}");
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
