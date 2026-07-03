using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MarketProHunter.Models;

namespace MarketProHunter.Amazon;

public sealed partial class AmazonSearchParser
{
    public IReadOnlyList<ProductResult> Parse(string html, string keyword, int page)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<ProductResult>();
        }

        var results = new List<ProductResult>();
        var cards = ProductCardRegex().Matches(html);

        foreach (Match card in cards)
        {
            var block = card.Value;
            var asin = ExtractAsin(block);
            if (string.IsNullOrWhiteSpace(asin))
            {
                continue;
            }

            var title = Clean(ExtractTitle(block));
            if (string.IsNullOrWhiteSpace(title) || IsNonProductCard(title))
            {
                continue;
            }

            var price = ExtractPrice(block);
            if (price <= 0)
            {
                continue;
            }

            var productUrl = ExtractProductUrl(block, asin);
            var decodedBlock = WebUtility.HtmlDecode(block);
            var isChoice = ContainsAny(decodedBlock,
                "Amazon's Choice",
                "Amazon’s Choice",
                "Amazon Choice");

            results.Add(new ProductResult
            {
                Asin = asin,
                Title = title,
                Brand = GuessBrand(title),
                Price = price,
                IsAmazonChoice = isChoice,
                IsSponsored = ContainsAny(decodedBlock, "Sponsored", "sponsored"),
                HasLowStockWarning = HasLowStockText(decodedBlock),
                HasUsuallyKeepItemText = HasUsuallyKeepText(decodedBlock),
                ProductUrl = productUrl,
                SearchKeyword = keyword,
                Page = page
            });
        }

        return results
            .GroupBy(p => p.Asin, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string ExtractAsin(string block)
    {
        var match = Regex.Match(block, "data-asin=\"(?<asin>[A-Z0-9]{10})\"", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["asin"].Value;
        }

        match = Regex.Match(block, "/(?:dp|gp/product)/(?<asin>[A-Z0-9]{10})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["asin"].Value : string.Empty;
    }

    private static string ExtractTitle(string block)
    {
        var titleMatch = Regex.Match(block, "<h2[^>]*>[\\s\\S]*?<span[^>]*>(?<title>[\\s\\S]*?)</span>[\\s\\S]*?</h2>", RegexOptions.IgnoreCase);
        if (titleMatch.Success)
        {
            return WebUtility.HtmlDecode(StripTags(titleMatch.Groups["title"].Value));
        }

        var aria = Regex.Match(block, "aria-label=\"(?<title>[^\"]{10,})\"", RegexOptions.IgnoreCase);
        if (aria.Success)
        {
            return WebUtility.HtmlDecode(aria.Groups["title"].Value);
        }

        var span = Regex.Match(block, "<span[^>]*class=\"[^\"]*(?:a-size-base-plus|a-size-medium)[^\"]*\"[^>]*>(?<title>.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return span.Success ? WebUtility.HtmlDecode(StripTags(span.Groups["title"].Value)) : string.Empty;
    }

    private static decimal ExtractPrice(string block)
    {
        var offscreenMatches = Regex.Matches(block, "<span[^>]*class=\"[^\"]*a-offscreen[^\"]*\"[^>]*>\\s*\\$(?<price>[0-9,.]+)\\s*</span>", RegexOptions.IgnoreCase);
        foreach (Match match in offscreenMatches)
        {
            var price = ParsePrice(match.Groups["price"].Value);
            if (price > 0)
            {
                return price;
            }
        }

        var whole = Regex.Match(block, "<span[^>]*class=\"[^\"]*a-price-whole[^\"]*\"[^>]*>(?<whole>[0-9,]+)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (whole.Success)
        {
            var fraction = Regex.Match(block, "<span[^>]*class=\"[^\"]*a-price-fraction[^\"]*\"[^>]*>(?<fraction>[0-9]{2})</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var raw = StripTags(whole.Groups["whole"].Value).Replace(",", "").Trim('.');
            if (fraction.Success)
            {
                raw += "." + StripTags(fraction.Groups["fraction"].Value);
            }

            return ParsePrice(raw);
        }

        return 0m;
    }

    private static decimal ParsePrice(string raw)
    {
        raw = WebUtility.HtmlDecode(raw).Replace(",", "").Trim();
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : 0m;
    }

    private static string ExtractProductUrl(string block, string asin)
    {
        var match = Regex.Match(block, "href=\"(?<url>/(?:[^\"]*?/)?(?:dp|gp/product)/[A-Z0-9]{10}[^\"]*)\"", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return $"https://www.amazon.com/dp/{asin}";
        }

        var url = WebUtility.HtmlDecode(match.Groups["url"].Value);
        var clean = url.Split('?')[0];
        if (!clean.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            clean = "https://www.amazon.com" + clean;
        }

        return clean;
    }

    private static bool HasLowStockText(string block)
    {
        return ContainsAny(block,
            "Only 1 left in stock",
            "Only 2 left in stock",
            "Only 3 left in stock",
            "Only 4 left in stock",
            "Only 5 left in stock",
            "left in stock - order soon",
            "in stock - order soon");
    }

    private static bool HasUsuallyKeepText(string block)
    {
        return ContainsAny(block,
            "Customers usually keep this item",
            "Customer usually keeps this item",
            "usually keep this item",
            "usually keeps this item");
    }

    private static bool IsNonProductCard(string title)
    {
        return ContainsAny(title,
            "Shop by category",
            "Related searches",
            "Explore more",
            "Need help");
    }

    private static string GuessBrand(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var cleaned = title.Trim();
        var byMatch = Regex.Match(cleaned, "\\bby\\s+(?<brand>[A-Za-z0-9][A-Za-z0-9&' .-]{1,30})", RegexOptions.IgnoreCase);
        if (byMatch.Success)
        {
            return NormalizeBrand(byMatch.Groups["brand"].Value);
        }

        var firstPart = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return NormalizeBrand(firstPart);
    }

    private static string NormalizeBrand(string value)
    {
        return value.Trim(',', '.', '-', ':', ';', '|', '(', ')', '[', ']', '"', '\'');
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string Clean(string value) => WebUtility.HtmlDecode(StripTags(value)).Replace("\n", " ").Replace("\r", " ").Trim();

    private static string StripTags(string value) => Regex.Replace(value, "<.*?>", string.Empty, RegexOptions.Singleline);

    [GeneratedRegex("<div[^>]+data-asin=\"[A-Z0-9]{10}\"[\\s\\S]*?(?=<div[^>]+data-asin=\"[A-Z0-9]{10}\"|</body>)", RegexOptions.IgnoreCase)]
    private static partial Regex ProductCardRegex();
}
