using MarketProHunter.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace MarketProHunter.Amazon;

public sealed class AmazonSearchClient : IDisposable
{
    private readonly SearchSettings _settings;
    private readonly ChromeDriver _driver;
    private readonly WebDriverWait _wait;
    private bool _sessionInitialized;

    public AmazonSearchClient(SearchSettings settings)
    {
        _settings = settings;

        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        options.AddArgument("--disable-notifications");
        options.AddArgument("--disable-popup-blocking");
        options.AddArgument("--lang=en-US");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-search-engine-choice-screen");

        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        // ChromeDriver first opens data:,. Navigate immediately so the user can
        // verify that the real Amazon session has started.
        _driver.Navigate().GoToUrl($"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/?language=en_US&currency=USD");
        WaitForDocumentReady();
    }

    public string BuildSearchUrl(string keyword, int page)
    {
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var min = Math.Max(1, (int)(_settings.MinPrice * 100));
        var max = Math.Max(min, (int)(_settings.MaxPrice * 100));
        return $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/s?k={encodedKeyword}&page={page}&rh=p_36%3A{min}-{max}&language=en_US&currency=USD";
    }

    public Task<string> FetchSearchPageAsync(string keyword, int page, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSession();

        var url = BuildSearchUrl(keyword, page);
        _driver.Navigate().GoToUrl(url);
        WaitForDocumentReady();

        try
        {
            _wait.Until(driver =>
                driver.FindElements(By.CssSelector("div[data-component-type='s-search-result']")).Count > 0
                || LooksBlocked(driver.PageSource));
        }
        catch (WebDriverTimeoutException)
        {
            // Return the page source so the caller can log the failure reason.
        }

        cancellationToken.ThrowIfCancellationRequested();
        var html = _driver.PageSource;
        ThrowIfBlocked(html);
        return Task.FromResult(html);
    }

    public Task<string> FetchProductPageAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(productUrl)) return Task.FromResult(string.Empty);

        EnsureSession();
        var url = productUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? productUrl
            : $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/{productUrl.TrimStart('/')}";

        _driver.Navigate().GoToUrl(url);
        WaitForDocumentReady();

        cancellationToken.ThrowIfCancellationRequested();
        var html = _driver.PageSource;
        ThrowIfBlocked(html);
        return Task.FromResult(html);
    }

    private void EnsureSession()
    {
        if (_sessionInitialized) return;

        AddOrReplaceCookie("lc-main", "en_US");
        AddOrReplaceCookie("i18n-prefs", "USD");
        AddOrReplaceCookie("zip-main", string.IsNullOrWhiteSpace(_settings.ZipCode) ? "07073" : _settings.ZipCode.Trim());
        _driver.Navigate().Refresh();
        WaitForDocumentReady();
        _sessionInitialized = true;
    }

    private void WaitForDocumentReady()
    {
        _wait.Until(driver =>
        {
            try
            {
                return string.Equals(
                    ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState")?.ToString(),
                    "complete",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (WebDriverException)
            {
                return false;
            }
        });
    }

    private void AddOrReplaceCookie(string name, string value)
    {
        try
        {
            _driver.Manage().Cookies.DeleteCookieNamed(name);
            _driver.Manage().Cookies.AddCookie(new Cookie(name, value, ".amazon.com", "/", null));
        }
        catch (WebDriverException)
        {
            // Locale cookies are optional. Continue with Amazon defaults.
        }
    }

    private static void ThrowIfBlocked(string html)
    {
        if (LooksBlocked(html))
        {
            throw new InvalidOperationException("Amazon robot kontrolü gösterdi. Açılan Chrome penceresinde kontrolü tamamlayıp taramayı yeniden başlatın.");
        }
    }

    private static bool LooksBlocked(string html)
    {
        return html.Contains("Enter the characters you see below", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Sorry, we just need to make sure you're not a robot", StringComparison.OrdinalIgnoreCase)
            || html.Contains("automated access to Amazon data", StringComparison.OrdinalIgnoreCase)
            || html.Contains("api-services-support@amazon.com", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            _driver.Quit();
        }
        catch
        {
            // Ignore shutdown errors.
        }

        _driver.Dispose();
    }
}
