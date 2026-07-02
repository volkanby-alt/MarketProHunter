using MarketProHunter.Amazon;
using MarketProHunter.Export;
using MarketProHunter.Filters;
using MarketProHunter.Models;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var settings = SearchSettings.Default;

Console.WriteLine("MarketProHunter - Amazon Search Engine v1");
Console.WriteLine($"ZIP: {settings.ZipCode} | Price: ${settings.MinPrice}-${settings.MaxPrice}");
Console.WriteLine();

Console.Write("Arama kelimesi yazın (örnek: home cleaner): ");
var keyword = Console.ReadLine();

if (string.IsNullOrWhiteSpace(keyword))
{
    Console.WriteLine("Arama kelimesi boş olamaz.");
    return;
}

Console.Write("Kaç sayfa taransın? Varsayılan 3: ");
var pageInput = Console.ReadLine();
var maxPages = int.TryParse(pageInput, out var parsedPages) && parsedPages > 0 ? parsedPages : 3;

var veroFilter = new VeroBrandFilter("config/vero-brands.txt");
var productFilter = new ProductFilter(settings, veroFilter);
var client = new AmazonSearchClient(settings);
var exporter = new CsvExporter();

var accepted = new List<ProductResult>();

for (var page = 1; page <= maxPages; page++)
{
    Console.WriteLine($"Sayfa taranıyor: {page}/{maxPages}");

    var html = await client.FetchSearchPageAsync(keyword, page);
    var parser = new AmazonSearchParser();
    var products = parser.Parse(html, keyword, page);

    foreach (var product in products)
    {
        var decision = productFilter.Evaluate(product);
        if (decision.Accepted)
        {
            accepted.Add(product with { Notes = decision.Reason });
            Console.WriteLine($"OK  {product.Asin} | {product.Price:C} | {product.Title}");
        }
        else
        {
            Console.WriteLine($"SKIP {product.Asin} | {decision.Reason}");
        }
    }

    await Task.Delay(settings.DelayBetweenPagesMs);
}

var outputPath = Path.Combine("output", $"amazon_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
await exporter.WriteAsync(outputPath, accepted);

Console.WriteLine();
Console.WriteLine($"Bitti. Kabul edilen ürün: {accepted.Count}");
Console.WriteLine($"Dosya: {Path.GetFullPath(outputPath)}");
