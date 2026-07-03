using MarketProHunter.Models;

namespace MarketProHunter.Scoring;

public sealed record OpportunityAnalysis(
    string RiskLevel,
    string SweetSpot,
    string Summary);

public sealed class OpportunityAnalyzer
{
    public OpportunityAnalysis Analyze(ProductResult product)
    {
        var riskLevel = CalculateRiskLevel(product);
        var sweetSpot = CalculateSweetSpot(product.Price);
        var reasons = BuildReasons(product, riskLevel, sweetSpot);
        return new OpportunityAnalysis(riskLevel, sweetSpot, string.Join(" | ", reasons));
    }

    private static string CalculateRiskLevel(ProductResult product)
    {
        if (product.SafetyScore >= 85 && !product.HasLowStockWarning && !product.HasUsuallyKeepItemText && !product.IsSponsored)
        {
            return "LOW";
        }

        if (product.SafetyScore >= 70 && !product.HasUsuallyKeepItemText)
        {
            return "MEDIUM";
        }

        return "HIGH";
    }

    private static string CalculateSweetSpot(decimal price)
    {
        return price switch
        {
            >= 9m and <= 25m => "9-25 FAST TEST",
            > 25m and <= 45m => "25-45 BEST BALANCE",
            > 45m and <= 70m => "45-70 PROFIT FOCUS",
            > 70m and <= 98m => "70-98 HIGH TICKET",
            _ => "OUTSIDE TARGET"
        };
    }

    private static IReadOnlyList<string> BuildReasons(ProductResult product, string riskLevel, string sweetSpot)
    {
        var reasons = new List<string>
        {
            $"Risk {riskLevel}",
            sweetSpot,
            $"Upload {product.UploadScore}",
            $"Confidence {product.ConfidenceScore}%"
        };

        if (product.IsAmazonChoice) reasons.Add("Amazon Choice");
        if (product.Rating >= 4.4m) reasons.Add($"Strong rating {product.Rating}/5");
        if (product.ReviewCount >= 1000) reasons.Add($"High reviews {product.ReviewCount}");
        else if (product.ReviewCount >= 100) reasons.Add($"Healthy reviews {product.ReviewCount}");
        if (product.CompetitionScore <= 45) reasons.Add("Low competition signal");
        if (product.NetProfit > 0) reasons.Add($"Net profit ${product.NetProfit}");
        if (!product.HasLowStockWarning) reasons.Add("No low-stock warning");
        if (!product.HasUsuallyKeepItemText) reasons.Add("No usually-keep warning");

        return reasons;
    }
}
