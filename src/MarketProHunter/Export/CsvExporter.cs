using System.Globalization;
using System.Text;
using MarketProHunter.Models;

namespace MarketProHunter.Export;

public sealed class CsvExporter
{
    public async Task WriteAsync(string path, IEnumerable<ProductResult> products, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("ASIN,Title,Brand,Price,Currency,AmazonChoice,Sponsored,ProductUrl,Keyword,Page,Notes");

        foreach (var p in products)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Escape(p.Asin),
                Escape(p.Title),
                Escape(p.Brand),
                p.Price.ToString(CultureInfo.InvariantCulture),
                Escape(p.Currency),
                p.IsAmazonChoice.ToString(),
                p.IsSponsored.ToString(),
                Escape(p.ProductUrl),
                Escape(p.SearchKeyword),
                p.Page.ToString(CultureInfo.InvariantCulture),
                Escape(p.Notes)
            }));
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, cancellationToken);
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
