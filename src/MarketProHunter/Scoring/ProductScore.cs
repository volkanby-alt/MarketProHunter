namespace MarketProHunter.Scoring;

public sealed record ProductScore(
    int SafetyScore,
    int SalesScore,
    int ProfitScore,
    int OverallScore,
    int ConfidenceScore,
    int CompetitionScore,
    int UploadScore,
    string UploadDecision,
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
