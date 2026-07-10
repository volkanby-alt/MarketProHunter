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
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--disable-notifications");
        options.AddArgument("--disable-popup-blocking");
        options.AddArgument("--lang=en-US");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-search-engine-choice-screen");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);

        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
        _driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(30);
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(25));
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
        return await NavigateAndReadAsync(url, waitForSearchResults: true, cancellationToken);
    }

    public async Task<string> FetchProductPageAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productUrl)) return string.Empty;

        await EnsureSessionAsync(cancellationToken);
        var url = productUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? productUrl
            : $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/{productUrl.TrimStart('/')}";

        return await NavigateAndReadAsync(url, waitForSearchResults: false, cancellationToken);
    }

    private async Task EnsureSessionAsync(CancellationToken cancellationToken)
    {
        if (_sessionInitialized) return;

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _driver.Navigate().GoToUrl($"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/?language=en_US&currency=USD");
            WaitForDocumentReady();

            AddOrReplaceCookie("lc-main", "en_US");
            AddOrReplaceCookie("i18n-prefs", "USD");
            AddOrReplaceCookie("zip-main", string.IsNullOrWhiteSpace(_settings.ZipCode) ? "07073" : _settings.ZipCode.Trim());

            _sessionInitialized = true;
        }, cancellationToken);
    }

    private async Task<string> NavigateAndReadAsync(string url, bool waitForSearchResults, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _driver.Navigate().GoToUrl(url);
            WaitForDocumentReady();

            if (waitForSearchResults)
            {
                try
                {
                    _wait.Until(driver =>
                        driver.FindElements(By.CssSelector("div[data-component-type='s-search-result']")).Count > 0
                        || LooksBlocked(driver.PageSource));
                }
                catch (WebDriverTimeoutException)
                {
                    // The caller will inspect the returned HTML and report that no cards were found.
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var html = _driver.PageSource;
            if (LooksBlocked(html))
            {
                throw new InvalidOperationException("Amazon robot kontrolü gösterdi. Açılan Chrome penceresinde kontrolü tamamlayıp taramayı yeniden başlatın.");
            }

            return html;
        }, cancellationToken);
    }

    private void WaitForDocumentReady()
    {
        _wait.Until(driver =>
        {
            try
            {
                return ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState")
                    ?.ToString()
                    ?.Equals("complete", StringComparison.OrdinalIgnoreCase) == true;
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
            // Locale cookies are helpful but not required for the scan to continue.
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
