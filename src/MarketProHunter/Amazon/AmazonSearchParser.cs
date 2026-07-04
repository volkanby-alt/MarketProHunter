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
            if (string.IsNullOrWhiteSpace(asin) || IsPlaceholderAsin(asin)) continue;

            var decodedBlock = WebUtility.HtmlDecode(block);
            var title = Clean(ExtractTitle(decodedBlock));
            if (string.IsNullOrWhiteSpace(title) || IsNonProductCard(title)) continue;

            var price = ExtractPrice(decodedBlock);
            if (price <= 0) continue;

            var productUrl = ExtractProductUrl(decodedBlock, asin);
            var imageUrls = ExtractImageUrls(decodedBlock);
            var visualRisk = AnalyzeVisualCompleteness(imageUrls.Count);
            var isChoice = ContainsAny(decodedBlock, "Amazon's Choice", "Amazon’s Choice", "Amazon Choice", "Overall Pick");

            results.Add(new ProductResult
            {
                Asin = asin,
                Title = title,
                Brand = GuessBrand(title),
                Price = price,
                IsAmazonChoice = isChoice,
                IsSponsored = ContainsAny(decodedBlock, "Sponsored", "AdHolder", "sponsored-label"),
                HasLowStockWarning = HasLowStockText(decodedBlock),
                HasUsuallyKeepItemText = HasUsuallyKeepText(decodedBlock),
                Rating = ExtractRating(decodedBlock),
                ReviewCount = ExtractReviewCount(decodedBlock),
                ImageUrl1 = GetImage(imageUrls, 0),
                ImageUrl2 = GetImage(imageUrls, 1),
                ImageUrl3 = GetImage(imageUrls, 2),
                ImageUrl4 = GetImage(imageUrls, 3),
                ImageUrl5 = GetImage(imageUrls, 4),
                ImageUrl6 = GetImage(imageUrls, 5),
                ImageCount = imageUrls.Count,
                VisualRiskLevel = visualRisk.Level,
                VisualRiskNotes = visualRisk.Notes,
                ProductUrl = productUrl,
                SearchKeyword = keyword,
                Page = page
            });
        }

        return results
            .GroupBy(p => p.Asin, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.IsAmazonChoice).ThenByDescending(p => p.ImageCount).First())
            .ToList();
    }

    private static string ExtractAsin(string block)
    {
        var match = Regex.Match(block, "data-asin=\"(?<asin>[A-Z0-9]{10})\"", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups["asin"].Value;
        match = Regex.Match(block, "/(?:dp|gp/product)/(?<asin>[A-Z0-9]{10})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["asin"].Value : string.Empty;
    }

    private static bool IsPlaceholderAsin(string asin)
    {
        return asin.Equals("0000000000", StringComparison.OrdinalIgnoreCase) || asin.Distinct().Count() <= 2;
    }

    private static string ExtractTitle(string block)
    {
        var titleMatch = Regex.Match(block, "<h2[^>]*>[\\s\\S]*?<span[^>]*>(?<title>[\\s\\S]*?)</span>[\\s\\S]*?</h2>", RegexOptions.IgnoreCase);
        if (titleMatch.Success) return StripTags(titleMatch.Groups["title"].Value);

        var titleSpan = Regex.Match(block, "<span[^>]*class=\"[^\"]*(?:a-size-base-plus|a-size-medium|a-text-normal)[^\"]*\"[^>]*>(?<title>[\\s\\S]*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleSpan.Success) return StripTags(titleSpan.Groups["title"].Value);

        var aria = Regex.Match(block, "aria-label=\"(?<title>[^\"]{15,})\"", RegexOptions.IgnoreCase);
        if (aria.Success && !LooksLikeRatingOrPrice(aria.Groups["title"].Value)) return aria.Groups["title"].Value;

        var imageAlt = Regex.Match(block, "alt=\"(?<title>[^\"]{15,})\"", RegexOptions.IgnoreCase);
        return imageAlt.Success ? imageAlt.Groups["title"].Value : string.Empty;
    }

    private static bool LooksLikeRatingOrPrice(string value)
    {
        return ContainsAny(value, "out of 5", "stars", "$", "ratings", "reviews");
    }

    private static decimal ExtractPrice(string block)
    {
        var offscreenMatches = Regex.Matches(block, "<span[^>]*class=\"[^\"]*a-offscreen[^\"]*\"[^>]*>\\s*\\$(?<price>[0-9,.]+)\\s*</span>", RegexOptions.IgnoreCase);
        foreach (Match match in offscreenMatches)
        {
            var context = GetContext(block, match.Index, 160);
            if (ContainsAny(context, "List Price", "Was:", "Typical price", "coupon", "Subscribe & Save")) continue;
            var price = ParsePrice(match.Groups["price"].Value);
            if (price > 0) return price;
        }

        var whole = Regex.Match(block, "<span[^>]*class=\"[^\"]*a-price-whole[^\"]*\"[^>]*>(?<whole>[0-9,]+)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (whole.Success)
        {
            var fraction = Regex.Match(block, "<span[^>]*class=\"[^\"]*a-price-fraction[^\"]*\"[^>]*>(?<fraction>[0-9]{2})</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var raw = StripTags(whole.Groups["whole"].Value).Replace(",", "").Trim('.');
            if (fraction.Success) raw += "." + StripTags(fraction.Groups["fraction"].Value);
            return ParsePrice(raw);
        }

        return 0m;
    }

    private static decimal ExtractRating(string block)
    {
        var patterns = new[]
        {
            "(?<rating>[0-5](?:\\.[0-9])?)\\s+out of 5 stars",
            "aria-label=\"(?<rating>[0-5](?:\\.[0-9])?)\\s+out of 5 stars\"",
            "(?<rating>[0-5](?:\\.[0-9])?)\\s+stars"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(block, pattern, RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups["rating"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var rating)) return rating;
        }

        return 0m;
    }

    private static int ExtractReviewCount(string block)
    {
        var patterns = new[]
        {
            "href=\"[^\"]*customerReviews[^\"]*\"[^>]*>[\\s\\S]*?<span[^>]*>(?<reviews>[0-9,]+)</span>",
            "href=\"[^\"]*product-reviews[^\"]*\"[^>]*>[\\s\\S]*?(?<reviews>[0-9,]+)",
            "aria-label=\"(?<reviews>[0-9,]+)\\s+(?:ratings|reviews)\""
        };

        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(block, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                var raw = match.Groups["reviews"].Value.Replace(",", "");
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reviews) && reviews > 0) return reviews;
            }
        }

        return 0;
    }

    private static IReadOnlyList<string> ExtractImageUrls(string block)
    {
        var urls = new List<string>();
        var patterns = new[]
        {
            "data-a-dynamic-image=\"(?<json>[^\"]+)\"",
            "src=\"(?<url>https://[^\"]+?\\.(?:jpg|jpeg|png|webp)[^\"]*)\"",
            "data-src=\"(?<url>https://[^\"]+?\\.(?:jpg|jpeg|png|webp)[^\"]*)\"",
            "srcset=\"(?<set>https://[^\"]+)\""
        };

        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(block, pattern, RegexOptions.IgnoreCase))
            {
                if (match.Groups["json"].Success)
                {
                    foreach (Match image in Regex.Matches(match.Groups["json"].Value, "https://[^\\\"']+?\\.(?:jpg|jpeg|png|webp)[^\\\"']*", RegexOptions.IgnoreCase))
                    {
                        AddImageUrl(urls, image.Value);
                    }
                }
                else if (match.Groups["url"].Success)
                {
                    AddImageUrl(urls, match.Groups["url"].Value);
                }
                else if (match.Groups["set"].Success)
                {
                    foreach (var part in match.Groups["set"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        AddImageUrl(urls, part.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty);
                    }
                }

                if (urls.Count >= 6) return urls;
            }
        }

        return urls;
    }

    private static void AddImageUrl(List<string> urls, string rawUrl)
    {
        var url = WebUtility.HtmlDecode(rawUrl).Trim().Trim('\\', '"', '\'', ',', '{', '}');
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.Contains("media-amazon", StringComparison.OrdinalIgnoreCase) && !url.Contains("ssl-images-amazon", StringComparison.OrdinalIgnoreCase)) return;
        url = Regex.Replace(url, @"\._[^.]+_\.", ".");
        if (!urls.Contains(url, StringComparer.OrdinalIgnoreCase)) urls.Add(url);
    }

    private static (string Level, string Notes) AnalyzeVisualCompleteness(int imageCount)
    {
        return imageCount switch
        {
            >= 4 and <= 6 => ("LOW", $"Image set complete: {imageCount}/6"),
            > 0 and < 4 => ("MEDIUM", $"Visual Data Incomplete: only {imageCount}/4 minimum images found"),
            _ => ("HIGH", "No product image found")
        };
    }

    private static string GetImage(IReadOnlyList<string> urls, int index) => index < urls.Count ? urls[index] : string.Empty;

    private static decimal ParsePrice(string raw)
    {
        raw = WebUtility.HtmlDecode(raw).Replace(",", "").Trim();
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : 0m;
    }

    private static string ExtractProductUrl(string block, string asin)
    {
        var match = Regex.Match(block, "href=\"(?<url>/(?:[^\"]*?/)?(?:dp|gp/product)/[A-Z0-9]{10}[^\"]*)\"", RegexOptions.IgnoreCase);
        if (!match.Success) return $"https://www.amazon.com/dp/{asin}";

        var url = WebUtility.HtmlDecode(match.Groups["url"].Value);
        var clean = url.Split('?')[0];
        if (!clean.StartsWith("http", StringComparison.OrdinalIgnoreCase)) clean = "https://www.amazon.com" + clean;
        return clean;
    }

    private static bool HasLowStockText(string block)
    {
        return ContainsAny(block, "Only 1 left in stock", "Only 2 left in stock", "Only 3 left in stock", "Only 4 left in stock", "Only 5 left in stock", "left in stock - order soon", "in stock - order soon");
    }

    private static bool HasUsuallyKeepText(string block)
    {
        return ContainsAny(block, "Customers usually keep this item", "Customer usually keeps this item", "usually keep this item", "usually keeps this item");
    }

    private static bool IsNonProductCard(string title)
    {
        return ContainsAny(title, "Shop by category", "Related searches", "Explore more", "Need help");
    }

    private static string GuessBrand(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        var cleaned = title.Trim();
        var byMatch = Regex.Match(cleaned, "\\bby\\s+(?<brand>[A-Za-z0-9][A-Za-z0-9&' .-]{1,30})", RegexOptions.IgnoreCase);
        if (byMatch.Success) return NormalizeBrand(byMatch.Groups["brand"].Value);
        var firstPart = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return NormalizeBrand(firstPart);
    }

    private static string NormalizeBrand(string value)
    {
        return value.Trim(',', '.', '-', ':', ';', '|', '(', ')', '[', ']', '"', '\'');
    }

    private static string GetContext(string value, int index, int radius)
    {
        var start = Math.Max(0, index - radius);
        var length = Math.Min(value.Length - start, radius * 2);
        return value.Substring(start, length);
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
