namespace MarketProHunter.Categories;

public static class KeywordCategoryProvider
{
    public static IReadOnlyList<KeywordCategory> GetDefaultCategories() => new List<KeywordCategory>
    {
        new("Home & Kitchen", new[]
        {
            "home cleaner", "bathroom cleaner", "kitchen cleaner", "laundry organizer", "closet organizer",
            "storage bins", "vacuum accessories", "mop replacement", "dish drying rack", "sink organizer"
        }),
        new("Automotive", new[]
        {
            "car organizer", "car cleaning kit", "floor mats", "seat cover", "trunk organizer",
            "windshield sun shade", "tire pressure gauge", "car phone holder", "auto trim", "car detailing brush"
        }),
        new("Patio & Garden", new[]
        {
            "garden hose nozzle", "plant support", "patio cover", "weed puller", "garden gloves",
            "outdoor storage", "grill accessories", "watering can", "bird feeder", "garden tool organizer"
        }),
        new("Electronics", new[]
        {
            "usb c hub", "hdmi cable", "ethernet cable", "wifi adapter", "laptop stand",
            "mouse pad", "webcam", "card reader", "power strip", "charger cable"
        }),
        new("Tools & Home Improvement", new[]
        {
            "hand tool set", "measuring tape", "garage organizer", "work gloves", "screwdriver set",
            "drill bits", "wall hooks", "hardware organizer", "level tool", "utility knife"
        }),
        new("Beauty & Personal Care", new[]
        {
            "makeup organizer", "hair brush", "travel toiletry bag", "beauty sponge", "nail tools",
            "shower cap", "hair clips", "bath sponge", "skin care tool", "makeup mirror"
        }),
        new("Pet Supplies", new[]
        {
            "dog grooming brush", "pet bowl", "cat litter scoop", "pet hair remover", "dog leash",
            "pet toy", "aquarium accessories", "bird cage accessories", "pet carrier", "dog waste bags"
        })
    };
}
