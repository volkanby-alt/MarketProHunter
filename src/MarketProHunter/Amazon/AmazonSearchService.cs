using MarketProHunter.Export;
using MarketProHunter.Filters;
using MarketProHunter.Models;

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
        if (string.IsNullOrWhiteSpace(keyword))
        {
            throw new ArgumentException("Arama kelimesi boş olamaz.", nameof(keyword));
        }

        if (maxPages < 1)
        {
            maxPages = 1;
        }

        var veroFilter = new VeroBrandFilter("config/vero-brands.txt");
        var productFilter = new ProductFilter(settings, veroFilter);
        var client = new AmazonSearchClient(settings);
        var parser = new AmazonSearchParser();
        var exporter = new CsvExporter();
        var accepted = new List<ProductResult>();
        var scannedCount = 0;
        var skippedCount = 0;

        for (var page = 1; page <= maxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logProgress?.Report($"Sayfa taranıyor: {page}/{maxPages}");

            var html = await client.FetchSearchPageAsync(keyword, page, cancellationToken);
            var products = parser.Parse(html, keyword, page);

            logProgress?.Report($"Sayfa {page}: {products.Count} ürün kartı bulundu.");

            foreach (var product in products)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scannedCount++;
                var decision = productFilter.Evaluate(product);

                if (decision.Accepted)
                {
                    var acceptedProduct = product with { Notes = decision.Reason };
                    accepted.Add(acceptedProduct);
                    acceptedProgress?.Report(acceptedProduct);
                    logProgress?.Report($"OK  {product.Asin} | ${product.Price} | {Shorten(product.Title)}");
                }
                else
                {
                    skippedCount++;
                    logProgress?.Report($"SKIP {product.Asin} | {decision.Reason}");
                }
            }

            await Task.Delay(settings.DelayBetweenPagesMs, cancellationToken);
        }

        var outputPath = Path.Combine(AppContext.BaseDirectory, "output", $"amazon_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        await exporter.WriteAsync(outputPath, accepted, cancellationToken);

        return new SearchRunResult(scannedCount, accepted.Count, skippedCount, outputPath);
    }

    private static string Shorten(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= 80 ? value : value[..80] + "...";
    }
}
