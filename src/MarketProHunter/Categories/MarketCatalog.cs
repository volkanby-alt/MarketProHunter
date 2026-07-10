namespace MarketProHunter.Categories;

public static class MarketCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> MarketsByCategory =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Automotive"] = new[]
            {
                "Dorman", "X AUTOHAUX", "uxcell", "QWORK", "MroMax", "Fielect", "MECCANIXITY",
                "AstroAI", "EPAuto", "Lisle", "OEMTOOLS", "Performance Tool", "Permatex", "Gates",
                "Bosch Automotive", "ACDelco", "Delphi", "Standard Motor Products", "Fel-Pro", "Monroe",
                "NOCO", "Schumacher Electric", "Meguiar's", "Chemical Guys", "Mothers", "Rain-X"
            },
            ["Home & Kitchen"] = new[]
            {
                "OXO", "Rubbermaid", "Simplehuman", "Joseph Joseph", "Sterilite", "IRIS USA", "Whitmor",
                "Honey-Can-Do", "mDesign", "Vtopmart", "Scotch-Brite", "Libman", "MR.SIGA", "Holikme"
            },
            ["Electronics"] = new[]
            {
                "Anker", "UGREEN", "Belkin", "Cable Matters", "Baseus", "Sabrent", "StarTech", "BENFEI",
                "JSAUX", "Syntech", "TP-Link", "Logitech", "ORICO", "Satechi", "Kensington"
            },
            ["Tools & Home Improvement"] = new[]
            {
                "DEWALT", "Klein Tools", "Stanley", "CRAFTSMAN", "WORKPRO", "Milwaukee", "IRWIN",
                "Wera", "Wiha", "TEKTON", "QWORK", "HORUSDY", "3M", "Mechanix Wear"
            },
            ["Patio & Garden"] = new[]
            {
                "Fiskars", "Corona", "GARDENA", "AMES", "Edward Tools", "WORKPRO", "FANHAO",
                "Radius Garden", "Classic Accessories", "Suncast", "Keter", "Weber", "Cuisinart"
            },
            ["Beauty & Personal Care"] = new[]
            {
                "Conair", "Revlon", "Remington", "Goody", "Scunci", "Tweezerman", "EcoTools",
                "Real Techniques", "Wet Brush", "AQUIS", "Kitsch", "Tangle Teezer"
            },
            ["Pet Supplies"] = new[]
            {
                "KONG", "Chuckit!", "PetSafe", "Nylabone", "Hartz", "Outward Hound", "FURminator",
                "Earth Rated", "Arm & Hammer", "Catit", "IRIS USA", "MidWest Homes for Pets"
            }
        };

    public static IReadOnlyList<string> GetMarkets(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return Array.Empty<string>();
        return MarketsByCategory.TryGetValue(categoryName, out var markets)
            ? markets
            : Array.Empty<string>();
    }
}
