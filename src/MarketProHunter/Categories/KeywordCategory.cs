namespace MarketProHunter.Categories;

public sealed record KeywordCategory(
    string Name,
    IReadOnlyList<KeywordSubCategory> SubCategories)
{
    public IReadOnlyList<string> Keywords => SubCategories
        .SelectMany(x => x.Keywords)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

public sealed record KeywordSubCategory
{
    public KeywordSubCategory(string name, IReadOnlyList<string> keywords)
        : this(name, keywords, Array.Empty<string>())
    {
    }

    public KeywordSubCategory(string name, IReadOnlyList<string> keywords, IReadOnlyList<string> markets)
    {
        Name = name;
        Keywords = keywords;
        Markets = markets;
    }

    public string Name { get; }
    public IReadOnlyList<string> Keywords { get; }
    public IReadOnlyList<string> Markets { get; }

    public override string ToString() => Name;
}
