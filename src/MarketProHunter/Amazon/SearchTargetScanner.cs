using MarketProHunter.Models;

namespace MarketProHunter.Amazon;

public sealed class SearchTargetScanner
{
    public async Task<IReadOnlyList<ProductResult>> ScanAsync(
        SearchTarget target,
        SearchSettings settings,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default)
    {
        SearchTargetResolver.Validate(target);

        var results = new List<ProductResult>();
        var seenAsins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parser = new AmazonSearchParser();
        var navigator = new AmazonPageNavigator();
        var maxPages = target.ScanAllPages ? 500 : Math.Max(1, target.MaxPages);

        using var client = new AmazonSearchClient(settings);
        logProgress?.Report($"Tek Chrome oturumu açıldı | ZIP: {settings.ZipCode} | Hedef: {target.DisplayName}");

        for (var page = 1; page <= maxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = SearchTargetResolver.BuildPageUrl(target, page, settings);
            logProgress?.Report($"{target.DisplayName} | Sayfa {page}{(target.ScanAllPages ? string.Empty : $"/{maxPages}")}");
            logProgress?.Report($"URL: {url}");

            var html = await client.FetchUrlPageAsync(url, target.DisplayName, page, cancellationToken);
            var products = parser.Parse(html, target.DisplayName, page);
            logProgress?.Report($"Sayfa {page}: {products.Count} ürün kartı bulundu.");

            foreach (var product in products)
            {
                if (seenAsins.Add(product.Asin)) results.Add(product);
            }

            if (!navigator.ShouldContinue(
                    html,
                    products.Count,
                    target.ScanAllPages,
                    page,
                    maxPages,
                    out var stopReason))
            {
                logProgress?.Report($"Tarama durdu: {stopReason}");
                break;
            }
        }

        logProgress?.Report($"Ham tarama tamamlandı: {results.Count} benzersiz ASIN.");
        return results;
    }
}
