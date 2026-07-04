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
        var sweetSpot = CalculateSweetSpot(product);
        var summary = BuildSummary(product, riskLevel, sweetSpot);

        return new OpportunityAnalysis(riskLevel, sweetSpot, summary);
    }

    private static string CalculateRiskLevel(ProductResult product)
    {
        if (product.UploadDecision.Equals("REJECT", StringComparison.OrdinalIgnoreCase) ||
            product.Recommendation.Equals("Reject", StringComparison.OrdinalIgnoreCase) ||
            product.SafetyScore < 60 ||
            product.ConfidenceScore < 60 ||
            product.VisualRiskLevel.Equals("HIGH", StringComparison.OrdinalIgnoreCase))
        {
            return "HIGH";
        }

        if (product.HasLowStockWarning ||
            product.HasUsuallyKeepItemText ||
            product.ImageCount < 4 ||
            product.NetProfit < 2m ||
            product.CompetitionScore > 75)
        {
            return "MEDIUM";
        }

        return "LOW";
    }

    private static string CalculateSweetSpot(ProductResult product)
    {
        if (product.UploadScore >= 88 &&
            product.ConfidenceScore >= 82 &&
            product.SafetyScore >= 80 &&
            product.NetProfit >= 2m &&
            product.VisualRiskLevel.Equals("LOW", StringComparison.OrdinalIgnoreCase))
        {
            return "Strong upload candidate";
        }

        if (product.UploadScore >= 74 && product.NetProfit > 0m)
        {
            return "Review before upload";
        }

        if (product.NetProfit <= 0m)
        {
            return "Not profitable";
        }

        return "Low priority";
    }

    private static string BuildSummary(ProductResult product, string riskLevel, string sweetSpot)
    {
        var parts = new List<string>
        {
            $"Risk: {riskLevel}",
            $"{sweetSpot}",
            $"Upload {product.UploadScore}",
            $"Confidence {product.ConfidenceScore}%",
            $"Competition {product.CompetitionScore}",
            $"Net ${product.NetProfit:0.00}"
        };

        if (product.ImageCount < 4)
        {
            parts.Add($"images {product.ImageCount}/4 minimum");
        }

        if (product.HasLowStockWarning)
        {
            parts.Add("low stock warning");
        }

        if (product.HasUsuallyKeepItemText)
        {
            parts.Add("usually keep text");
        }

        return string.Join(" | ", parts);
    }
}
