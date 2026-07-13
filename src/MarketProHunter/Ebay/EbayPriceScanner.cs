using System.Globalization;
using System.Text.RegularExpressions;
using MarketProHunter.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace MarketProHunter.Ebay;

public sealed class EbayPriceScanner : IDisposable
{
    private readonly ChromeDriver _driver;
    private readonly WebDriverWait _wait;

    public EbayPriceScanner()
    {
        var profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketProHunter",
            "EbayChromeProfile");
        Directory.CreateDirectory(profilePath);

        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        options.AddArgument("--disable-notifications");
        options.AddArgument("--disable-popup-blocking");
        options.AddArgument("--lang=en-US");
        options.AddArgument($"--user-data-dir={profilePath}");
        options.AddArgument("--profile-directory=Default");

        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(25));
    }

    public async Task<EbayPriceResult> ScanAsync(
        ProductResult product,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = BuildSearchQuery(product);
        var searchUrl = $"https://www.ebay.com/sch/i.html?_nkw={Uri.EscapeDataString(query)}&LH_BIN=1";

        try
        {
            _driver.Navigate().GoToUrl(searchUrl);
            WaitForDocumentReady();

            try
            {
                _wait.Until(driver =>
                    driver.FindElements(By.CssSelector("li.s-item")).Count > 0
                    || driver.PageSource.Contains("0 results", StringComparison.OrdinalIgnoreCase));
            }
            catch (WebDriverTimeoutException)
            {
                // The parsed result below will decide whether anything useful exists.
            }

            cancellationToken.ThrowIfCancellationRequested();

            var prices = new List<decimal>();
            var productTokens = Tokenize(product.Title);

            foreach (var card in _driver.FindElements(By.CssSelector("li.s-item")))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var title = ReadText(card, ".s-item__title");
                if (string.IsNullOrWhiteSpace(title) || !LooksLikeSameProduct(productTokens, title)) continue;

                var priceText = ReadText(card, ".s-item__price");
                foreach (var price in ParseUsdPrices(priceText))
                {
                    if (price > 0) prices.Add(price);
                }
            }

            await Task.Delay(800, cancellationToken);

            if (prices.Count == 0)
            {
                return new EbayPriceResult(false, null, null, searchUrl);
            }

            return new EbayPriceResult(true, prices.Min(), prices.Max(), searchUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EbayPriceResult(false, null, null, searchUrl, ex.Message);
        }
    }

    private static string BuildSearchQuery(ProductResult product)
    {
        var title = product.Title?.Trim() ?? string.Empty;
        if (title.Length > 120) title = title[..120];
        return string.IsNullOrWhiteSpace(product.Brand)
            ? title
            : $"{product.Brand} {title}";
    }

    private static string ReadText(IWebElement parent, string selector)
    {
        try
        {
            return parent.FindElements(By.CssSelector(selector)).FirstOrDefault()?.Text?.Trim() ?? string.Empty;
        }
        catch (StaleElementReferenceException)
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

        return Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]{3,}")
            .Select(match => match.Value)
            .Where(token => token is not "with" and not "from" and not "this" and not "that" and not "pack")
            .Distinct()
            .Take(12)
            .ToList();
    }

    private static bool LooksLikeSameProduct(IReadOnlyList<string> productTokens, string ebayTitle)
    {
        if (productTokens.Count == 0) return false;

        var ebayTokens = Tokenize(ebayTitle).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matched = productTokens.Count(ebayTokens.Contains);
        var required = productTokens.Count <= 4 ? 2 : Math.Max(3, (int)Math.Ceiling(productTokens.Count * 0.45));
        return matched >= required;
    }

    private static IEnumerable<decimal> ParseUsdPrices(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        foreach (Match match in Regex.Matches(text, @"\$\s*([0-9,]+(?:\.[0-9]{2})?)"))
        {
            var normalized = match.Groups[1].Value.Replace(",", string.Empty);
            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                yield return value;
            }
        }
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

    public void Dispose()
    {
        try
        {
            _driver.Quit();
        }
        catch
        {
            // Ignore browser shutdown errors.
        }

        _driver.Dispose();
    }
}
