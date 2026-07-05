using MarketProHunter.Models;

namespace MarketProHunter.Scoring;

public sealed record ListingQualityResult(
    int TitleQualityScore,
    int ImageQualityScore,
    int ContentQualityScore,
    string Notes);

public sealed class ListingQualityAnalyzer
{
    public ListingQualityResult Analyze(ProductResult product)
    {
        var titleScore = CalculateTitleQuality(product.Title);
        var imageScore = CalculateImageQuality(product.ImageCount, product.VisualRiskLevel);
        var contentScore = CalculateContentQuality(product);
        var notes = BuildNotes(product, titleScore, imageScore, contentScore);

        return new ListingQualityResult(titleScore, imageScore, contentScore, notes);
    }

    private static int CalculateTitleQuality(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return 0;

        var score = 45;
        var wordCount = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        if (title.Length is >= 70 and <= 180) score += 25;
        else if (title.Length is >= 45 and < 70) score += 15;
        else if (title.Length > 180) score -= 12;
        else score -= 15;

        if (wordCount is >= 8 and <= 28) score += 15;
        else if (wordCount < 6) score -= 12;

        if (ContainsAny(title, "for ", "with ", "set", "pack", "kit", "compatible", "replacement", "organizer", "cleaner", "filter", "cover", "mat", "tool")) score += 8;
        if (ContainsAny(title, "!!!", "best", "hot sale", "cheap", "free shipping")) score -= 12;

        return Clamp(score);
    }

    private static int CalculateImageQuality(int imageCount, string visualRiskLevel)
    {
        var score = imageCount switch
        {
            >= 6 => 100,
            5 => 92,
            4 => 84,
            3 => 68,
            2 => 54,
            1 => 40,
            _ => 0
        };

        if (visualRiskLevel.Equals("HIGH", StringComparison.OrdinalIgnoreCase)) score -= 25;
        else if (visualRiskLevel.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase)) score -= 12;

        return Clamp(score);
    }

    private static int CalculateContentQuality(ProductResult product)
    {
        var score = 45;

        if (!string.IsNullOrWhiteSpace(product.Title)) score += 15;
        if (!string.IsNullOrWhiteSpace(product.Brand)) score += 10;
        if (product.Rating > 0) score += 8;
        if (product.ReviewCount > 0) score += 8;
        if (product.ImageCount >= 4) score += 10;
        if (!string.IsNullOrWhiteSpace(product.ProductUrl)) score += 4;

        return Clamp(score);
    }

    private static string BuildNotes(ProductResult product, int titleScore, int imageScore, int contentScore)
    {
        var notes = new List<string>();

        notes.Add(titleScore >= 80 ? "Başlık açıklayıcı" : "Başlık kontrol edilmeli");
        notes.Add(imageScore >= 80 ? "Fotoğraf seti güçlü" : "Fotoğraf sayısı/kalitesi yetersiz olabilir");
        notes.Add(contentScore >= 80 ? "Ürün kartı bilgileri yeterli" : "Alt açıklama/bilgi tarafı manuel kontrol edilmeli");

        if (product.ImageCount < 4) notes.Add($"Görsel az: {product.ImageCount}/4 minimum");
        if (product.Title.Length < 45) notes.Add("Başlık kısa");
        if (string.IsNullOrWhiteSpace(product.Brand)) notes.Add("Marka okunamadı");

        return string.Join(" | ", notes);
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
