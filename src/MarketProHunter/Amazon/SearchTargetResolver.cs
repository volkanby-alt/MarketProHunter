using MarketProHunter.Models;

namespace MarketProHunter.Amazon;

public static class SearchTargetResolver
{
    public static string BuildInitialUrl(SearchTarget target, SearchSettings settings)
    {
        return target.Mode switch
        {
            SearchMode.AmazonStoreLink => NormalizeStoreUrl(target.StoreUrl, settings.MarketplaceBaseUrl),
            SearchMode.ReadyMarket => BuildKeywordUrl(BuildReadyMarketQuery(target), 1, settings),
            SearchMode.CustomSearch => BuildKeywordUrl(target.CustomQuery ?? string.Empty, 1, settings),
            _ => throw new InvalidOperationException("Geçersiz arama modu.")
        };
    }

    public static string BuildPageUrl(SearchTarget target, int page, SearchSettings settings)
    {
        if (page < 1) page = 1;

        return target.Mode switch
        {
            SearchMode.AmazonStoreLink => AddOrReplacePage(NormalizeStoreUrl(target.StoreUrl, settings.MarketplaceBaseUrl), page),
            SearchMode.ReadyMarket => BuildKeywordUrl(BuildReadyMarketQuery(target), page, settings),
            SearchMode.CustomSearch => BuildKeywordUrl(target.CustomQuery ?? string.Empty, page, settings),
            _ => throw new InvalidOperationException("Geçersiz arama modu.")
        };
    }

    public static void Validate(SearchTarget target)
    {
        switch (target.Mode)
        {
            case SearchMode.ReadyMarket when string.IsNullOrWhiteSpace(target.MarketName):
                throw new InvalidOperationException("Bir hazır mağaza seçin.");
            case SearchMode.AmazonStoreLink when string.IsNullOrWhiteSpace(target.StoreUrl):
                throw new InvalidOperationException("Amazon mağaza linkini girin.");
            case SearchMode.CustomSearch when string.IsNullOrWhiteSpace(target.CustomQuery):
                throw new InvalidOperationException("Özel arama metnini girin.");
        }
    }

    private static string BuildReadyMarketQuery(SearchTarget target)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(target.MarketName)) parts.Add(target.MarketName.Trim());
        if (!string.IsNullOrWhiteSpace(target.SubCategory) && !target.SubCategory.Equals("Tümü", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(target.SubCategory.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(target.Category) && !target.Category.Equals("Tümü", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(target.Category.Trim());
        }

        return string.Join(" ", parts);
    }

    private static string BuildKeywordUrl(string query, int page, SearchSettings settings)
    {
        var encoded = Uri.EscapeDataString(query.Trim());
        var min = Math.Max(1, (int)(settings.MinPrice * 100));
        var max = Math.Max(min, (int)(settings.MaxPrice * 100));
        return $"{settings.MarketplaceBaseUrl.TrimEnd('/')}/s?k={encoded}&page={page}&rh=p_36%3A{min}-{max}&language=en_US&currency=USD";
    }

    private static string NormalizeStoreUrl(string? value, string marketplaceBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Amazon mağaza linki boş olamaz.");
        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute)) return absolute.ToString();
        return $"{marketplaceBaseUrl.TrimEnd('/')}/{trimmed.TrimStart('/')}";
    }

    private static string AddOrReplacePage(string url, int page)
    {
        var fragmentIndex = url.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : string.Empty;
        var withoutFragment = fragmentIndex >= 0 ? url[..fragmentIndex] : url;

        var queryIndex = withoutFragment.IndexOf('?');
        var baseUrl = queryIndex >= 0 ? withoutFragment[..queryIndex] : withoutFragment;
        var query = queryIndex >= 0 ? withoutFragment[(queryIndex + 1)..] : string.Empty;

        var parts = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !x.StartsWith("page=", StringComparison.OrdinalIgnoreCase))
            .ToList();
        parts.Add($"page={page}");

        return $"{baseUrl}?{string.Join("&", parts)}{fragment}";
    }
}
