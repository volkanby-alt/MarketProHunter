using System.Net;
using MarketProHunter.Models;

namespace MarketProHunter.Amazon;

public sealed class AmazonSearchClient : IDisposable
{
    private readonly SearchSettings _settings;
    private readonly HttpClient _httpClient;

    public AmazonSearchClient(SearchSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        });

        _httpClient.Timeout = TimeSpan.FromSeconds(35);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "none");
    }

    public async Task<string> FetchSearchPageAsync(string keyword, int page, CancellationToken cancellationToken = default)
    {
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var min = Math.Max(1, (int)(_settings.MinPrice * 100));
        var max = Math.Max(min, (int)(_settings.MaxPrice * 100));
        var url = $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/s?k={encodedKeyword}&page={page}&rh=p_36%3A{min}-{max}&language=en_US&currency=USD";

        return await FetchDocumentAsync(url, _settings.MarketplaceBaseUrl, cancellationToken);
    }

    public async Task<string> FetchProductPageAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productUrl)) return string.Empty;

        var url = productUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? productUrl
            : $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/{productUrl.TrimStart('/')}";

        return await FetchDocumentAsync(url, _settings.MarketplaceBaseUrl, cancellationToken);
    }

    private async Task<string> FetchDocumentAsync(string url, string referrer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri(referrer);
        request.Headers.TryAddWithoutValidation("Cookie", BuildCookie());

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Amazon HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        if (html.Contains("Enter the characters you see below", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("Sorry, we just need to make sure you're not a robot", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Amazon robot kontrolü gösterdi. Tarama durdu; tarayıcı/cookie destekli motor gerekecek.");
        }

        return html;
    }

    private string BuildCookie()
    {
        var zip = string.IsNullOrWhiteSpace(_settings.ZipCode) ? "07073" : _settings.ZipCode.Trim();
        return string.Join("; ",
            "lc-main=en_US",
            "i18n-prefs=USD",
            "skin=noskin",
            $"zip-main={zip}",
            "ubid-main=000-0000000-0000000",
            "session-id=000-0000000-0000000",
            "session-id-time=2082787201l");
    }

    public void Dispose() => _httpClient.Dispose();
}
