using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MarketProHunter.Models;

namespace MarketProHunter.Amazon;

public sealed class AmazonSearchParser
{
    public IReadOnlyList<ProductResult> Parse(string html, string keyword, int page)
    {
        if (string.IsNullOrWhiteSpace(html)) return Array.Empty<ProductResult>();

        var results = new List<ProductResult>();
        foreach (var block in ExtractProductCards(html))
        {
            var asin = ExtractAsin(block);
            if (string.IsNullOrWhiteSpace(asin) || IsPlaceholderAsin(asin)) continue;

            var decoded = WebUtility.HtmlDecode(block);
            var title = Clean(ExtractTitle(decoded));
            if (string.IsNullOrWhiteSpace(title) || IsNonProductCard(title)) continue;

            var price = ExtractPrice(decoded);
            if (price <= 0) continue;

            var images = ExtractImageUrls(decoded);
            var visual = AnalyzeVisualCompleteness(images.Count);

            results.Add(new ProductResult
            {
                Asin = asin,
                Title = title,
                Brand = GuessBrand(title),
                Price = price,
                IsAmazonChoice = ContainsAny(decoded, "Amazon's Choice", "Amazon’s Choice", "Amazon Choice", "Overall Pick"),
                IsSponsored = ContainsAny(decoded, "Sponsored", "AdHolder", "sponsored-label"),
                HasLowStockWarning = HasLowStockText(decoded),
                HasUsuallyKeepItemText = HasUsuallyKeepText(decoded),
                Rating = ExtractRating(decoded),
                ReviewCount = ExtractReviewCount(decoded),
                ImageUrl1 = GetImage(images, 0),
                ImageUrl2 = GetImage(images, 1),
                ImageUrl3 = GetImage(images, 2),
                ImageUrl4 = GetImage(images, 3),
                ImageUrl5 = GetImage(images, 4),
                ImageUrl6 = GetImage(images, 5),
                ImageCount = images.Count,
                VisualRiskLevel = visual.Level,
                VisualRiskNotes = visual.Notes,
                ProductUrl = ExtractProductUrl(decoded, asin),
                SearchKeyword = keyword,
                Page = page
            });
        }

        return results
            .GroupBy(x => x.Asin, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(p => p.IsAmazonChoice).ThenByDescending(p => p.ImageCount).First())
            .ToList();
    }

    private static IReadOnlyList<string> ExtractProductCards(string html)
    {
        var blocks = new List<string>();
        var markers = Regex.Matches(html, "data-component-type=\"s-search-result\"", RegexOptions.IgnoreCase);

        foreach (Match marker in markers)
        {
            var start = html.LastIndexOf("<div", marker.Index, StringComparison.OrdinalIgnoreCase);
            if (start < 0) continue;

            var next = marker.NextMatch();
            var end = next.Success
                ? html.LastIndexOf("<div", next.Index, StringComparison.OrdinalIgnoreCase)
                : html.IndexOf("</body>", marker.Index, StringComparison.OrdinalIgnoreCase);

            if (end <= start) end = Math.Min(html.Length, start + 120_000);
            var length = Math.Min(end - start, 120_000);
            if (length > 0) blocks.Add(html.Substring(start, length));
        }

        if (blocks.Count > 0) return blocks;

        foreach (Match match in Regex.Matches(
                     html,
                     "<div[^>]+data-asin=\"[A-Z0-9]{10}\"[\\s\\S]*?(?=<div[^>]+data-asin=\"[A-Z0-9]{10}\"|</body>)",
                     RegexOptions.IgnoreCase))
        {
            blocks.Add(match.Value);
        }

        return blocks;
    }

    private static string ExtractAsin(string block)
    {
        var match = Regex.Match(block, "data-asin=\"(?<asin>[A-Z0-9]{10})\"", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups["asin"].Value;
        match = Regex.Match(block, "/(?:dp|gp/product)/(?<asin>[A-Z0-9]{10})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["asin"].Value : string.Empty;
    }

    private static string ExtractTitle(string block)
    {
        // Current Amazon result cards normally expose the complete product title in
        // the product image alt attribute. Prefer it over short brand/store labels.
        var imageAltMatches = Regex.Matches(
            block,
            "<img[^>]+alt=\"(?<title>[^\"]{15,})\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match imageAlt in imageAltMatches)
        {
            var candidate = Clean(imageAlt.Groups["title"].Value);
            if (candidate.Length >= 15 && !IsNonProductCard(candidate)) return candidate;
        }

        var patterns = new[]
        {
            "<h2[^>]*>\\s*<a[^>]*>[\\s\\S]*?<span[^>]*>(?<title>[\\s\\S]*?)</span>[\\s\\S]*?</a>\\s*</h2>",
            "<a[^>]*class=\"[^\"]*a-link-normal[^\"]*s-line-clamp[^\"]*\"[^>]*>[\\s\\S]*?<span[^>]*>(?<title>[\\s\\S]*?)</span>",
            "<span[^>]*class=\"[^\"]*(?:a-size-base-plus|a-size-medium|a-text-normal)[^\"]*\"[^>]*>(?<title>[\\s\\S]*?)</span>"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(block, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success) continue;
            var candidate = StripTags(match.Groups["title"].Value);
            if (candidate.Length >= 15) return candidate;
        }

        return string.Empty;
    }

    private static decimal ExtractPrice(string block)
    {
        foreach (Match match in Regex.Matches(block, "class=\"[^\"]*a-offscreen[^\"]*\"[^>]*>\\s*\\$(?<price>[0-9,.]+)", RegexOptions.IgnoreCase))
        {
            var context = GetContext(block, match.Index, 160);
            if (ContainsAny(context, "List Price", "Was:", "Typical price", "coupon", "Subscribe & Save")) continue;
            var price = ParsePrice(match.Groups["price"].Value);
            if (price > 0) return price;
        }

        var whole = Regex.Match(block, "class=\"[^\"]*a-price-whole[^\"]*\"[^>]*>(?<whole>[0-9,]+)", RegexOptions.IgnoreCase);
        if (!whole.Success) return 0m;
        var fraction = Regex.Match(block, "class=\"[^\"]*a-price-fraction[^\"]*\"[^>]*>(?<fraction>[0-9]{2})", RegexOptions.IgnoreCase);
        var raw = whole.Groups["whole"].Value.Replace(",", "").Trim('.');
        if (fraction.Success) raw += "." + fraction.Groups["fraction"].Value;
        return ParsePrice(raw);
    }

    private static decimal ExtractRating(string block)
    {
        var match = Regex.Match(block, "(?<rating>[0-5](?:\\.[0-9])?)\\s+out of 5 stars", RegexOptions.IgnoreCase);
        return match.Success && decimal.TryParse(match.Groups["rating"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private static int ExtractReviewCount(string block)
    {
        var patterns = new[]
        {
            "aria-label=\"(?<reviews>[0-9,]+)\\s+(?:ratings|reviews)\"",
            "href=\"[^\"]*(?:customerReviews|product-reviews)[^\"]*\"[^>]*>[\\s\\S]*?(?<reviews>[0-9,]+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(block, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && int.TryParse(match.Groups["reviews"].Value.Replace(",", ""), out var count)) return count;
        }
        return 0;
    }

    private static IReadOnlyList<string> ExtractImageUrls(string block)
    {
        var urls = new List<string>();
        foreach (Match match in Regex.Matches(block, "(?:src|data-src)=\"(?<url>https://[^\"]+?\\.(?:jpg|jpeg|png|webp)[^\"]*)\"", RegexOptions.IgnoreCase))
        {
            AddImageUrl(urls, match.Groups["url"].Value);
            if (urls.Count >= 6) break;
        }
        return urls;
    }

    private static void AddImageUrl(List<string> urls, string raw)
    {
        var url = WebUtility.HtmlDecode(raw).Trim().Trim('\\', '"', '\'', ',', '{', '}');
        if (!url.Contains("media-amazon", StringComparison.OrdinalIgnoreCase) && !url.Contains("ssl-images-amazon", StringComparison.OrdinalIgnoreCase)) return;
        url = Regex.Replace(url, @"\._[^.]+_\.", ".");
        if (!urls.Contains(url, StringComparer.OrdinalIgnoreCase)) urls.Add(url);
    }

    private static string ExtractProductUrl(string block, string asin)
    {
        var match = Regex.Match(block, "href=\"(?<url>/[^\"]*(?:dp|gp/product)/[A-Z0-9]{10}[^\"]*)\"", RegexOptions.IgnoreCase);
        if (!match.Success) return $"https://www.amazon.com/dp/{asin}";
        var clean = WebUtility.HtmlDecode(match.Groups["url"].Value).Split('?')[0];
        return clean.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? clean : "https://www.amazon.com" + clean;
    }

    private static bool HasLowStockText(string block) => ContainsAny(block, "Only 1 left in stock", "Only 2 left in stock", "Only 3 left in stock", "Only 4 left in stock", "Only 5 left in stock", "left in stock - order soon", "in stock - order soon");
    private static bool HasUsuallyKeepText(string block) => ContainsAny(block, "Customers usually keep this item", "Customer usually keeps this item", "usually keep this item", "usually keeps this item");
    private static bool IsNonProductCard(string title) => ContainsAny(title, "Shop by category", "Related searches", "Explore more", "Need help");
    private static bool IsPlaceholderAsin(string asin) => asin.Equals("0000000000", StringComparison.OrdinalIgnoreCase) || asin.Distinct().Count() <= 2;
    private static string GetImage(IReadOnlyList<string> urls, int index) => index < urls.Count ? urls[index] : string.Empty;
    private static decimal ParsePrice(string raw) => decimal.TryParse(WebUtility.HtmlDecode(raw).Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    private static string GuessBrand(string title) => NormalizeBrand(title.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty);
    private static string NormalizeBrand(string value) => value.Trim(',', '.', '-', ':', ';', '|', '(', ')', '[', ']', '"', '\'');
    private static string GetContext(string value, int index, int radius) { var start = Math.Max(0, index - radius); return value.Substring(start, Math.Min(value.Length - start, radius * 2)); }
    private static bool ContainsAny(string value, params string[] needles) => needles.Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase));
    private static string Clean(string value) => WebUtility.HtmlDecode(StripTags(value)).Replace("\n", " ").Replace("\r", " ").Trim();
    private static string StripTags(string value) => Regex.Replace(value, "<.*?>", string.Empty, RegexOptions.Singleline);
    private static (string Level, string Notes) AnalyzeVisualCompleteness(int count) => count switch { >= 4 => ("LOW", $"Image set complete: {count}/6"), > 0 => ("MEDIUM", $"Visual Data Incomplete: {count} image"), _ => ("HIGH", "No product image found") };
}
