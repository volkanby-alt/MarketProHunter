namespace MarketProHunter.Categories;

public static class KeywordCategoryProvider
{
    public static IReadOnlyList<KeywordCategory> GetDefaultCategories() => new List<KeywordCategory>
    {
        new("Home & Kitchen", new[]
        {
            new KeywordSubCategory("Kitchen Cleaning", new[] { "kitchen cleaner", "dish brush", "sink organizer", "dish drying rack", "scrub sponge", "cleaning brush set", "countertop cleaner" }),
            new KeywordSubCategory("Bathroom Cleaning", new[] { "bathroom cleaner", "toilet brush", "shower cleaner", "grout brush", "bathroom scrubber", "drain hair catcher" }),
            new KeywordSubCategory("Laundry Care", new[] { "laundry organizer", "dryer balls", "laundry bag", "lint remover", "clothes drying rack", "laundry basket" }),
            new KeywordSubCategory("Storage & Organization", new[] { "closet organizer", "storage bins", "under sink organizer", "drawer organizer", "pantry organizer", "cabinet organizer" }),
            new KeywordSubCategory("Vacuum & Floor Care", new[] { "vacuum accessories", "mop replacement", "microfiber mop", "floor scrub brush", "broom holder", "dustpan set" })
        }),
        new("Automotive", new[]
        {
            new KeywordSubCategory("Car Cleaning", new[] { "car cleaning kit", "car detailing brush", "microfiber car towels", "wheel brush", "car wash sponge" }),
            new KeywordSubCategory("Interior Accessories", new[] { "car organizer", "seat cover", "trunk organizer", "car phone holder", "car trash can" }),
            new KeywordSubCategory("Exterior Accessories", new[] { "windshield sun shade", "license plate frame", "car door edge guard", "auto trim", "car cover" }),
            new KeywordSubCategory("Tools & Maintenance", new[] { "tire pressure gauge", "car emergency kit", "battery terminal cleaner", "oil funnel", "trim removal tool" })
        }),
        new("Patio & Garden", new[]
        {
            new KeywordSubCategory("Garden Tools", new[] { "garden hose nozzle", "weed puller", "garden gloves", "garden tool organizer", "hand cultivator" }),
            new KeywordSubCategory("Plant Care", new[] { "plant support", "watering can", "plant saucer", "garden ties", "seed starter tray" }),
            new KeywordSubCategory("Outdoor Living", new[] { "patio cover", "outdoor storage", "grill accessories", "bird feeder", "patio furniture cover" })
        }),
        new("Electronics", new[]
        {
            new KeywordSubCategory("Cables & Adapters", new[] { "usb c hub", "hdmi cable", "ethernet cable", "charger cable", "card reader" }),
            new KeywordSubCategory("Desk Accessories", new[] { "laptop stand", "mouse pad", "cable organizer", "monitor stand", "desk power strip" }),
            new KeywordSubCategory("Computer Accessories", new[] { "wifi adapter", "webcam", "keyboard cover", "usb extension cable", "laptop sleeve" })
        }),
        new("Tools & Home Improvement", new[]
        {
            new KeywordSubCategory("Hand Tools", new[] { "hand tool set", "measuring tape", "screwdriver set", "level tool", "utility knife" }),
            new KeywordSubCategory("Garage Organization", new[] { "garage organizer", "wall hooks", "hardware organizer", "tool storage rack", "pegboard hooks" }),
            new KeywordSubCategory("Work Safety", new[] { "work gloves", "safety glasses", "knee pads", "ear protection", "dust mask" })
        }),
        new("Beauty & Personal Care", new[]
        {
            new KeywordSubCategory("Beauty Organization", new[] { "makeup organizer", "travel toiletry bag", "makeup mirror", "cosmetic bag", "vanity organizer" }),
            new KeywordSubCategory("Hair Accessories", new[] { "hair brush", "hair clips", "shower cap", "hair towel", "hair comb set" }),
            new KeywordSubCategory("Bath Accessories", new[] { "bath sponge", "body brush", "bath pillow", "shower caddy", "loofah set" })
        }),
        new("Pet Supplies", new[]
        {
            new KeywordSubCategory("Dog Supplies", new[] { "dog grooming brush", "dog leash", "dog waste bags", "dog bowl", "dog toy" }),
            new KeywordSubCategory("Cat Supplies", new[] { "cat litter scoop", "cat toy", "cat bowl", "cat grooming brush", "cat carrier" }),
            new KeywordSubCategory("Pet Cleaning", new[] { "pet hair remover", "pet stain remover", "pet grooming gloves", "lint roller pet hair", "pet bath brush" })
        })
    };
}
