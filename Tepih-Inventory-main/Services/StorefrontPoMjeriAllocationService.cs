using Inventar.Data;
using Inventar.Models;
using Inventar.Utils;
using Inventar.ViewModels.StorefrontAdmin;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Services;

public class StorefrontPoMjeriAllocationService
{
    private const string AllocationAdjustedReason = "Allocation adjusted by admin";

    private readonly ApplicationDbContext _context;

    public StorefrontPoMjeriAllocationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PoMjeriAllocationCandidateViewModel>> BuildCandidateOptionsAsync(
        WebOrderItem item,
        IReadOnlyDictionary<int, int>? currentAllocationQuantities = null,
        CancellationToken cancellationToken = default)
    {
        if (!item.PoMjeri || !item.Width.HasValue || !item.Length.HasValue || string.IsNullOrWhiteSpace(item.Color))
        {
            return Array.Empty<PoMjeriAllocationCandidateViewModel>();
        }

        var candidates = await LoadCandidatesAsync(item, excludeWebOrderItemId: item.Id, cancellationToken);

        return candidates
            .Select(candidate => new PoMjeriAllocationCandidateViewModel
            {
                ProductId = candidate.ProductId,
                UnID = candidate.UnID,
                OriginalWidth = candidate.OriginalWidth,
                OriginalLength = candidate.OriginalLength,
                RemainingWidth = candidate.RemainingWidth,
                RemainingLength = candidate.RemainingLength,
                ConsumedLengthPerUnit = candidate.ConsumedLengthPerUnit,
                MaxAvailableQuantity = candidate.MaxAvailableQuantity,
                CurrentAllocatedQuantity = currentAllocationQuantities?.GetValueOrDefault(candidate.ProductId) ?? 0
            })
            .ToList();
    }

    public async Task<PoMjeriAllocationApplyResult> ReplaceAllocationsAsync(
        int webOrderId,
        int webOrderItemId,
        IReadOnlyCollection<PoMjeriAllocationEntryInputViewModel>? entries,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.WebOrders
            .Include(item => item.Items)
            .Include(item => item.Reservations)
            .FirstOrDefaultAsync(item => item.Id == webOrderId, cancellationToken);

        if (order == null)
        {
            return PoMjeriAllocationApplyResult.Failure("Web narudzbina nije pronadjena.");
        }

        if (order.Status == WebOrderStatuses.Completed || order.Status == WebOrderStatuses.Cancelled)
        {
            return PoMjeriAllocationApplyResult.Failure("Rezervacije mogu da se mijenjaju samo za aktivne narudzbine.");
        }

        var orderItem = order.Items.FirstOrDefault(item => item.Id == webOrderItemId);
        if (orderItem == null || !orderItem.PoMjeri || !orderItem.Width.HasValue || !orderItem.Length.HasValue)
        {
            return PoMjeriAllocationApplyResult.Failure("Po mjeri stavka nije pronadjena.");
        }

        var normalizedEntries = (entries ?? Array.Empty<PoMjeriAllocationEntryInputViewModel>())
            .Where(entry => entry.ProductId > 0 && entry.Quantity > 0)
            .GroupBy(entry => entry.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(entry => entry.Quantity)
            })
            .OrderBy(entry => entry.ProductId)
            .ToList();

        if (normalizedEntries.Count == 0)
        {
            return PoMjeriAllocationApplyResult.Failure("Potrebno je odabrati barem jednu izvoru instancu.");
        }

        var requestedTotalQuantity = normalizedEntries.Sum(entry => entry.Quantity);
        if (requestedTotalQuantity != orderItem.Quantity)
        {
            return PoMjeriAllocationApplyResult.Failure(
                $"Ukupan broj odabranih komada mora biti tacno {orderItem.Quantity}.");
        }

        var candidates = await LoadCandidatesAsync(orderItem, excludeWebOrderItemId: orderItem.Id, cancellationToken);
        var candidateByProductId = candidates.ToDictionary(candidate => candidate.ProductId);

        var slices = new List<PoMjeriAllocationSlice>();
        foreach (var entry in normalizedEntries)
        {
            if (!candidateByProductId.TryGetValue(entry.ProductId, out var candidate))
            {
                return PoMjeriAllocationApplyResult.Failure("Jedna od odabranih instanci vise nije dostupna za ovu alokaciju.");
            }

            if (entry.Quantity > candidate.MaxAvailableQuantity)
            {
                return PoMjeriAllocationApplyResult.Failure(
                    $"Instanca {candidate.UnID} moze da pokrije najvise {candidate.MaxAvailableQuantity} komada za trazene dimenzije.");
            }

            slices.Add(new PoMjeriAllocationSlice(
                candidate.ProductId,
                entry.Quantity,
                candidate.ConsumedLengthPerUnit));
        }

        var activeReservations = order.Reservations
            .Where(reservation =>
                reservation.WebOrderItemId == webOrderItemId &&
                reservation.Status == InventoryReservationStatuses.Active)
            .ToList();

        var utcNow = DateTime.UtcNow;
        var expiresUtc = activeReservations
            .Where(reservation => reservation.ExpiresUtc.HasValue)
            .Select(reservation => reservation.ExpiresUtc)
            .OrderBy(value => value)
            .FirstOrDefault()
            ?? order.Reservations
                .Where(reservation =>
                    reservation.WebOrderItemId != webOrderItemId &&
                    reservation.Status == InventoryReservationStatuses.Active &&
                    reservation.ExpiresUtc.HasValue)
                .Select(reservation => reservation.ExpiresUtc)
                .OrderBy(value => value)
                .FirstOrDefault()
            ?? utcNow.AddHours(48);

        foreach (var reservation in activeReservations)
        {
            reservation.Status = InventoryReservationStatuses.Released;
            reservation.ReleasedUtc = utcNow;
            reservation.Reason = AllocationAdjustedReason;
        }

        foreach (var slice in slices)
        {
            order.Reservations.Add(new InventoryReservation
            {
                WebOrderItemId = orderItem.Id,
                TepihId = slice.ProductId,
                Quantity = slice.Quantity,
                CutWidth = orderItem.Width,
                CutLength = orderItem.Length,
                ConsumedLengthPerUnit = slice.ConsumedLengthPerUnit,
                Status = InventoryReservationStatuses.Active,
                CreatedUtc = utcNow,
                ExpiresUtc = expiresUtc,
                Reason = AllocationAdjustedReason
            });
        }

        _context.WebOrderStatusHistory.Add(new WebOrderStatusHistory
        {
            WebOrderId = order.Id,
            Status = order.Status,
            ChangedBy = changedBy,
            Note = $"Adjusted po mjeri allocation for {orderItem.ProductName}.",
            ChangedUtc = utcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return PoMjeriAllocationApplyResult.Success("Alokacija je uspjesno azurirana.");
    }

    private async Task<List<PoMjeriCandidate>> LoadCandidatesAsync(
        WebOrderItem item,
        int excludeWebOrderItemId,
        CancellationToken cancellationToken)
    {
        var products = await _context.Tepisi
            .AsNoTracking()
            .Where(product =>
                !product.Disabled &&
                product.PoMjeri &&
                product.Name == item.ProductName &&
                product.ProductNumber == item.ProductNumber &&
                product.Model == item.Model &&
                product.Color == item.Color &&
                product.Width == item.Width &&
                product.Width.HasValue &&
                product.Length.HasValue)
            .ToListAsync(cancellationToken);

        var productIds = products.Select(product => product.Id).ToList();
        if (productIds.Count == 0)
        {
            return new List<PoMjeriCandidate>();
        }

        var soldLengths = await _context.Prodaje
            .AsNoTracking()
            .Where(sale => productIds.Contains(sale.TepihId) && !sale.Disabled)
            .GroupBy(sale => sale.TepihId)
            .Select(group => new
            {
                TepihId = group.Key,
                ConsumedLength = group.Sum(sale => (sale.ConsumedLength ?? sale.CustomLength ?? 0) * sale.Quantity)
            })
            .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength, cancellationToken);

        var reservedLengths = await _context.InventoryReservations
            .AsNoTracking()
            .Where(reservation =>
                productIds.Contains(reservation.TepihId) &&
                reservation.Status == InventoryReservationStatuses.Active &&
                reservation.WebOrderItemId != excludeWebOrderItemId &&
                reservation.ConsumedLengthPerUnit.HasValue)
            .GroupBy(reservation => reservation.TepihId)
            .Select(group => new
            {
                TepihId = group.Key,
                ConsumedLength = group.Sum(reservation => reservation.Quantity * (reservation.ConsumedLengthPerUnit ?? 0))
            })
            .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength, cancellationToken);

        return products
            .Select(product =>
            {
                var originalWidth = product.Width ?? 0;
                var originalLength = product.Length ?? 0;
                var remainingWidth = originalWidth;
                var remainingLength = Math.Max(
                    originalLength -
                    soldLengths.GetValueOrDefault(product.Id) -
                    reservedLengths.GetValueOrDefault(product.Id),
                    0);

                var consumedLengthPerUnit = item.Length!.Value;
                var maxAvailableQuantity = Math.Max(remainingLength / item.Length.Value, 0);

                return new PoMjeriCandidate(
                    product.Id,
                    product.UnID ?? string.Empty,
                    originalWidth,
                    originalLength,
                    remainingWidth,
                    remainingLength,
                    consumedLengthPerUnit,
                    maxAvailableQuantity);
            })
            .Where(candidate =>
                candidate.RemainingLength > 0 &&
                item.Width!.Value == candidate.RemainingWidth &&
                item.Length!.Value <= candidate.RemainingLength &&
                candidate.MaxAvailableQuantity > 0)
            .OrderBy(candidate => candidate.RemainingWidth * candidate.RemainingLength)
            .ThenBy(candidate => candidate.RemainingWidth)
            .ThenBy(candidate => candidate.RemainingLength)
            .ThenBy(candidate => candidate.ProductId)
            .ToList();
    }

    private sealed record PoMjeriCandidate(
        int ProductId,
        string UnID,
        int OriginalWidth,
        int OriginalLength,
        int RemainingWidth,
        int RemainingLength,
        int ConsumedLengthPerUnit,
        int MaxAvailableQuantity);

    private sealed record PoMjeriAllocationSlice(
        int ProductId,
        int Quantity,
        int ConsumedLengthPerUnit);
}

public sealed class PoMjeriAllocationApplyResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;

    public static PoMjeriAllocationApplyResult Success(string message) => new()
    {
        Succeeded = true,
        Message = message
    };

    public static PoMjeriAllocationApplyResult Failure(string message) => new()
    {
        Succeeded = false,
        Message = message
    };
}
