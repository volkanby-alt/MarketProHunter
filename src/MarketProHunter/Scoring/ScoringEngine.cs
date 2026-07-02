using MarketProHunter.Models;

namespace MarketProHunter.Scoring;

public sealed class ScoringEngine
{
    public ProductScore Score(ProductResult product)
    {
        var safety = CalculateSafetyScore(product);
        var sales = CalculateSalesScore(product);
        var profit = CalculateProfitScore(product);
        var overall = Clamp((int)Math.Round((safety * 0.45) + (sales * 0.30) + (profit * 0.25)));
        var recommendation = RecommendationFor(overall, safety);

        return new ProductScore(safety, sales, profit, overall, recommendation);
    }

    private static int CalculateSafetyScore(ProductResult product)
    {
        var score = 100;

        if (product.HasLowStockWarning)
        {
            score -= 25;
        }

        if (product.HasUsuallyKeepItemText)
        {
            score -= 20;
        }

        if (product.IsSponsored)
        {
            score -= 5;
        }

        if (string.IsNullOrWhiteSpace(product.Brand))
        {
            score -= 8;
        }

        if (ContainsRiskText(product.Title))
        {
            score -= 15;
        }

        return Clamp(score);
    }

    private static int CalculateSalesScore(ProductResult product)
    {
        var score = 45;

        if (product.IsAmazonChoice)
        {
            score += 30;
        }

        if (product.Price is >= 9m and <= 60m)
        {
            score += 15;
        }
        else if (product.Price is > 60m and <= 98m)
        {
            score += 8;
        }

        if (!product.IsSponsored)
        {
            score += 5;
        }

        return Clamp(score);
    }

    private static int CalculateProfitScore(ProductResult product)
    {
        var score = 50;

        if (product.Price is >= 9m and <= 25m)
        {
            score += 25;
        }
        else if (product.Price is > 25m and <= 45m)
        {
            score += 20;
        }
        else if (product.Price is > 45m and <= 70m)
        {
            score += 12;
        }
        else if (product.Price is > 70m and <= 98m)
        {
            score += 5;
        }

        if (product.IsAmazonChoice)
        {
            score += 8;
        }

        return Clamp(score);
    }

    private static string RecommendationFor(int overallScore, int safetyScore)
    {
        if (safetyScore < 60 || overallScore < 60)
        {
            return "Reject";
        }

        if (overallScore >= 90 && safetyScore >= 80)
        {
            return "Upload";
        }

        if (overallScore >= 75)
        {
            return "Review";
        }

        return "Caution";
    }

    private static bool ContainsRiskText(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var riskWords = new[]
        {
            "compatible with", "for apple", "for samsung", "replacement for", "oem", "genuine",
            "trademark", "licensed", "official", "logo"
        };

        return riskWords.Any(word => title.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
