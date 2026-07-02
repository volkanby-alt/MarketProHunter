namespace MarketProHunter.Filters;

public sealed class VeroBrandFilter
{
    private readonly HashSet<string> _blockedBrands;

    public VeroBrandFilter(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(path))
        {
            path = relativePath;
        }

        _blockedBrands = File.Exists(path)
            ? File.ReadAllLines(path)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith('#'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsBlocked(string brandOrTitle)
    {
        if (string.IsNullOrWhiteSpace(brandOrTitle))
        {
            return false;
        }

        return _blockedBrands.Any(blocked => brandOrTitle.Contains(blocked, StringComparison.OrdinalIgnoreCase));
    }
}
