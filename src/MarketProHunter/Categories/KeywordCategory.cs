namespace MarketProHunter.Categories;

public sealed record KeywordCategory(
    string Name,
    IReadOnlyList<KeywordSubCategory> SubCategories)
{
    public IReadOnlyList<string> Keywords => SubCategories.SelectMany(x => x.Keywords).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

public sealed record KeywordSubCategory(
    string Name,
    IReadOnlyList<string> Keywords)
{
    public override string ToString() => Name;
}
