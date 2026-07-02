namespace MarketProHunter.Profit;

public sealed record ProfitResult(
    decimal AmazonCost,
    decimal RecommendedSalePrice,
    decimal EbayFee,
    decimal PromotedFee,
    decimal TotalCost,
    decimal NetProfit,
    decimal NetMarginPercent,
    string ProfitDecision);
