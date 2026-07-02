namespace MarketProHunter.Profit;

public sealed record ProfitSettings
{
    public decimal EbayFinalValueFeePercent { get; init; } = 13.25m;
    public decimal EbayFixedFee { get; init; } = 0.40m;
    public decimal PromotedPercent { get; init; } = 5.00m;
    public decimal TargetProfitPercent { get; init; } = 12.00m;
    public decimal MinimumNetProfit { get; init; } = 2.00m;
    public decimal AmazonTaxPercent { get; init; } = 0.00m;
    public decimal ShippingCost { get; init; } = 0.00m;

    public static ProfitSettings Default => new();
}
