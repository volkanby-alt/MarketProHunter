namespace MarketProHunter.BrandIntelligence;

public sealed record BrandProfile(
    string Brand,
    int RiskScore,
    string RiskLevel,
    string Recommendation,
    string Notes);
