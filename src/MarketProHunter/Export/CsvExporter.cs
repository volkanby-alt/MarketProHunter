using System.Globalization;
using System.Text;
using MarketProHunter.Models;
using MarketProHunter.Scoring;

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
        builder.AppendLine("UploadScore,UploadDecision,RiskLevel,SweetSpot,OpportunitySummary,CompetitionScore,ConfidenceScore,VisualRiskLevel,VisualRiskNotes,ImageCount,ImageUrl1,ImageUrl2,ImageUrl3,ImageUrl4,ImageUrl5,ImageUrl6,OverallScore,SafetyScore,SalesScore,ProfitScore,Recommendation,Stars,Rating,ReviewCount,AmazonCost,RecommendedSalePrice,EbayFee,PromotedFee,NetProfit,NetMarginPercent,ProfitDecision,ASIN,Title,Brand,Price,Currency,AmazonChoice,Sponsored,LowStock,UsuallyKeep,ProductUrl,Keyword,Page,Notes");

        foreach (var p in products)
        {
            builder.AppendLine(BuildProductRow(p));
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, cancellationToken);
    }

    public async Task WriteSmartQueueAsync(string path, SmartQueueResult queue, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("Rank,Tier,UploadScore,UploadDecision,RiskLevel,SweetSpot,CompetitionScore,ConfidenceScore,VisualRiskLevel,ImageCount,NetProfit,RecommendedSalePrice,ASIN,Brand,Title,ProductUrl,ImageUrl1,ImageUrl2,ImageUrl3,ImageUrl4,ImageUrl5,ImageUrl6,OpportunitySummary");

        foreach (var item in queue.Items)
        {
            var p = item.Product;
            builder.AppendLine(string.Join(',', new[]
            {
                item.Rank.ToString(CultureInfo.InvariantCulture),
                Escape(item.Tier),
                p.UploadScore.ToString(CultureInfo.InvariantCulture),
                Escape(p.UploadDecision),
                Escape(p.RiskLevel),
                Escape(p.SweetSpot),
                p.CompetitionScore.ToString(CultureInfo.InvariantCulture),
                p.ConfidenceScore.ToString(CultureInfo.InvariantCulture),
                Escape(p.VisualRiskLevel),
                p.ImageCount.ToString(CultureInfo.InvariantCulture),
                p.NetProfit.ToString(CultureInfo.InvariantCulture),
                p.RecommendedSalePrice.ToString(CultureInfo.InvariantCulture),
                Escape(p.Asin),
                Escape(p.Brand),
                Escape(p.Title),
                Escape(p.ProductUrl),
                Escape(p.ImageUrl1),
                Escape(p.ImageUrl2),
                Escape(p.ImageUrl3),
                Escape(p.ImageUrl4),
                Escape(p.ImageUrl5),
                Escape(p.ImageUrl6),
                Escape(p.OpportunitySummary)
            }));
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, cancellationToken);
    }

    private static string BuildProductRow(ProductResult p)
    {
        return string.Join(',', new[]
        {
            p.UploadScore.ToString(CultureInfo.InvariantCulture),
            Escape(p.UploadDecision),
            Escape(p.RiskLevel),
            Escape(p.SweetSpot),
            Escape(p.OpportunitySummary),
            p.CompetitionScore.ToString(CultureInfo.InvariantCulture),
            p.ConfidenceScore.ToString(CultureInfo.InvariantCulture),
            Escape(p.VisualRiskLevel),
            Escape(p.VisualRiskNotes),
            p.ImageCount.ToString(CultureInfo.InvariantCulture),
            Escape(p.ImageUrl1),
            Escape(p.ImageUrl2),
            Escape(p.ImageUrl3),
            Escape(p.ImageUrl4),
            Escape(p.ImageUrl5),
            Escape(p.ImageUrl6),
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
        });
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
