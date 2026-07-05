using System.Text.RegularExpressions;
using MarketProHunter.Models;

namespace MarketProHunter.Deduplication;

public static partial class DuplicateKeyBuilder
{
    private static readonly string[] VariationTerms =
    {
        "black", "white", "blue", "red", "green", "gray", "grey", "pink", "purple", "orange", "yellow", "brown", "silver", "gold",
        "small", "medium", "large", "x-large", "xl", "xxl", "one size",
        "1 pack", "2 pack", "3 pack", "4 pack", "5 pack", "6 pack", "8 pack", "10 pack", "12 pack",
        "single", "set of 2", "set of 3", "set of 4", "set of 6", "set of 8", "set of 10", "set of 12"
    };

    public static string Build(ProductResult product)
    {
        if (!string.IsNullOrWhiteSpace(product.Asin)) return $"asin:{product.Asin.Trim().ToUpperInvariant()}";

        var brand = Normalize(product.Brand);
        var title = NormalizeTitle(product.Title);
        return string.IsNullOrWhiteSpace(brand) ? title : $"{brand}|{title}";
    }

    public static IReadOnlyList<ProductResult> KeepBestCandidates(IEnumerable<ProductResult> products)
    {
        return products
            .GroupBy(Build, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(product => product.UploadScore)
                .ThenByDescending(product => product.NetProfit)
                .ThenByDescending(product => product.OverallScore)
                .ThenByDescending(product => product.ImageCount)
                .First())
            .OrderByDescending(product => product.UploadScore)
            .ThenByDescending(product => product.NetProfit)
            .ToList();
    }

    private static string NormalizeTitle(string value)
    {
        var normalized = Normalize(value);
        foreach (var term in VariationTerms)
        {
            normalized = Regex.Replace(normalized, $@"\b{Regex.Escape(term)}\b", " ", RegexOptions.IgnoreCase);
        }

        normalized = SizePattern().Replace(normalized, " ");
        normalized = OuncePattern().Replace(normalized, " ");
        normalized = CountPattern().Replace(normalized, " ");
        normalized = ExtraSpacePattern().Replace(normalized, " ").Trim();
        return normalized;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.ToLowerInvariant();
        normalized = PunctuationPattern().Replace(normalized, " ");
        normalized = ExtraSpacePattern().Replace(normalized, " ").Trim();
        return normalized;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.IgnoreCase)]
    private static partial Regex PunctuationPattern();

    [GeneratedRegex("\\b\\d+(\\.\\d+)?\\s?(inch|in|cm|mm|ft|feet|oz|ounce|ounces|lb|lbs|quart|qt|gallon|gal)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex SizePattern();

    [GeneratedRegex("\\b\\d+(\\.\\d+)?\\s?(oz|ounce|ounces)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex OuncePattern();

    [GeneratedRegex("\\b\\d+\\s?(count|ct|pcs|pieces|pack|pk)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex CountPattern();

    [GeneratedRegex("\\s+")]
    private static partial Regex ExtraSpacePattern();
}
