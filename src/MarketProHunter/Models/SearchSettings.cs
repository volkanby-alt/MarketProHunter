namespace MarketProHunter.Models;

public sealed record SearchSettings
{
    public string MarketplaceBaseUrl { get; init; } = "https://www.amazon.com";
    public string ZipCode { get; init; } = "07073";
    public decimal MinPrice { get; init; } = 9m;
    public decimal MaxPrice { get; init; } = 98m;
    public bool RequireAmazonChoice { get; init; } = true;
    public bool ExcludeLowStock { get; init; } = true;
    public bool ExcludeUsuallyKeepItem { get; init; } = false;
    public bool ExcludeSponsored { get; init; } = true;
    public int DelayBetweenPagesMs { get; init; } = 2500;
    public int MaxParallelSearches { get; init; } = 3;

    public static SearchSettings Default => new();
}
