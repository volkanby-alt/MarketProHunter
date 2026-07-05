using System.Net;
using System.Text.RegularExpressions;

namespace MarketProHunter.Amazon;

public sealed partial class AmazonProductPageParser
{
    public AmazonProductPageData Parse(string html, string asin)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Empty(asin);
        }

        var decoded = WebUtility.HtmlDecode(html);
        var bullets = ExtractBulletPoints(decoded);
        var description = ExtractDescription(decoded);
        var specs = ExtractSpecifications(decoded);
        var hasAPlus = HasAPlusContent(decoded);

        return new AmazonProductPageData(asin, bullets, description, specs, hasAPlus);
    }

    private static AmazonProductPageData Empty(string asin) => new(asin, Array.Empty<string>(), string.Empty, new Dictionary<string, string>(), false);

    private static IReadOnlyList<string> ExtractBulletPoints(string html)
    {
        var bullets = new List<string>();
        var featureBlock = MatchFirst(html,
            "<div[^>]+id=\"feature-bullets\"[\\s\\S]*?</div>\\s*</div>",
            "<ul[^>]+class=\"[^\"]*a-unordered-list[^\"]*a-vertical[^\"]*[^\"]*\"[\\s\\S]*?</ul>");

        foreach (Match match in Regex.Matches(featureBlock, "<li[^>]*>[\\s\\S]*?<span[^>]*>(?<text>[\\s\\S]*?)</span>[\\s\\S]*?</li>", RegexOptions.IgnoreCase))
        {
            var text = Clean(match.Groups["text"].Value);
            if (IsUsefulText(text) && !bullets.Contains(text, StringComparer.OrdinalIgnoreCase)) bullets.Add(text);
            if (bullets.Count >= 8) break;
        }

        return bullets;
    }

    private static string ExtractDescription(string html)
    {
        var candidates = new[]
        {
            "<div[^>]+id=\"productDescription\"[^>]*>(?<text>[\\s\\S]*?)</div>",
            "<div[^>]+id=\"aplus\"[^>]*>(?<text>[\\s\\S]*?)</div>",
            "<div[^>]+id=\"dpx-product-description_feature_div\"[^>]*>(?<text>[\\s\\S]*?)</div>"
        };

        foreach (var pattern in candidates)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success) continue;
            var text = Clean(match.Groups["text"].Value);
            if (text.Length >= 30) return Shorten(text, 2000);
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string> ExtractSpecifications(string html)
    {
        var specs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ExtractSpecRows(html, specs, "productDetails_techSpec_section_1");
        ExtractSpecRows(html, specs, "productDetails_detailBullets_sections1");
        ExtractDetailBullets(html, specs);
        return specs;
    }

    private static void ExtractSpecRows(string html, Dictionary<string, string> specs, string tableId)
    {
        var table = MatchFirst(html, $"<table[^>]+id=\"{Regex.Escape(tableId)}\"[^>]*>[\\s\\S]*?</table>");
        foreach (Match row in Regex.Matches(table, "<tr[^>]*>[\\s\\S]*?<th[^>]*>(?<key>[\\s\\S]*?)</th>[\\s\\S]*?<td[^>]*>(?<value>[\\s\\S]*?)</td>[\\s\\S]*?</tr>", RegexOptions.IgnoreCase))
        {
            AddSpec(specs, row.Groups["key"].Value, row.Groups["value"].Value);
        }
    }

    private static void ExtractDetailBullets(string html, Dictionary<string, string> specs)
    {
        var block = MatchFirst(html, "<div[^>]+id=\"detailBullets_feature_div\"[^>]*>[\\s\\S]*?</div>\\s*</div>");
        foreach (Match item in Regex.Matches(block, "<span[^>]*class=\"[^\"]*a-list-item[^\"]*\"[^>]*>(?<text>[\\s\\S]*?)</span>", RegexOptions.IgnoreCase))
        {
            var text = Clean(item.Groups["text"].Value);
            var parts = text.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2) AddSpec(specs, parts[0], parts[1]);
        }
    }

    private static void AddSpec(Dictionary<string, string> specs, string rawKey, string rawValue)
    {
        var key = Clean(rawKey).Trim(':');
        var value = Clean(rawValue);
        if (!IsUsefulText(key) || !IsUsefulText(value)) return;
        if (key.Length > 80 || value.Length > 300) return;
        specs.TryAdd(key, value);
    }

    private static bool HasAPlusContent(string html)
    {
        return html.Contains("aplus", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("aplus-v2", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("premium-aplus", StringComparison.OrdinalIgnoreCase);
    }

    private static string MatchFirst(string html, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success) return match.Value;
        }

        return string.Empty;
    }

    private static bool IsUsefulText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Equals("Make sure this fits", StringComparison.OrdinalIgnoreCase)) return false;
        if (value.Contains("javascript", StringComparison.OrdinalIgnoreCase)) return false;
        return value.Length >= 2;
    }

    private static string Clean(string value)
    {
        var noTags = StripTags(value);
        return Regex.Replace(WebUtility.HtmlDecode(noTags), "\\s+", " ").Trim();
    }

    private static string StripTags(string value) => Regex.Replace(value, "<.*?>", string.Empty, RegexOptions.Singleline);

    private static string Shorten(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
