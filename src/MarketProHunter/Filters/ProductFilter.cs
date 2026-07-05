using MarketProHunter.Models;

namespace MarketProHunter.Filters;

public sealed class ProductFilter
{
    private readonly SearchSettings _settings;
    private readonly VeroBrandFilter _veroBrandFilter;

    public ProductFilter(SearchSettings settings, VeroBrandFilter veroBrandFilter)
    {
        _settings = settings;
        _veroBrandFilter = veroBrandFilter;
    }

    public FilterDecision Evaluate(ProductResult product)
    {
        if (string.IsNullOrWhiteSpace(product.Asin))
        {
            return FilterDecision.Reject("ASIN bulunamadı");
        }

        if (string.IsNullOrWhiteSpace(product.Title))
        {
            return FilterDecision.Reject("Başlık bulunamadı");
        }

        if (LooksLikeBadListing(product.Title))
        {
            return FilterDecision.Reject("İstenmeyen ürün tipi");
        }

        if (product.Price < _settings.MinPrice || product.Price > _settings.MaxPrice)
        {
            return FilterDecision.Reject($"Fiyat aralık dışı: {product.Price}");
        }

        if (_settings.RequireAmazonChoice && !product.IsAmazonChoice)
        {
            return FilterDecision.Reject("Amazon Choice değil");
        }

        if (_settings.ExcludeSponsored && product.IsSponsored)
        {
            return FilterDecision.Reject("Sponsored sonuç");
        }

        if (_settings.ExcludeLowStock && product.HasLowStockWarning)
        {
            return FilterDecision.Reject("Stok az uyarısı var");
        }

        if (_settings.ExcludeUsuallyKeepItem && product.HasUsuallyKeepItemText)
        {
            return FilterDecision.Reject("Customer usually keep this item var");
        }

        if (_veroBrandFilter.IsBlocked(product.Brand) || _veroBrandFilter.IsBlocked(product.Title))
        {
            return FilterDecision.Reject("VeRO riskli marka/başlık");
        }

        return FilterDecision.Accept("Uygun");
    }

    private static bool LooksLikeBadListing(string title)
    {
        var blockedTerms = new[]
        {
            "gift card",
            "eGift card",
            "digital code",
            "download code",
            "subscription",
            "renewed",
            "refurbished",
            "used",
            "open box",
            "replacement plan",
            "protection plan",
            "warranty",
            "bundle only",
            "parts only"
        };

        return blockedTerms.Any(term => title.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
