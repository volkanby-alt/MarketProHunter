using MarketProHunter.Models;

namespace MarketProHunter.Amazon;

public sealed class AmazonSearchClient
{
    private readonly SearchSettings _settings;
    private readonly HttpClient _httpClient;

    public AmazonSearchClient(SearchSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
    }

    public async Task<string> FetchSearchPageAsync(string keyword, int page, CancellationToken cancellationToken = default)
    {
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var url = $"{_settings.MarketplaceBaseUrl}/s?k={encodedKeyword}&page={page}&rh=p_36%3A{(int)(_settings.MinPrice * 100)}-{(int)(_settings.MaxPrice * 100)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Cookie", $"lc-main=en_US; ubid-main=000-0000000-0000000; i18n-prefs=USD; skin=noskin; session-id=000-0000000-0000000; session-id-time=2082787201l; x-main=; at-main=; sess-at-main=; sst-main=; session-token=; csm-hit=tb:s-000000000000000000000|0&t:0&av-time=0; zip-main={_settings.ZipCode}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
