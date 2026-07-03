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
        builder.AppendLine("UploadScore,UploadDecision,CompetitionScore,ConfidenceScore,OverallScore,SafetyScore,SalesScore,ProfitScore,Recommendation,Stars,Rating,ReviewCount,AmazonCost,RecommendedSalePrice,EbayFee,PromotedFee,NetProfit,NetMarginPercent,ProfitDecision,ASIN,Title,Brand,Price,Currency,AmazonChoice,Sponsored,LowStock,UsuallyKeep,ProductUrl,Keyword,Page,Notes");

        foreach (var p in products)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                p.UploadScore.ToString(CultureInfo.InvariantCulture),
                Escape(p.UploadDecision),
                p.CompetitionScore.ToString(CultureInfo.InvariantCulture),
                p.ConfidenceScore.ToString(CultureInfo.InvariantCulture),
                p.OverallScore.ToString(CultureInfo.InvariantCulture),
                p.SafetyScore.ToString(CultureInfo.InvariantCulture),
                p.SalesScore.ToString(CultureInfo.InvariantCulture),
                p.ProfitScore.ToString(CultureInfo.InvariantCulture),
                Escape(p.Recommendation),
                Escape(p.Stars),
                p.Rating.ToString(CultureInfo.InvariantCulture),
                p.ReviewCount.ToString(CultureInfo.InvariantCulture),
                p.Price.ToString(CultureInfo.InvariantCulture),
                p.RecommendedSalePrice.ToString(CultureInfo.InvariantCulture),
                p.EbayFee.ToString(CultureInfo.InvariantCulture),
                p.PromotedFee.ToString(CultureInfo.InvariantCulture),
                p.NetProfit.ToString(CultureInfo.InvariantCulture),
                p.NetMarginPercent.ToString(CultureInfo.InvariantCulture),
                Escape(p.ProfitDecision),
                Escape(p.Asin),
                Escape(p.Title),
                Escape(p.Brand),
                p.Price.ToString(CultureInfo.InvariantCulture),
                Escape(p.Currency),
                p.IsAmazonChoice.ToString(),
                p.IsSponsored.ToString(),
                p.HasLowStockWarning.ToString(),
                p.HasUsuallyKeepItemText.ToString(),
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
