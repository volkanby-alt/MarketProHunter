using MarketProHunter.Models;

namespace MarketProHunter.Scoring;

public sealed class ScoringEngine
{
    public ProductScore Score(ProductResult product)
    {
        var safety = CalculateSafetyScore(product);
        var sales = CalculateSalesScore(product);
        var profit = CalculateProfitScore(product);
        var overall = Clamp((int)Math.Round((safety * 0.38) + (sales * 0.35) + (profit * 0.27)));
        var confidence = CalculateConfidenceScore(product, safety, sales, profit, overall);
        var competition = CalculateCompetitionScore(product);
        var uploadScore = CalculateUploadScore(safety, sales, profit, confidence, competition, product);
        var uploadDecision = UploadDecisionFor(uploadScore, safety, confidence, competition, product);
        var recommendation = RecommendationFor(overall, safety, confidence, uploadScore, product);

        return new ProductScore(safety, sales, profit, overall, confidence, competition, uploadScore, uploadDecision, recommendation);
    }

    private static int CalculateSafetyScore(ProductResult product)
    {
        var score = 100;

        if (product.HasLowStockWarning) score -= 25;
        if (product.HasUsuallyKeepItemText) score -= 20;
        if (product.IsSponsored) score -= 7;
        if (string.IsNullOrWhiteSpace(product.Brand)) score -= 8;
        if (ContainsRiskText(product.Title)) score -= 15;
        if (product.Rating is > 0 and < 4.0m) score -= 10;
        if (product.ReviewCount is > 0 and < 25) score -= 6;
        if (product.ImageCount == 0) score -= 18;
        else if (product.ImageCount < 4) score -= 10;
        if (product.VisualRiskLevel.Equals("HIGH", StringComparison.OrdinalIgnoreCase)) score -= 12;
        else if (product.VisualRiskLevel.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase)) score -= 6;

        return Clamp(score);
    }

    private static int CalculateSalesScore(ProductResult product)
    {
        var score = 40;

        if (product.IsAmazonChoice) score += 25;
        if (product.Price is >= 9m and <= 60m) score += 13;
        else if (product.Price is > 60m and <= 98m) score += 7;
        if (!product.IsSponsored) score += 5;
        if (product.ImageCount >= 4) score += 4;
        if (product.Rating >= 4.5m) score += 10;
        else if (product.Rating >= 4.2m) score += 7;
        else if (product.Rating >= 4.0m) score += 4;
        if (product.ReviewCount >= 5000) score += 12;
        else if (product.ReviewCount >= 1000) score += 9;
        else if (product.ReviewCount >= 250) score += 6;
        else if (product.ReviewCount >= 50) score += 3;

        return Clamp(score);
    }

    private static int CalculateProfitScore(ProductResult product)
    {
        var score = 50;

        if (product.Price is >= 9m and <= 25m) score += 25;
        else if (product.Price is > 25m and <= 45m) score += 20;
        else if (product.Price is > 45m and <= 70m) score += 12;
        else if (product.Price is > 70m and <= 98m) score += 5;
        if (product.IsAmazonChoice) score += 8;
        if (product.Rating >= 4.4m && product.ReviewCount >= 100) score += 5;
        if (product.ImageCount >= 4) score += 3;

        return Clamp(score);
    }

    private static int CalculateConfidenceScore(ProductResult product, int safety, int sales, int profit, int overall)
    {
        var confidence = (int)Math.Round((overall * 0.45) + (safety * 0.25) + (sales * 0.20) + (profit * 0.10));

        if (product.IsAmazonChoice) confidence += 4;
        if (product.Rating >= 4.5m) confidence += 4;
        if (product.ReviewCount >= 1000) confidence += 4;
        if (product.ImageCount >= 4) confidence += 5;
        if (product.HasLowStockWarning) confidence -= 10;
        if (product.HasUsuallyKeepItemText) confidence -= 8;
        if (product.IsSponsored) confidence -= 4;
        if (ContainsRiskText(product.Title)) confidence -= 8;
        if (product.ImageCount == 0) confidence -= 12;
        else if (product.ImageCount < 4) confidence -= 7;
        if (product.VisualRiskLevel.Equals("HIGH", StringComparison.OrdinalIgnoreCase)) confidence -= 10;
        else if (product.VisualRiskLevel.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase)) confidence -= 5;

        return Clamp(confidence);
    }

    private static int CalculateCompetitionScore(ProductResult product)
    {
        var score = 45;

        if (product.ReviewCount >= 10000) score += 25;
        else if (product.ReviewCount >= 5000) score += 18;
        else if (product.ReviewCount >= 1000) score += 12;
        else if (product.ReviewCount >= 250) score += 7;
        else if (product.ReviewCount is > 0 and < 50) score -= 8;

        if (product.IsAmazonChoice) score += 8;
        if (product.IsSponsored) score += 6;
        if (product.Price is >= 9m and <= 35m) score += 8;
        if (ContainsGenericOpportunityText(product.Title)) score -= 10;
        if (ContainsRiskText(product.Title)) score += 10;

        return Clamp(score);
    }

    private static int CalculateUploadScore(int safety, int sales, int profit, int confidence, int competition, ProductResult product)
    {
        var lowCompetitionBonus = 100 - competition;
        var uploadScore = (int)Math.Round((confidence * 0.32) + (safety * 0.24) + (sales * 0.18) + (profit * 0.16) + (lowCompetitionBonus * 0.10));
        if (product.ImageCount >= 4) uploadScore += 3;
        else if (product.ImageCount == 0) uploadScore -= 10;
        else uploadScore -= 6;
        return Clamp(uploadScore);
    }

    private static string UploadDecisionFor(int uploadScore, int safetyScore, int confidenceScore, int competitionScore, ProductResult product)
    {
        if (safetyScore < 60 || confidenceScore < 60 || uploadScore < 60) return "Reject";
        if (product.ImageCount < 4 && uploadScore >= 88) return "Review";
        if (uploadScore >= 88 && safetyScore >= 80 && confidenceScore >= 82 && competitionScore <= 70) return "Upload";
        if (uploadScore >= 74) return "Review";
        return "Caution";
    }

    private static string RecommendationFor(int overallScore, int safetyScore, int confidenceScore, int uploadScore, ProductResult product)
    {
        if (safetyScore < 60 || overallScore < 60 || confidenceScore < 60 || uploadScore < 60) return "Reject";
        if (product.ImageCount < 4 && uploadScore >= 88) return "Review";
        if (uploadScore >= 88 && overallScore >= 82 && safetyScore >= 80 && confidenceScore >= 82) return "Upload";
        if (uploadScore >= 74 || overallScore >= 75) return "Review";
        return "Caution";
    }

    private static bool ContainsGenericOpportunityText(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var words = new[] { "organizer", "holder", "storage", "cleaning", "brush", "filter", "cover", "mat", "tray", "rack", "tool" };
        return words.Any(word => title.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsRiskText(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;

        var riskWords = new[]
        {
            "compatible with", "for apple", "for samsung", "replacement for", "oem", "genuine",
            "trademark", "licensed", "official", "logo"
        };

        return riskWords.Any(word => title.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
