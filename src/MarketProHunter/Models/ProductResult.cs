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
    public string ProductUrl { get; init; } = "";
    public string SearchKeyword { get; init; } = "";
    public int Page { get; init; }
    public string Notes { get; init; } = "";
}
