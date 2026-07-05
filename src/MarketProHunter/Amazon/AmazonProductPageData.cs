namespace MarketProHunter.Amazon;

public sealed record AmazonProductPageData(
    string Asin,
    IReadOnlyList<string> BulletPoints,
    string Description,
    IReadOnlyDictionary<string, string> Specifications,
    bool HasAPlusContent)
{
    public int BulletPointCount => BulletPoints.Count;
    public int SpecificationCount => Specifications.Count;
}
