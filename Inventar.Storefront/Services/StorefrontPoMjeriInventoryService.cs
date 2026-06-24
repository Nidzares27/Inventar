using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Storefront.Services;

public class StorefrontPoMjeriInventoryService
{
    private readonly StorefrontDbContext _dbContext;

    public StorefrontPoMjeriInventoryService(StorefrontDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PoMjeriInventorySnapshot> LoadSnapshotAsync(
        IEnumerable<StorefrontProduct> products,
        IReadOnlyCollection<CartItem>? cartItems = null,
        string? excludeCartLineId = null,
        CancellationToken cancellationToken = default)
    {
        var poMjeriProducts = products
            .Where(product => product.PoMjeri && product.Length.HasValue && product.Width.HasValue)
            .GroupBy(product => product.Id)
            .Select(group => group.First())
            .ToList();

        if (poMjeriProducts.Count == 0)
        {
            return new PoMjeriInventorySnapshot(new Dictionary<int, int>(), new Dictionary<int, int>());
        }

        var productIds = poMjeriProducts.Select(product => product.Id).ToList();

        var soldLengths = await _dbContext.Sales
            .AsNoTracking()
            .Where(sale => productIds.Contains(sale.TepihId) && !sale.Disabled)
            .GroupBy(sale => sale.TepihId)
            .Select(group => new
            {
                TepihId = group.Key,
                ConsumedLength = group.Sum(sale => (sale.ConsumedLength ?? sale.CustomLength ?? 0) * sale.Quantity)
            })
            .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength, cancellationToken);

        var reservedLengths = await _dbContext.InventoryReservations
            .AsNoTracking()
            .Where(reservation =>
                productIds.Contains(reservation.TepihId) &&
                reservation.Status == InventoryReservationStatuses.Active &&
                reservation.ConsumedLengthPerUnit.HasValue)
            .GroupBy(reservation => reservation.TepihId)
            .Select(group => new
            {
                TepihId = group.Key,
                ConsumedLength = group.Sum(reservation => reservation.Quantity * (reservation.ConsumedLengthPerUnit ?? 0))
            })
            .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength, cancellationToken);

        var cartReservedLengths = cartItems?
            .Where(item =>
                item.PoMjeri &&
                !string.Equals(item.LineId, excludeCartLineId, StringComparison.Ordinal) &&
                item.Allocations.Count > 0)
            .SelectMany(item => item.Allocations)
            .GroupBy(allocation => allocation.SourceProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(allocation => allocation.Quantity * allocation.ConsumedLengthPerUnit))
            ?? new Dictionary<int, int>();

        var remainingLengths = new Dictionary<int, int>(poMjeriProducts.Count);
        var availableRemainingLengths = new Dictionary<int, int>(poMjeriProducts.Count);

        foreach (var product in poMjeriProducts)
        {
            var originalLength = product.Length ?? 0;
            var sold = soldLengths.GetValueOrDefault(product.Id);
            var reserved = reservedLengths.GetValueOrDefault(product.Id);
            var remainingLength = Math.Max(originalLength - sold - reserved, 0);
            remainingLengths[product.Id] = remainingLength;

            var cartReserved = cartReservedLengths.GetValueOrDefault(product.Id);
            availableRemainingLengths[product.Id] = Math.Max(remainingLength - cartReserved, 0);
        }

        return new PoMjeriInventorySnapshot(remainingLengths, availableRemainingLengths);
    }
}

public sealed class PoMjeriInventorySnapshot
{
    private readonly IReadOnlyDictionary<int, int> _remainingLengths;
    private readonly IReadOnlyDictionary<int, int> _availableRemainingLengths;

    public PoMjeriInventorySnapshot(
        IReadOnlyDictionary<int, int> remainingLengths,
        IReadOnlyDictionary<int, int> availableRemainingLengths)
    {
        _remainingLengths = remainingLengths;
        _availableRemainingLengths = availableRemainingLengths;
    }

    public int GetRemainingLength(int productId)
    {
        return _remainingLengths.TryGetValue(productId, out var remainingLength)
            ? remainingLength
            : 0;
    }

    public int GetAvailableRemainingLength(int productId)
    {
        return _availableRemainingLengths.TryGetValue(productId, out var remainingLength)
            ? remainingLength
            : 0;
    }

    public int GetEffectiveAvailability(StorefrontProduct product)
    {
        return product.PoMjeri
            ? (GetAvailableRemainingLength(product.Id) > 0 ? 1 : 0)
            : product.AvailableQuantity;
    }
}
