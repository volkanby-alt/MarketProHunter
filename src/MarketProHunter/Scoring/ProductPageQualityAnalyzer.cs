using MarketProHunter.Amazon;

namespace MarketProHunter.Scoring;

public sealed record ProductPageQualityResult(
    int BulletPointQualityScore,
    int DescriptionQualityScore,
    int SpecificationQualityScore,
    string Notes);

public sealed class ProductPageQualityAnalyzer
{
    public ProductPageQualityResult Analyze(AmazonProductPageData pageData)
    {
        var bulletScore = CalculateBulletPointScore(pageData.BulletPoints);
        var descriptionScore = CalculateDescriptionScore(pageData.Description, pageData.HasAPlusContent);
        var specificationScore = CalculateSpecificationScore(pageData.SpecificationCount);
        var notes = BuildNotes(pageData, bulletScore, descriptionScore, specificationScore);

        return new ProductPageQualityResult(bulletScore, descriptionScore, specificationScore, notes);
    }

    private static int CalculateBulletPointScore(IReadOnlyList<string> bullets)
    {
        if (bullets.Count == 0) return 0;

        var score = bullets.Count switch
        {
            >= 6 => 95,
            5 => 100,
            4 => 88,
            3 => 72,
            2 => 55,
            _ => 35
        };

        var averageLength = bullets.Average(x => x.Length);
        if (averageLength >= 45 && averageLength <= 220) score += 5;
        else if (averageLength < 25) score -= 10;
        else if (averageLength > 260) score -= 8;

        return Clamp(score);
    }

    private static int CalculateDescriptionScore(string description, bool hasAPlusContent)
    {
        var length = string.IsNullOrWhiteSpace(description) ? 0 : description.Length;
        var score = length switch
        {
            >= 1200 => 95,
            >= 500 => 88,
            >= 180 => 70,
            >= 60 => 45,
            _ => 15
        };

        if (hasAPlusContent) score += 8;
        return Clamp(score);
    }

    private static int CalculateSpecificationScore(int specificationCount)
    {
        return specificationCount switch
        {
            >= 12 => 100,
            >= 8 => 90,
            >= 5 => 75,
            >= 3 => 58,
            >= 1 => 40,
            _ => 10
        };
    }

    private static string BuildNotes(AmazonProductPageData pageData, int bulletScore, int descriptionScore, int specificationScore)
    {
        var notes = new List<string>
        {
            bulletScore >= 80 ? $"Bullet points güçlü: {pageData.BulletPointCount}" : $"Bullet points zayıf/eksik: {pageData.BulletPointCount}",
            descriptionScore >= 80 ? "Açıklama güçlü" : "Açıklama zayıf veya eksik",
            specificationScore >= 75 ? $"Teknik detaylar yeterli: {pageData.SpecificationCount}" : $"Teknik detaylar eksik: {pageData.SpecificationCount}"
        };

        if (pageData.HasAPlusContent) notes.Add("A+ Content var");
        return string.Join(" | ", notes);
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
