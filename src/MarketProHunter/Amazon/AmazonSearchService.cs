using System.Collections.Concurrent;
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
        var keywordList = keywords
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywordList.Count == 0)
        {
            throw new ArgumentException("En az bir arama kelimesi gerekli.", nameof(keywords));
        }

        if (maxPages < 1)
        {
            maxPages = 1;
        }

        var veroFilter = new VeroBrandFilter("config/vero-brands.txt");
        var productFilter = new ProductFilter(settings, veroFilter);
        var scoringEngine = new ScoringEngine();
        var profitEngine = new EbayProfitEngine();
        var exporter = new CsvExporter();
        var accepted = new ConcurrentBag<ProductResult>();
        var acceptedAsins = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var scannedCount = 0;
        var skippedCount = 0;
        var maxParallel = Math.Clamp(settings.MaxParallelSearches, 1, 8);

        logProgress?.Report($"Paralel tarama başlıyor. Anahtar kelime: {keywordList.Count}, paralel görev: {maxParallel}");
        logProgress?.Report($"Kâr ayarları: eBay %{profitSettings.EbayFinalValueFeePercent}, Promoted %{profitSettings.PromotedPercent}, hedef %{profitSettings.TargetProfitPercent}, min ${profitSettings.MinimumNetProfit}");

        await Parallel.ForEachAsync(
            keywordList,
            new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = cancellationToken },
            async (keyword, token) =>
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
                            var acceptedProduct = product with
                            {
                                SafetyScore = score.SafetyScore,
                                SalesScore = score.SalesScore,
                                ProfitScore = score.ProfitScore,
                                OverallScore = score.OverallScore,
                                ConfidenceScore = score.ConfidenceScore,
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

                            accepted.Add(acceptedProduct);
                            acceptedProgress?.Report(acceptedProduct);
                            logProgress?.Report($"OK  {product.Asin} | Score {score.OverallScore} | Confidence {score.ConfidenceScore}% | eBay ${profit.RecommendedSalePrice} | Net ${profit.NetProfit} | {Shorten(product.Title)}");
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

        var orderedAccepted = accepted
            .OrderByDescending(p => p.ConfidenceScore)
            .ThenByDescending(p => p.OverallScore)
            .ThenByDescending(p => p.NetProfit)
            .ThenByDescending(p => p.SafetyScore)
            .ThenBy(p => p.Price)
            .ToList();

        var outputPath = Path.Combine(AppContext.BaseDirectory, "output", $"amazon_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        await exporter.WriteAsync(outputPath, orderedAccepted, cancellationToken);

        return new SearchRunResult(scannedCount, orderedAccepted.Count, skippedCount, outputPath);
    }

    private static string Shorten(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Length <= 80 ? value : value[..80] + "...";
    }
}
