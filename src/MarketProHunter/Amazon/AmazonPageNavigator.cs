using System.Text.RegularExpressions;

namespace MarketProHunter.Amazon;

public sealed class AmazonPageNavigator
{
    private string? _lastSignature;
    private int _consecutiveEmptyPages;

    public bool ShouldContinue(string html, int productCount, bool scanAllPages, int currentPage, int maxPages, out string reason)
    {
        if (!scanAllPages && currentPage >= maxPages)
        {
            reason = "Seçilen sayfa sınırına ulaşıldı.";
            return false;
        }

        if (productCount == 0)
        {
            _consecutiveEmptyPages++;
            if (_consecutiveEmptyPages >= 2)
            {
                reason = "Art arda iki boş sayfa geldi.";
                return false;
            }
        }
        else
        {
            _consecutiveEmptyPages = 0;
        }

        var signature = BuildPageSignature(html);
        if (!string.IsNullOrWhiteSpace(signature) && signature.Equals(_lastSignature, StringComparison.Ordinal))
        {
            reason = "Amazon aynı sonuç sayfasını tekrar döndürdü.";
            return false;
        }
        _lastSignature = signature;

        if (scanAllPages && !HasNextPage(html))
        {
            reason = "Amazon'da sonraki sayfa bulunamadı.";
            return false;
        }

        if (scanAllPages && currentPage >= 500)
        {
            reason = "Güvenlik sınırı olan 500 sayfaya ulaşıldı.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static bool HasNextPage(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return false;

        return html.Contains("s-pagination-next", StringComparison.OrdinalIgnoreCase)
            && !Regex.IsMatch(
                html,
                "class=\"[^\"]*s-pagination-next[^\"]*s-pagination-disabled[^\"]*\"",
                RegexOptions.IgnoreCase);
    }

    private static string BuildPageSignature(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var asins = Regex.Matches(html, "data-asin=\"([A-Z0-9]{10})\"", RegexOptions.IgnoreCase)
            .Select(x => x.Groups[1].Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12);

        return string.Join("|", asins);
    }
}
