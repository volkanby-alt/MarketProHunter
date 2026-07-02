using System.Text.Json;
using MarketProHunter.Models;

namespace MarketProHunter.Decisions;

public sealed class UserDecisionStore
{
    private readonly string _dataDirectory;
    private readonly string _favoritesPath;
    private readonly string _rejectedAsinsPath;
    private readonly string _rejectedBrandsPath;

    private readonly HashSet<string> _favoriteAsins = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _rejectedAsins = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _rejectedBrands = new(StringComparer.OrdinalIgnoreCase);

    public UserDecisionStore()
    {
        _dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        _favoritesPath = Path.Combine(_dataDirectory, "favorites.json");
        _rejectedAsinsPath = Path.Combine(_dataDirectory, "rejected-asins.json");
        _rejectedBrandsPath = Path.Combine(_dataDirectory, "rejected-brands.json");
        Directory.CreateDirectory(_dataDirectory);
        Load();
    }

    public bool IsFavorite(string asin) => _favoriteAsins.Contains(asin);

    public bool IsRejected(ProductResult product)
    {
        return _rejectedAsins.Contains(product.Asin) ||
               (!string.IsNullOrWhiteSpace(product.Brand) && _rejectedBrands.Contains(product.Brand));
    }

    public void AddFavorite(ProductResult product)
    {
        if (!string.IsNullOrWhiteSpace(product.Asin))
        {
            _favoriteAsins.Add(product.Asin);
            SaveSet(_favoritesPath, _favoriteAsins);
        }
    }

    public void RejectAsin(ProductResult product)
    {
        if (!string.IsNullOrWhiteSpace(product.Asin))
        {
            _rejectedAsins.Add(product.Asin);
            _favoriteAsins.Remove(product.Asin);
            SaveSet(_rejectedAsinsPath, _rejectedAsins);
            SaveSet(_favoritesPath, _favoriteAsins);
        }
    }

    public void RejectBrand(ProductResult product)
    {
        if (!string.IsNullOrWhiteSpace(product.Brand))
        {
            _rejectedBrands.Add(product.Brand);
            SaveSet(_rejectedBrandsPath, _rejectedBrands);
        }
    }

    public IReadOnlyCollection<string> FavoriteAsins => _favoriteAsins;
    public IReadOnlyCollection<string> RejectedAsins => _rejectedAsins;
    public IReadOnlyCollection<string> RejectedBrands => _rejectedBrands;

    private void Load()
    {
        LoadSet(_favoritesPath, _favoriteAsins);
        LoadSet(_rejectedAsinsPath, _rejectedAsins);
        LoadSet(_rejectedBrandsPath, _rejectedBrands);
    }

    private static void LoadSet(string path, HashSet<string> target)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? new List<string>();
            foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                target.Add(value.Trim());
            }
        }
        catch
        {
            // Ignore corrupt user data files and continue with an empty set.
        }
    }

    private static void SaveSet(string path, IEnumerable<string> values)
    {
        var ordered = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        File.WriteAllText(path, JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true }));
    }
}
