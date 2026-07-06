using MarketProHunter.Models;

namespace MarketProHunter.Decisions;

public sealed record FinalDecisionResult(
    int Score,
    string Decision,
    string Tier,
    string Reason);

public sealed class FinalDecisionEngine
{
    public FinalDecisionResult Analyze(ProductResult product)
    {
        var listingQuality = Average(
            product.TitleQualityScore,
            product.ImageQualityScore,
            product.ContentQualityScore,
            product.BulletPointQualityScore,
            product.DescriptionQualityScore,
            product.SpecificationQualityScore);

        var lowCompetitionScore = 100 - product.CompetitionScore;
        var score = Clamp((int)Math.Round(
            product.BrandScore * 0.20m +
            lowCompetitionScore * 0.18m +
            product.ProfitScore * 0.22m +
            listingQuality * 0.14m +
            product.ConfidenceScore * 0.16m +
            product.ImageQualityScore * 0.10m));

        if (IsHardReject(product))
        {
            return new FinalDecisionResult(Math.Min(score, 59), "Reject", "Blocked", BuildReason(product, listingQuality, lowCompetitionScore));
        }

        var decision = score switch
        {
            >= 93 when product.NetProfit >= 8m => "Buy Immediately",
            >= 86 => "Upload",
            >= 74 => "Review",
            >= 65 => "Watch",
            _ => "Reject"
        };

        var tier = score switch
        {
            >= 93 => "Elite",
            >= 86 => "Premium",
            >= 74 => "Good",
            >= 65 => "Watch",
            _ => "Reject"
        };

        return new FinalDecisionResult(score, decision, tier, BuildReason(product, listingQuality, lowCompetitionScore));
    }

    private static bool IsHardReject(ProductResult product)
    {
        return product.BrandAction.Equals("REJECT", StringComparison.OrdinalIgnoreCase)
            || product.UploadDecision.Equals("Reject", StringComparison.OrdinalIgnoreCase)
            || product.Recommendation.Equals("Reject", StringComparison.OrdinalIgnoreCase)
            || product.BrandScore < 55
            || product.SafetyScore < 60
            || product.NetProfit < 2m;
    }

    private static string BuildReason(ProductResult product, int listingQuality, int lowCompetitionScore)
    {
        var reasons = new List<string>();

        Add(reasons, product.BrandScore >= 75, "Brand güvenli", "Brand kontrol gerekli");
        Add(reasons, product.NetProfit >= 8m, "Net kâr güçlü", "Net kâr sınırlı");
        Add(reasons, lowCompetitionScore >= 55, "Rekabet uygun", "Rekabet yüksek olabilir");
        Add(reasons, listingQuality >= 70, "Listing kalitesi iyi", "Listing kalitesi zayıf");
        Add(reasons, product.ConfidenceScore >= 80, "Confidence yüksek", "Confidence düşük");
        Add(reasons, product.ImageQualityScore >= 70, "Görseller yeterli", "Görseller kontrol edilmeli");

        return string.Join(" | ", reasons);
    }

    private static void Add(ICollection<string> reasons, bool positive, string good, string bad)
    {
        reasons.Add((positive ? "+ " : "- ") + (positive ? good : bad));
    }

    private static int Average(params int[] values)
    {
        var valid = values.Where(x => x > 0).ToArray();
        return valid.Length == 0 ? 0 : Clamp((int)Math.Round(valid.Average()));
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
