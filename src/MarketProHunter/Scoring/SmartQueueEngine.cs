using MarketProHunter.Models;

namespace MarketProHunter.Scoring;

public sealed record SmartQueueItem(
    int Rank,
    string Tier,
    ProductResult Product);

public sealed record SmartQueueResult(
    int RequestedCount,
    int SelectedCount,
    decimal ExpectedNetProfit,
    decimal AverageUploadScore,
    decimal AverageConfidenceScore,
    decimal AverageListingQualityScore,
    IReadOnlyList<SmartQueueItem> Items);

public sealed class SmartQueueEngine
{
    public const int DefaultQueueSize = 200;

    public SmartQueueResult Build(IEnumerable<ProductResult> products, int queueSize = DefaultQueueSize)
    {
        queueSize = Math.Clamp(queueSize, 1, 300);

        var selected = products
            .Where(IsQueueCandidate)
            .OrderByDescending(p => p.UploadScore)
            .ThenByDescending(p => p.NetProfit)
            .ThenByDescending(p => AverageListingQuality(p))
            .ThenByDescending(p => p.BulletPointQualityScore)
            .ThenByDescending(p => p.DescriptionQualityScore)
            .ThenByDescending(p => p.SpecificationQualityScore)
            .ThenByDescending(p => p.ContentQualityScore)
            .ThenByDescending(p => p.TitleQualityScore)
            .ThenByDescending(p => p.ImageQualityScore)
            .ThenBy(p => p.CompetitionScore)
            .ThenByDescending(p => p.ConfidenceScore)
            .ThenByDescending(p => p.ImageCount)
            .Take(queueSize)
            .Select((product, index) => new SmartQueueItem(index + 1, TierFor(index + 1, product), product))
            .ToList();

        return new SmartQueueResult(
            queueSize,
            selected.Count,
            selected.Sum(x => x.Product.NetProfit),
            selected.Count == 0 ? 0m : Math.Round(selected.Average(x => (decimal)x.Product.UploadScore), 2),
            selected.Count == 0 ? 0m : Math.Round(selected.Average(x => (decimal)x.Product.ConfidenceScore), 2),
            selected.Count == 0 ? 0m : Math.Round(selected.Average(x => AverageListingQuality(x.Product)), 2),
            selected);
    }

    private static bool IsQueueCandidate(ProductResult product)
    {
        if (product.UploadDecision.Equals("REJECT", StringComparison.OrdinalIgnoreCase)) return false;
        if (product.Recommendation.Equals("Reject", StringComparison.OrdinalIgnoreCase)) return false;
        if (product.SafetyScore < 60) return false;
        if (product.ConfidenceScore < 60) return false;
        if (product.NetProfit <= 0) return false;
        if (product.ImageQualityScore > 0 && product.ImageQualityScore < 45) return false;
        if (product.TitleQualityScore > 0 && product.TitleQualityScore < 40) return false;
        return product.UploadScore >= 60;
    }

    private static string TierFor(int rank, ProductResult product)
    {
        var quality = AverageListingQuality(product);
        if (rank <= 20 && product.UploadScore >= 94 && product.ConfidenceScore >= 90 && quality >= 85 && product.VisualRiskLevel == "LOW") return "Platinum";
        if (rank <= 80 && product.UploadScore >= 88 && quality >= 75) return "Gold";
        if (rank <= 150 && product.UploadScore >= 74) return "Silver";
        return "Bronze";
    }

    private static decimal AverageListingQuality(ProductResult product)
    {
        return Math.Round((product.TitleQualityScore + product.ImageQualityScore + product.ContentQualityScore + product.BulletPointQualityScore + product.DescriptionQualityScore + product.SpecificationQualityScore) / 6m, 2);
    }
}
