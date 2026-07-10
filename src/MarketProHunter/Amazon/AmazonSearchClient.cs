using System.Net;
using MarketProHunter.Models;

namespace MarketProHunter.Amazon;

public sealed class AmazonSearchClient : IDisposable
{
    private readonly SearchSettings _settings;
    private readonly HttpClient _httpClient;
    private bool _sessionInitialized;

    public AmazonSearchClient(SearchSettings settings)
    {
        _settings = settings;
        var cookies = new CookieContainer();
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            CookieContainer = cookies,
            UseCookies = true,
            AllowAutoRedirect = true
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
    }

    public string BuildSearchUrl(string keyword, int page)
    {
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var min = Math.Max(1, (int)(_settings.MinPrice * 100));
        var max = Math.Max(min, (int)(_settings.MaxPrice * 100));
        return $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/s?k={encodedKeyword}&page={page}&rh=p_36%3A{min}-{max}&language=en_US&currency=USD";
    }

    public async Task<string> FetchSearchPageAsync(string keyword, int page, CancellationToken cancellationToken = default)
    {
        await EnsureSessionAsync(cancellationToken);
        var url = BuildSearchUrl(keyword, page);
        return await FetchDocumentAsync(url, _settings.MarketplaceBaseUrl, cancellationToken);
    }

    public async Task<string> FetchProductPageAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productUrl)) return string.Empty;

        await EnsureSessionAsync(cancellationToken);
        var url = productUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? productUrl
            : $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/{productUrl.TrimStart('/')}";

        return await FetchDocumentAsync(url, _settings.MarketplaceBaseUrl, cancellationToken);
    }

    private async Task EnsureSessionAsync(CancellationToken cancellationToken)
    {
        if (_sessionInitialized) return;

        var homeUrl = $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/?language=en_US&currency=USD";
        using var request = new HttpRequestMessage(HttpMethod.Get, homeUrl);
        request.Headers.Referrer = new Uri(_settings.MarketplaceBaseUrl);
        request.Headers.TryAddWithoutValidation("Cookie", BuildLocaleCookie());

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        _sessionInitialized = true;
    }

    private async Task<string> FetchDocumentAsync(string url, string referrer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri(referrer);
        request.Headers.TryAddWithoutValidation("Cookie", BuildLocaleCookie());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Amazon HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        if (LooksBlocked(html))
        {
            throw new InvalidOperationException("Amazon robot kontrolü veya koruma sayfası gösterdi.");
        }

        return html;
    }

    private string BuildLocaleCookie()
    {
        var zip = string.IsNullOrWhiteSpace(_settings.ZipCode) ? "07073" : _settings.ZipCode.Trim();
        return string.Join("; ",
            "lc-main=en_US",
            "i18n-prefs=USD",
            $"zip-main={zip}");
    }

    private static bool LooksBlocked(string html)
    {
        return html.Contains("Enter the characters you see below", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Sorry, we just need to make sure you're not a robot", StringComparison.OrdinalIgnoreCase)
            || html.Contains("automated access to Amazon data", StringComparison.OrdinalIgnoreCase)
            || html.Contains("api-services-support@amazon.com", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _httpClient.Dispose();
}
