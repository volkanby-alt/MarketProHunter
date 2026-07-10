namespace MarketProHunter.Models;

public enum SearchMode
{
    ReadyMarket,
    AmazonStoreLink,
    CustomSearch
}

public sealed record SearchTarget
{
    public SearchMode Mode { get; init; } = SearchMode.ReadyMarket;
    public string? Category { get; init; }
    public string? SubCategory { get; init; }
    public string? MarketName { get; init; }
    public string? StoreUrl { get; init; }
    public string? CustomQuery { get; init; }
    public bool ScanAllPages { get; init; }
    public int MaxPages { get; init; } = 5;

    public string DisplayName => Mode switch
    {
        SearchMode.ReadyMarket => MarketName ?? "Hazır mağaza",
        SearchMode.AmazonStoreLink => StoreUrl ?? "Amazon mağaza linki",
        SearchMode.CustomSearch => CustomQuery ?? "Özel arama",
        _ => "Arama"
    };
}
