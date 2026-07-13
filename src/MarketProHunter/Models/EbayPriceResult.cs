namespace MarketProHunter.Models;

public sealed record EbayPriceResult(
    bool IsListed,
    decimal? MinimumPrice,
    decimal? MaximumPrice,
    string SearchUrl,
    string? Error = null)
{
    public string StatusText => Error is not null
        ? "Hata"
        : IsListed ? "Satılıyor" : "Yok";

    public string PriceRangeText => MinimumPrice.HasValue && MaximumPrice.HasValue
        ? MinimumPrice.Value == MaximumPrice.Value
            ? $"${MinimumPrice.Value:0.00}"
            : $"${MinimumPrice.Value:0.00} - ${MaximumPrice.Value:0.00}"
        : "-";
}
