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
            ClickFirstDisplayedWithRetry(
                TimeSpan.FromSeconds(30),
                By.Id("nav-global-location-popover-link"),
                By.Id("nav-global-location-data-modal-action"),
                By.CssSelector("a[data-csa-c-content-id='nav-global-location']"));

            SetZipInputWithRetry(zip);

            ClickFirstDisplayedWithRetry(
                TimeSpan.FromSeconds(30),
                By.CssSelector("#GLUXZipUpdate input.a-button-input"),
                By.Id("GLUXZipUpdate"),
                By.CssSelector("span[data-action='GLUXPostalUpdateAction'] input"));

            TryCloseLocationDialog();

            new WebDriverWait(_driver, TimeSpan.FromSeconds(12)).Until(driver =>
            {
                try
                {
                    var text = ReadLocationText(driver);
                    return text.Contains(zip, StringComparison.OrdinalIgnoreCase)
                        || driver.FindElements(By.Id("GLUXZipUpdateInput")).All(x => !x.Displayed);
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
            });

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

    private void SetZipInputWithRetry(string zip)
    {
        var selectors = new[]
        {
            By.Id("GLUXZipUpdateInput"),
            By.CssSelector("input[data-action='GLUXPostalInputAction']"),
            By.CssSelector("input[placeholder*='ZIP']")
        };

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(ElementNotInteractableException));
        wait.Until(driver =>
        {
            var input = FirstDisplayed(driver, selectors);
            if (input is null) return false;

            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript(
                    "arguments[0].focus(); arguments[0].value=''; arguments[0].dispatchEvent(new Event('input',{bubbles:true}));",
                    input);
                input.SendKeys(zip);
                return string.Equals(input.GetAttribute("value"), zip, StringComparison.OrdinalIgnoreCase);
            }
            catch (StaleElementReferenceException)
            {
                return false;
            }
        });
    }

    private void ClickFirstDisplayedWithRetry(TimeSpan timeout, params By[] selectors)
    {
        var wait = new WebDriverWait(_driver, timeout);
        wait.IgnoreExceptionTypes(
            typeof(StaleElementReferenceException),
            typeof(ElementClickInterceptedException),
            typeof(ElementNotInteractableException));

        var clicked = wait.Until(driver =>
        {
            var element = FirstDisplayed(driver, selectors);
            if (element is null) return false;

            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", element);
                element.Click();
                return true;
            }
            catch (StaleElementReferenceException)
            {
                return false;
            }
            catch (WebDriverException)
            {
                try
                {
                    element = FirstDisplayed(driver, selectors);
                    if (element is null) return false;
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", element);
                    return true;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
            }
        });

        if (!clicked)
        {
            throw new WebDriverTimeoutException("Amazon konum penceresindeki gerekli düğme bulunamadı.");
        }
    }

    private void TryCloseLocationDialog()
    {
        try
        {
            ClickFirstDisplayedWithRetry(
                TimeSpan.FromSeconds(8),
                By.CssSelector("#GLUXConfirmClose input.a-button-input"),
                By.Id("GLUXConfirmClose"),
                By.CssSelector("button[name='glowDoneButton']"));
        }
        catch (WebDriverTimeoutException)
        {
            // Some Amazon layouts close automatically.
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
            try
            {
                foreach (var element in driver.FindElements(selector))
                {
                    try
                    {
                        if (element.Displayed && element.Enabled) return element;
                    }
                    catch (StaleElementReferenceException)
                    {
                        // Amazon rerendered the element; continue with a fresh lookup.
                    }
                }
            }
            catch (StaleElementReferenceException)
            {
                // Continue with the next selector.
            }
        }

        return null;
    }

    private static string ReadLocationText(IWebDriver driver)
    {
        var selectors = new[]
        {
            By.Id("glow-ingress-line1"),
            By.Id("glow-ingress-line2"),
            By.Id("nav-global-location-data-modal-action")
        };

        var values = new List<string>();
        foreach (var selector in selectors)
        {
            foreach (var element in driver.FindElements(selector))
            {
                try
                {
                    if (element.Displayed) values.Add(element.Text);
                }
                catch (StaleElementReferenceException)
                {
                    // Ignore elements replaced during an Amazon header refresh.
                }
            }
        }

        return string.Join(" ", values);
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
