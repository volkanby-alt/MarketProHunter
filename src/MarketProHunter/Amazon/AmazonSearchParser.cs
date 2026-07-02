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
            var price = ExtractPrice(block);
            if (price <= 0)
            {
                continue;
            }

            var productUrl = $"https://www.amazon.com/dp/{asin}";
            var isChoice = block.Contains("Amazon's Choice", StringComparison.OrdinalIgnoreCase) ||
                           block.Contains("Amazon&#x27;s Choice", StringComparison.OrdinalIgnoreCase);

            results.Add(new ProductResult
            {
                Asin = asin,
                Title = title,
                Brand = GuessBrand(title),
                Price = price,
                IsAmazonChoice = isChoice,
                IsSponsored = block.Contains("Sponsored", StringComparison.OrdinalIgnoreCase),
                HasLowStockWarning = HasLowStockText(block),
                HasUsuallyKeepItemText = block.Contains("Customers usually keep this item", StringComparison.OrdinalIgnoreCase) ||
                                         block.Contains("Customer usually keep this item", StringComparison.OrdinalIgnoreCase),
                ProductUrl = productUrl,
                SearchKeyword = keyword,
                Page = page
            });
        }

        return results
            .GroupBy(p => p.Asin)
            .Select(g => g.First())
            .ToList();
    }

    private static string ExtractAsin(string block)
    {
        var match = Regex.Match(block, "data-asin=\"(?<asin>[A-Z0-9]{10})\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["asin"].Value : string.Empty;
    }

    private static string ExtractTitle(string block)
    {
        var aria = Regex.Match(block, "aria-label=\"(?<title>[^\"]{10,})\"", RegexOptions.IgnoreCase);
        if (aria.Success)
        {
            return WebUtility.HtmlDecode(aria.Groups["title"].Value);
        }

        var span = Regex.Match(block, "<span[^>]*class=\"[^\"]*a-size-base-plus[^\"]*\"[^>]*>(?<title>.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return span.Success ? WebUtility.HtmlDecode(StripTags(span.Groups["title"].Value)) : string.Empty;
    }

    private static decimal ExtractPrice(string block)
    {
        var match = Regex.Match(block, "<span class=\"a-offscreen\">\$(?<price>[0-9,.]+)</span>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return 0m;
        }

        var raw = match.Groups["price"].Value.Replace(",", "");
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : 0m;
    }

    private static bool HasLowStockText(string block)
    {
        return block.Contains("Only", StringComparison.OrdinalIgnoreCase) &&
               (block.Contains("left in stock", StringComparison.OrdinalIgnoreCase) ||
                block.Contains("in stock - order soon", StringComparison.OrdinalIgnoreCase));
    }

    private static string GuessBrand(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var firstPart = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return firstPart.Trim(',', '.', '-', ':');
    }

    private static string Clean(string value) => WebUtility.HtmlDecode(StripTags(value)).Trim();

    private static string StripTags(string value) => Regex.Replace(value, "<.*?>", string.Empty, RegexOptions.Singleline);

    [GeneratedRegex("<div[^>]+data-asin=\"[A-Z0-9]{10}\"[\\s\\S]*?(?=<div[^>]+data-asin=\"[A-Z0-9]{10}\"|</body>)", RegexOptions.IgnoreCase)]
    private static partial Regex ProductCardRegex();
}
