namespace MarketProHunter.Models;

public sealed record ProductResult
{
    public string Asin { get; init; } = "";
    public string Title { get; init; } = "";
    public string Brand { get; init; } = "";
    public decimal Price { get; init; }
    public string Currency { get; init; } = "USD";
    public bool IsAmazonChoice { get; init; }
    public bool IsSponsored { get; init; }
    public bool HasLowStockWarning { get; init; }
    public bool HasUsuallyKeepItemText { get; init; }
    public decimal Rating { get; init; }
    public int ReviewCount { get; init; }
    public int ConfidenceScore { get; init; }
    public int CompetitionScore { get; init; }
    public int UploadScore { get; init; }
    public string UploadDecision { get; init; } = "";
    public string ProductUrl { get; init; } = "";
    public string SearchKeyword { get; init; } = "";
    public int Page { get; init; }
    public int SafetyScore { get; init; }
    public int SalesScore { get; init; }
    public int ProfitScore { get; init; }
    public int OverallScore { get; init; }
    public string Recommendation { get; init; } = "";
    public string Stars { get; init; } = "";
    public decimal RecommendedSalePrice { get; init; }
    public decimal EbayFee { get; init; }
    public decimal PromotedFee { get; init; }
    public decimal NetProfit { get; init; }
    public decimal NetMarginPercent { get; init; }
    public string ProfitDecision { get; init; } = "";
    public string Notes { get; init; } = "";
}
