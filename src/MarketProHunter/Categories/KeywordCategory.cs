namespace MarketProHunter.Categories;

public sealed record KeywordCategory(
    string Name,
    IReadOnlyList<string> Keywords);
