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
    IReadOnlyList<SmartQueueItem> Items);

public sealed class SmartQueueEngine
{
    public const int DefaultQueueSize = 50;

    public SmartQueueResult Build(IEnumerable<ProductResult> products, int queueSize = DefaultQueueSize)
    {
        queueSize = Math.Clamp(queueSize, 1, 200);

        var selected = products
            .Where(IsQueueCandidate)
            .OrderByDescending(p => p.UploadDecision.Equals("UPLOAD NOW", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.UploadScore)
            .ThenByDescending(p => p.ConfidenceScore)
            .ThenByDescending(p => p.NetProfit)
            .ThenBy(p => p.CompetitionScore)
            .ThenByDescending(p => p.ImageCount)
            .Take(queueSize)
            .Select((product, index) => new SmartQueueItem(index + 1, TierFor(index + 1, product), product))
            .ToList();

        return new SmartQueueResult(
            queueSize,
            selected.Count,
            selected.Sum(x => x.Product.NetProfit),
            selected.Count == 0 ? 0 : Math.Round(selected.Average(x => x.Product.UploadScore), 2),
            selected.Count == 0 ? 0 : Math.Round(selected.Average(x => x.Product.ConfidenceScore), 2),
            selected);
    }

    private static bool IsQueueCandidate(ProductResult product)
    {
        if (product.UploadDecision.Equals("REJECT", StringComparison.OrdinalIgnoreCase)) return false;
        if (product.Recommendation.Equals("Reject", StringComparison.OrdinalIgnoreCase)) return false;
        if (product.SafetyScore < 60) return false;
        if (product.ConfidenceScore < 60) return false;
        if (product.NetProfit <= 0) return false;
        return product.UploadScore >= 60;
    }

    private static string TierFor(int rank, ProductResult product)
    {
        if (rank <= 10 && product.UploadScore >= 94 && product.ConfidenceScore >= 90 && product.VisualRiskLevel == "LOW") return "Platinum";
        if (rank <= 25 && product.UploadScore >= 88) return "Gold";
        if (rank <= 40 && product.UploadScore >= 74) return "Silver";
        return "Bronze";
    }
}
