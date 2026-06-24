using Inventar.Storefront.Models;

namespace Inventar.Storefront.Services;

public static class StorefrontPricingHelper
{
    public static StorefrontPricingResult BuildPricing(StorefrontProduct product, int? width = null, int? length = null)
    {
        if (product.PoMjeri)
        {
            return BuildPoMjeriPricing(product.EffectivePrice, product.Price, length);
        }

        if (product.PerM2)
        {
            return BuildPerSquareMeterPricing(product.EffectivePrice, product.Price, width ?? product.Width, length ?? product.Length);
        }

        return BuildFlatPricing(product.EffectivePrice, product.Price);
    }

    public static StorefrontPricingResult BuildPerSquareMeterPricing(
        decimal currentPricePerSquareMeter,
        decimal compareAtPricePerSquareMeter,
        int? width,
        int? length)
    {
        var area = CalculateAreaSquareMeters(width, length);
        if (area <= 0)
        {
            return BuildFlatPricing(currentPricePerSquareMeter, compareAtPricePerSquareMeter);
        }

        var totalPrice = Math.Round(currentPricePerSquareMeter * area, 2);
        var compareAtTotalPrice = Math.Round(compareAtPricePerSquareMeter * area, 2);

        return new StorefrontPricingResult
        {
            UnitPrice = totalPrice,
            CompareAtUnitPrice = compareAtTotalPrice > totalPrice ? compareAtTotalPrice : null,
            PricePerSquareMeter = currentPricePerSquareMeter
        };
    }

    public static StorefrontPricingResult BuildPoMjeriPricing(
        decimal currentPricePerSquareMeter,
        decimal compareAtPricePerSquareMeter,
        int? customLength)
    {
        var lengthFactor = CalculatePoMjeriLengthFactor(customLength);
        var totalPrice = Math.Round(currentPricePerSquareMeter * lengthFactor, 2);
        var compareAtTotalPrice = Math.Round(compareAtPricePerSquareMeter * lengthFactor, 2);

        return new StorefrontPricingResult
        {
            UnitPrice = totalPrice,
            CompareAtUnitPrice = compareAtTotalPrice > totalPrice ? compareAtTotalPrice : null,
            PricePerSquareMeter = currentPricePerSquareMeter
        };
    }

    public static StorefrontPricingResult BuildPoMjeriPricing(
        IReadOnlyDictionary<int, StorefrontProduct> sourceProducts,
        IReadOnlyList<PoMjeriAllocationSlice> slices,
        int? customLength)
    {
        var pricingProduct = SelectPoMjeriPricingProduct(sourceProducts, slices);
        return pricingProduct == null
            ? new StorefrontPricingResult()
            : BuildPoMjeriPricing(pricingProduct.EffectivePrice, pricingProduct.Price, customLength);
    }

    public static StorefrontPricingResult BuildPoMjeriPricing(
        IReadOnlyDictionary<int, StorefrontProduct> sourceProducts,
        IReadOnlyCollection<CartItemAllocation> allocations,
        int? customLength)
    {
        var pricingProduct = SelectPoMjeriPricingProduct(sourceProducts, allocations);
        return pricingProduct == null
            ? new StorefrontPricingResult()
            : BuildPoMjeriPricing(pricingProduct.EffectivePrice, pricingProduct.Price, customLength);
    }

    public static StorefrontProduct? SelectPoMjeriPricingProduct(
        IReadOnlyDictionary<int, StorefrontProduct> sourceProducts,
        IReadOnlyList<PoMjeriAllocationSlice> slices)
    {
        if (slices.Count == 0)
        {
            return null;
        }

        return slices
            .Select(slice => sourceProducts.GetValueOrDefault(slice.ProductId))
            .Where(product => product != null)
            .OrderBy(product => product!.Width ?? int.MaxValue)
            .ThenBy(product => product!.Id)
            .FirstOrDefault();
    }

    public static StorefrontProduct? SelectPoMjeriPricingProduct(
        IReadOnlyDictionary<int, StorefrontProduct> sourceProducts,
        IReadOnlyCollection<CartItemAllocation> allocations)
    {
        if (allocations.Count == 0)
        {
            return null;
        }

        return allocations
            .Select(allocation => sourceProducts.GetValueOrDefault(allocation.SourceProductId))
            .Where(product => product != null)
            .OrderBy(product => product!.Width ?? int.MaxValue)
            .ThenBy(product => product!.Id)
            .FirstOrDefault();
    }

    public static StorefrontProduct? SelectPoMjeriPricingProduct(
        IEnumerable<StorefrontProduct> variants,
        string? color,
        int? customWidth)
    {
        var normalizedWidth = customWidth.GetValueOrDefault();
        return variants
            .Where(product =>
                product.PoMjeri &&
                product.Width.HasValue &&
                (string.IsNullOrWhiteSpace(color) ||
                 string.Equals(product.Color, color, StringComparison.OrdinalIgnoreCase)) &&
                (normalizedWidth <= 0 || product.Width.Value == normalizedWidth))
            .OrderBy(product => product.Width)
            .ThenBy(product => product.Id)
            .FirstOrDefault();
    }

    public static StorefrontProduct? SelectPoMjeriPricingProduct(
        IEnumerable<StorefrontProduct> variants,
        PoMjeriInventorySnapshot snapshot,
        string? color,
        int? customWidth,
        int? customLength)
    {
        var normalizedWidth = customWidth.GetValueOrDefault();
        var normalizedLength = customLength.GetValueOrDefault();

        return variants
            .Where(product =>
                product.PoMjeri &&
                product.Width.HasValue &&
                (string.IsNullOrWhiteSpace(color) ||
                 string.Equals(product.Color, color, StringComparison.OrdinalIgnoreCase)) &&
                (normalizedWidth <= 0 || product.Width.Value == normalizedWidth) &&
                (normalizedWidth <= 0 || normalizedLength <= 0 ||
                 CalculateMaxAvailableFromRemaining(snapshot, product, normalizedWidth, normalizedLength) > 0))
            .OrderBy(product => product.Width)
            .ThenBy(product => product.Id)
            .FirstOrDefault();
    }

    public static decimal CalculateAreaSquareMeters(int? width, int? length)
    {
        if (!width.HasValue || !length.HasValue || width.Value <= 0 || length.Value <= 0)
        {
            return 0m;
        }

        return (width.Value * length.Value) / 10000m;
    }

    public static decimal CalculatePoMjeriLengthFactor(int? customLength)
    {
        var normalizedLength = Math.Max(customLength ?? 100, 100);
        return normalizedLength / 100m;
    }

    private static StorefrontPricingResult BuildFlatPricing(decimal currentPrice, decimal compareAtPrice)
    {
        return new StorefrontPricingResult
        {
            UnitPrice = currentPrice,
            CompareAtUnitPrice = compareAtPrice > currentPrice ? compareAtPrice : null,
            PricePerSquareMeter = null
        };
    }

    private static int CalculateMaxAvailableFromRemaining(
        PoMjeriInventorySnapshot snapshot,
        StorefrontProduct product,
        int customWidth,
        int customLength)
    {
        return StorefrontPoMjeriPlanner.CalculateMaxAvailableQuantity(
            product.Width ?? 0,
            snapshot.GetAvailableRemainingLength(product.Id),
            customWidth,
            customLength);
    }
}

public sealed class StorefrontPricingResult
{
    public decimal UnitPrice { get; init; }
    public decimal? CompareAtUnitPrice { get; init; }
    public decimal? PricePerSquareMeter { get; init; }
}
