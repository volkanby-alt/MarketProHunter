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

        var profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketProHunter",
            "ChromeProfile");
        Directory.CreateDirectory(profilePath);

        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        options.AddArgument("--disable-notifications");
        options.AddArgument("--disable-popup-blocking");
        options.AddArgument("--lang=en-US");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-search-engine-choice-screen");
        options.AddArgument($"--user-data-dir={profilePath}");
        options.AddArgument("--profile-directory=Default");

        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        _driver.Navigate().GoToUrl($"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/?language=en_US&currency=USD");
        WaitForDocumentReady();
    }

    public string CurrentUrl => _driver.Url;

    public string BuildSearchUrl(string keyword, int page)
    {
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var min = Math.Max(1, (int)(_settings.MinPrice * 100));
        var max = Math.Max(min, (int)(_settings.MaxPrice * 100));
        return $"{_settings.MarketplaceBaseUrl.TrimEnd('/')}/s?k={encodedKeyword}&page={page}&rh=p_36%3A{min}-{max}&language=en_US&currency=USD";
    }

    public async Task<string> FetchSearchPageAsync(string keyword, int page, CancellationToken cancellationToken = default)
    {
        var url = BuildSearchUrl(keyword, page);
        return await FetchUrlPageAsync(url, keyword, page, cancellationToken);
    }

    public async Task<string> FetchUrlPageAsync(
        string url,
        string displayName,
        int page,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSession();

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Amazon URL boş olamaz.", nameof(url));
        }

        _driver.Navigate().GoToUrl(url);
        WaitForDocumentReady();

        try
        {
            _wait.Until(driver =>
                driver.FindElements(By.CssSelector("div[data-component-type='s-search-result']")).Count > 0
                || driver.FindElements(By.CssSelector("div[data-asin]")).Count > 0
                || LooksBlocked(driver.PageSource));
        }
        catch (WebDriverTimeoutException)
        {
            // The caller inspects the returned HTML and decides whether to continue.
        }

        cancellationToken.ThrowIfCancellationRequested();
        TrySetStatusTitle(displayName, page);

        var html = _driver.PageSource;
        ThrowIfBlocked(html);

        if (_settings.DelayBetweenPagesMs > 0)
        {
            await Task.Delay(_settings.DelayBetweenPagesMs, cancellationToken);
        }

        return html;
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
        _driver.Navigate().Refresh();
        WaitForDocumentReady();

        if (!IsDeliveryZipAlreadySet())
        {
            SetDeliveryZipCode();
        }

        _sessionInitialized = true;
    }

    private bool IsDeliveryZipAlreadySet()
    {
        var zip = string.IsNullOrWhiteSpace(_settings.ZipCode) ? "07073" : _settings.ZipCode.Trim();
        var locationText = ReadLocationText(_driver);
        return locationText.Contains(zip, StringComparison.OrdinalIgnoreCase);
    }

    private void SetDeliveryZipCode()
    {
        var zip = string.IsNullOrWhiteSpace(_settings.ZipCode) ? "07073" : _settings.ZipCode.Trim();

        try
        {
            var locationButton = _wait.Until(driver =>
                FirstDisplayed(driver,
                    By.Id("nav-global-location-popover-link"),
                    By.Id("nav-global-location-data-modal-action"),
                    By.CssSelector("a[data-csa-c-content-id='nav-global-location']")));

            if (locationButton is null)
            {
                throw new WebDriverTimeoutException("Teslimat konumu düğmesi bulunamadı.");
            }

            ClickElement(locationButton);

            var zipInput = _wait.Until(driver =>
                FirstDisplayed(driver,
                    By.Id("GLUXZipUpdateInput"),
                    By.CssSelector("input[data-action='GLUXPostalInputAction']"),
                    By.CssSelector("input[placeholder*='ZIP']")));

            if (zipInput is null)
            {
                throw new WebDriverTimeoutException("ZIP giriş alanı bulunamadı.");
            }

            zipInput.Clear();
            zipInput.SendKeys(zip);

            var applyButton = _wait.Until(driver =>
                FirstDisplayed(driver,
                    By.CssSelector("#GLUXZipUpdate input.a-button-input"),
                    By.Id("GLUXZipUpdate"),
                    By.CssSelector("span[data-action='GLUXPostalUpdateAction'] input")));

            if (applyButton is null)
            {
                throw new WebDriverTimeoutException("ZIP uygulama düğmesi bulunamadı.");
            }

            ClickElement(applyButton);

            try
            {
                var closeButton = new WebDriverWait(_driver, TimeSpan.FromSeconds(8)).Until(driver =>
                    FirstDisplayed(driver,
                        By.CssSelector("#GLUXConfirmClose input.a-button-input"),
                        By.Id("GLUXConfirmClose"),
                        By.CssSelector("button[name='glowDoneButton']")));
                if (closeButton is not null) ClickElement(closeButton);
            }
            catch (WebDriverTimeoutException)
            {
                // Some Amazon layouts close the modal automatically.
            }

            _driver.Navigate().Refresh();
            WaitForDocumentReady();
        }
        catch (WebDriverException ex)
        {
            throw new InvalidOperationException(
                $"Amazon teslimat adresi {zip} olarak ayarlanamadı. Açılan Chrome penceresinde 'Deliver to' bölümünü kontrol edin. Ayrıntı: {ex.Message}",
                ex);
        }
    }

    private void TrySetStatusTitle(string keyword, int page)
    {
        try
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript(
                "document.title = arguments[0];",
                $"MarketProHunter | {keyword} | Sayfa {page}");
        }
        catch (WebDriverException)
        {
            // The address bar still shows the exact search URL.
        }
    }

    private static IWebElement? FirstDisplayed(IWebDriver driver, params By[] selectors)
    {
        foreach (var selector in selectors)
        {
            var element = driver.FindElements(selector).FirstOrDefault(x => x.Displayed && x.Enabled);
            if (element is not null) return element;
        }

        return null;
    }

    private void ClickElement(IWebElement element)
    {
        try
        {
            element.Click();
        }
        catch (WebDriverException)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", element);
        }
    }

    private static string ReadLocationText(IWebDriver driver)
    {
        var selectors = new[]
        {
            By.Id("glow-ingress-line1"),
            By.Id("glow-ingress-line2"),
            By.Id("nav-global-location-data-modal-action")
        };

        return string.Join(" ", selectors
            .SelectMany(driver.FindElements)
            .Where(x => x.Displayed)
            .Select(x => x.Text));
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
            // Locale cookies are optional.
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
