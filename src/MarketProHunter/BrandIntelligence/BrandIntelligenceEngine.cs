namespace MarketProHunter.BrandIntelligence;

public sealed class BrandIntelligenceEngine
{
    private readonly IReadOnlyDictionary<string, BrandProfile> _profiles;

    public BrandIntelligenceEngine(string profilePath = "config/brand-risk-profiles.csv")
    {
        _profiles = LoadProfiles(profilePath);
    }

    public BrandProfile Analyze(string? brand)
    {
        var normalized = NormalizeBrand(brand);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new BrandProfile("UNKNOWN", 45, "MEDIUM", "REVIEW", "Brand could not be detected.");
        }

        if (_profiles.TryGetValue(normalized, out var profile))
        {
            return profile;
        }

        return new BrandProfile(normalized, 25, "LOW", "REVIEW", "Brand is not in the profile list yet. Review manually before large batch uploads.");
    }

    private static IReadOnlyDictionary<string, BrandProfile> LoadProfiles(string profilePath)
    {
        var profiles = new Dictionary<string, BrandProfile>(StringComparer.OrdinalIgnoreCase);
        var fullPath = Path.IsPathRooted(profilePath) ? profilePath : Path.Combine(AppContext.BaseDirectory, profilePath);
        if (!File.Exists(fullPath)) return profiles;

        foreach (var rawLine in File.ReadAllLines(fullPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            var parts = line.Split(',', 5, StringSplitOptions.TrimEntries);
            if (parts.Length < 4) continue;
            if (!int.TryParse(parts[1], out var riskScore)) continue;

            var brand = NormalizeBrand(parts[0]);
            if (string.IsNullOrWhiteSpace(brand)) continue;

            var notes = parts.Length >= 5 ? parts[4] : string.Empty;
            profiles[brand] = new BrandProfile(brand, Math.Clamp(riskScore, 0, 100), parts[2].ToUpperInvariant(), parts[3].ToUpperInvariant(), notes);
        }

        return profiles;
    }

    private static string NormalizeBrand(string? brand)
    {
        return string.IsNullOrWhiteSpace(brand) ? string.Empty : brand.Trim().ToUpperInvariant();
    }
}
