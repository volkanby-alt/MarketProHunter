namespace MarketProHunter.Scoring;

public sealed record ProductScore(
    int SafetyScore,
    int SalesScore,
    int ProfitScore,
    int OverallScore,
    int ConfidenceScore,
    string Recommendation)
{
    public string Stars => OverallScore switch
    {
        >= 90 => "★★★★★",
        >= 75 => "★★★★☆",
        >= 60 => "★★★☆☆",
        >= 40 => "★★☆☆☆",
        _ => "★☆☆☆☆"
    };
}
