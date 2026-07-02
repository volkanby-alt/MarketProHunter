using MarketProHunter.Models;

namespace MarketProHunter.Profit;

public sealed class EbayProfitEngine
{
    public ProfitResult Calculate(ProductResult product, ProfitSettings settings)
    {
        var amazonCost = Round(product.Price * (1 + settings.AmazonTaxPercent / 100m) + settings.ShippingCost);
        var targetNetProfit = Math.Max(settings.MinimumNetProfit, amazonCost * settings.TargetProfitPercent / 100m);
        var combinedPercent = (settings.EbayFinalValueFeePercent + settings.PromotedPercent) / 100m;

        var rawSalePrice = (amazonCost + settings.EbayFixedFee + targetNetProfit) / (1 - combinedPercent);
        var recommendedSalePrice = RoundToRetailPrice(rawSalePrice);

        var ebayFee = Round(recommendedSalePrice * settings.EbayFinalValueFeePercent / 100m + settings.EbayFixedFee);
        var promotedFee = Round(recommendedSalePrice * settings.PromotedPercent / 100m);
        var totalCost = Round(amazonCost + ebayFee + promotedFee);
        var netProfit = Round(recommendedSalePrice - totalCost);
        var margin = recommendedSalePrice <= 0 ? 0 : Round(netProfit / recommendedSalePrice * 100m);
        var decision = netProfit >= settings.MinimumNetProfit && margin >= settings.TargetProfitPercent ? "Profitable" : "Low Profit";

        return new ProfitResult(
            amazonCost,
            recommendedSalePrice,
            ebayFee,
            promotedFee,
            totalCost,
            netProfit,
            margin,
            decision);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundToRetailPrice(decimal value)
    {
        var roundedUp = Math.Ceiling(value);
        return Math.Max(0.99m, roundedUp - 0.05m);
    }
}
