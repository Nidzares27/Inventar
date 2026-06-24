using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Utils;
using Inventar.ViewModels.StorefrontAdmin;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Services;

public class StorefrontOrderAdminService
{
    private const int MaxOrderQuantityPerItem = int.MaxValue;
    private const string OrderEditedReason = "Order items edited by admin";

    private readonly ApplicationDbContext _context;

    public StorefrontOrderAdminService(ApplicationDbContext context)
    {
        _context = context;
    }

    public static bool CanEditItems(WebOrder order)
    {
        return order.Status != WebOrderStatuses.Completed
            && order.Status != WebOrderStatuses.Cancelled
            && order.Status != WebOrderStatuses.Refunded;
    }

    public async Task<IReadOnlyList<StorefrontOrderEditableLineViewModel>> BuildEditableLinesAsync(
        WebOrder order,
        IReadOnlyList<WebOrderItem> items,
        IReadOnlyList<InventoryReservation> reservations,
        CancellationToken cancellationToken = default)
    {
        if (!CanEditItems(order) || items.Count == 0)
        {
            return Array.Empty<StorefrontOrderEditableLineViewModel>();
        }

        var productIds = items.Select(item => item.TepihId).Distinct().ToList();
        var products = await _context.Tepisi
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        var currentItemIds = items.Select(item => item.Id).ToHashSet();
        var currentRegularReservedByProduct = reservations
            .Where(reservation =>
                reservation.Status == InventoryReservationStatuses.Active &&
                reservation.WebOrderItemId.HasValue &&
                currentItemIds.Contains(reservation.WebOrderItemId.Value))
            .Join(
                items.Where(item => !item.PoMjeri),
                reservation => reservation.WebOrderItemId!.Value,
                item => item.Id,
                (reservation, item) => reservation)
            .GroupBy(reservation => reservation.TepihId)
            .ToDictionary(group => group.Key, group => group.Sum(reservation => reservation.Quantity));

        var result = new List<StorefrontOrderEditableLineViewModel>(items.Count);

        foreach (var item in items.OrderBy(item => item.Id))
        {
            products.TryGetValue(item.TepihId, out var product);

            var editableLine = new StorefrontOrderEditableLineViewModel
            {
                ExistingItemId = item.Id,
                ProductId = item.TepihId,
                ProductNumber = item.ProductNumber,
                ProductName = item.ProductName,
                Model = item.Model,
                Color = item.Color,
                Width = item.Width,
                Length = item.Length,
                PerM2 = item.PerM2,
                PoMjeri = item.PoMjeri,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal,
                PrimaryImageUrl = item.PrimaryImageUrl
            };

            if (item.PoMjeri && item.Width.HasValue && item.Length.HasValue && !string.IsNullOrWhiteSpace(item.Color))
            {
                var preview = await PreviewPoMjeriSelectionAsync(
                    order.Id,
                    item.TepihId,
                    item.Width.Value,
                    item.Length.Value,
                    item.Quantity,
                    cancellationToken);

                editableLine.MaxQuantity = Math.Max(item.Quantity, preview.MaxAvailableQuantity);
                editableLine.MaxQuantityMessage = preview.Message;
            }
            else
            {
                var maxQuantity = item.Quantity;
                if (product != null)
                {
                    var reservedByThisOrder = currentRegularReservedByProduct.GetValueOrDefault(product.Id);
                    var availableForThisOrder = Math.Max(product.Quantity - (product.ReservedQuantity - reservedByThisOrder), 0);
                    maxQuantity = Math.Max(item.Quantity, availableForThisOrder);
                }

                editableLine.MaxQuantity = maxQuantity;
                editableLine.MaxQuantityMessage = maxQuantity <= 1
                    ? "Dostupan je najvise 1 komad."
                    : $"Moguce je naruciti najvise {maxQuantity} komada.";
            }

            result.Add(editableLine);
        }

        return result;
    }

    public async Task<StorefrontOrderPoMjeriPreviewResult> PreviewPoMjeriSelectionAsync(
        int webOrderId,
        int productId,
        int customWidth,
        int customLength,
        int requestedQuantity,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Tepisi
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == productId, cancellationToken);

        if (product == null)
        {
            return StorefrontOrderPoMjeriPreviewResult.Invalid("Proizvod nije pronadjen.");
        }

        return await PreviewPoMjeriSelectionAsync(
            webOrderId,
            product,
            customWidth,
            customLength,
            requestedQuantity,
            null,
            cancellationToken);
    }

    public async Task<WebOrderProcessingResult> DeleteOrderAsync(
        int orderId,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.WebOrders
            .Include(webOrder => webOrder.Items)
            .Include(webOrder => webOrder.Reservations)
            .FirstOrDefaultAsync(webOrder => webOrder.Id == orderId, cancellationToken);

        if (order == null)
        {
            return Failure("Web narudzbina nije pronadjena.");
        }

        if (order.Status == WebOrderStatuses.Completed || order.Status == WebOrderStatuses.Refunded)
        {
            return Failure("Zavrsene ili refundirane narudzbine nije moguce obrisati.");
        }

        var currentItemsById = order.Items.ToDictionary(item => item.Id);
        var regularReservationProducts = order.Reservations
            .Where(reservation =>
                reservation.Status == InventoryReservationStatuses.Active &&
                reservation.WebOrderItemId.HasValue &&
                currentItemsById.TryGetValue(reservation.WebOrderItemId.Value, out var item) &&
                !item.PoMjeri)
            .GroupBy(reservation => reservation.TepihId)
            .ToDictionary(group => group.Key, group => group.Sum(reservation => reservation.Quantity));

        if (regularReservationProducts.Count > 0)
        {
            var products = await _context.Tepisi
                .Where(product => regularReservationProducts.Keys.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            foreach (var (productId, quantity) in regularReservationProducts)
            {
                if (products.TryGetValue(productId, out var product))
                {
                    product.ReservedQuantity = Math.Max(product.ReservedQuantity - quantity, 0);
                }
            }
        }

        _context.WebOrders.Remove(order);
        await _context.SaveChangesAsync(cancellationToken);

        return Success("Web narudzbina je uspjesno obrisana.");
    }

    public async Task<WebOrderProcessingResult> ApplyItemEditsAsync(
        int orderId,
        IReadOnlyCollection<StorefrontOrderItemEditInputViewModel>? items,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.WebOrders
            .Include(webOrder => webOrder.Items)
            .Include(webOrder => webOrder.Reservations)
            .FirstOrDefaultAsync(webOrder => webOrder.Id == orderId, cancellationToken);

        if (order == null)
        {
            return Failure("Web narudzbina nije pronadjena.");
        }

        if (!CanEditItems(order))
        {
            return Failure("Stavke se mogu uredjivati samo za aktivne narudzbine.");
        }

        var normalizedInputs = NormalizeInputs(items);
        if (normalizedInputs.Count == 0)
        {
            return Failure("Narudzbina mora imati barem jednu stavku.");
        }

        var existingItemsById = order.Items.ToDictionary(item => item.Id);
        var allRelevantProductIds = normalizedInputs.Select(item => item.ProductId)
            .Concat(order.Items.Select(item => item.TepihId))
            .Distinct()
            .ToList();

        var products = await _context.Tepisi
            .Include(product => product.ProductImages)
            .Where(product => allRelevantProductIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        var currentItemIds = order.Items.Select(item => item.Id).ToHashSet();
        var currentRegularReservedByProduct = order.Reservations
            .Where(reservation =>
                reservation.Status == InventoryReservationStatuses.Active &&
                reservation.WebOrderItemId.HasValue &&
                currentItemIds.Contains(reservation.WebOrderItemId.Value))
            .Join(
                order.Items.Where(item => !item.PoMjeri),
                reservation => reservation.WebOrderItemId!.Value,
                item => item.Id,
                (reservation, item) => reservation)
            .GroupBy(reservation => reservation.TepihId)
            .ToDictionary(group => group.Key, group => group.Sum(reservation => reservation.Quantity));

        var plannedRegularItems = new List<PlannedRegularOrderItem>();
        var plannedPoMjeriItems = new List<PlannedPoMjeriOrderItem>();

        var provisionalConsumedLengths = new Dictionary<int, int>();

        foreach (var input in normalizedInputs.Where(item => !item.PoMjeri))
        {
            if (!products.TryGetValue(input.ProductId, out var product))
            {
                return Failure("Jedan od odabranih proizvoda vise ne postoji.");
            }

            var isExistingItem = input.ExistingItemId.HasValue && existingItemsById.ContainsKey(input.ExistingItemId.Value);
            if (!isExistingItem && (product.Disabled || !product.IsPublished))
            {
                return Failure($"Proizvod {product.Name} vise nije dostupan za dodavanje u narudzbinu.");
            }

            var reservedByThisOrder = currentRegularReservedByProduct.GetValueOrDefault(product.Id);
            var availableForThisOrder = Math.Max(product.Quantity - (product.ReservedQuantity - reservedByThisOrder), 0);
            var maxQuantity = availableForThisOrder;

            if (input.Quantity > maxQuantity)
            {
                return Failure($"Za proizvod {product.Name} moguce je naruciti najvise {maxQuantity} komada.");
            }

            var effectivePrice = product.OnlinePrice ?? product.Price;
            var unitPrice = InventoryPricingHelper.CalculateLineTotal(
                product.PerM2,
                false,
                effectivePrice,
                product.Width,
                product.Length,
                1);
            var lineTotal = unitPrice * input.Quantity;
            var primaryImage = product.ProductImages
                .Where(image => !image.Disabled && !string.Equals(image.MediaType, "video", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder)
                .FirstOrDefault();

            plannedRegularItems.Add(new PlannedRegularOrderItem
            {
                Product = product,
                Input = input,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
                PrimaryImageUrl = primaryImage?.Url
            });
        }

        var poMjeriInputs = normalizedInputs
            .Where(item => item.PoMjeri)
            .OrderByDescending(item => (item.Width ?? 0) * (item.Length ?? 0))
            .ThenByDescending(item => item.Quantity)
            .ToList();

        foreach (var input in poMjeriInputs)
        {
            if (!products.TryGetValue(input.ProductId, out var product))
            {
                return Failure("Jedan od po mjeri proizvoda vise ne postoji.");
            }

            var isExistingItem = input.ExistingItemId.HasValue && existingItemsById.ContainsKey(input.ExistingItemId.Value);
            if (!isExistingItem && (product.Disabled || !product.IsPublished))
            {
                return Failure($"Po mjeri proizvod {product.Name} vise nije dostupan za dodavanje.");
            }

            if (!input.Width.HasValue || !input.Length.HasValue)
            {
                return Failure($"Po mjeri stavka {product.Name} nema ispravne dimenzije.");
            }

            var preview = await PreviewPoMjeriSelectionAsync(
                order.Id,
                product,
                input.Width.Value,
                input.Length.Value,
                input.Quantity,
                provisionalConsumedLengths,
                cancellationToken);

            if (!preview.IsValid || preview.BestPlan == null)
            {
                return Failure($"{product.Name}: {preview.Message}");
            }

            foreach (var slice in preview.BestPlan.Slices)
            {
                provisionalConsumedLengths[slice.ProductId] =
                    provisionalConsumedLengths.GetValueOrDefault(slice.ProductId) +
                    (slice.Quantity * slice.ConsumedLengthPerUnit);
            }

            var effectivePrice = product.OnlinePrice ?? product.Price;
            var unitPrice = InventoryPricingHelper.CalculateLineTotal(
                true,
                true,
                effectivePrice,
                input.Width,
                input.Length,
                1);
            var lineTotal = unitPrice * input.Quantity;
            var primaryImage = product.ProductImages
                .Where(image => !image.Disabled && !string.Equals(image.MediaType, "video", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder)
                .FirstOrDefault();

            plannedPoMjeriItems.Add(new PlannedPoMjeriOrderItem
            {
                Product = product,
                Input = input,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
                PrimaryImageUrl = primaryImage?.Url,
                Plan = preview.BestPlan
            });
        }

        var utcNow = DateTime.UtcNow;
        var currentItemIdSet = order.Items.Select(item => item.Id).ToHashSet();
        var earliestExpiresUtc = order.Reservations
            .Where(reservation => reservation.Status == InventoryReservationStatuses.Active && reservation.ExpiresUtc.HasValue)
            .Select(reservation => reservation.ExpiresUtc)
            .OrderBy(value => value)
            .FirstOrDefault()
            ?? utcNow.AddHours(48);

        foreach (var reservation in order.Reservations.Where(reservation =>
                     reservation.Status == InventoryReservationStatuses.Active &&
                     reservation.WebOrderItemId.HasValue &&
                     currentItemIdSet.Contains(reservation.WebOrderItemId.Value)))
        {
            if (existingItemsById.TryGetValue(reservation.WebOrderItemId!.Value, out var existingItem) &&
                !existingItem.PoMjeri &&
                products.TryGetValue(reservation.TepihId, out var product))
            {
                product.ReservedQuantity = Math.Max(product.ReservedQuantity - reservation.Quantity, 0);
            }

            reservation.Status = InventoryReservationStatuses.Released;
            reservation.ReleasedUtc = utcNow;
            reservation.Reason = OrderEditedReason;
            reservation.WebOrderItemId = null;
        }

        foreach (var reservation in order.Reservations.Where(reservation =>
                     reservation.WebOrderItemId.HasValue &&
                     currentItemIdSet.Contains(reservation.WebOrderItemId.Value)))
        {
            reservation.WebOrderItemId = null;
        }

        _context.WebOrderItems.RemoveRange(order.Items.ToList());
        order.Items.Clear();

        order.ItemsTotal = 0m;

        foreach (var plannedItem in plannedRegularItems)
        {
            var orderItem = new WebOrderItem
            {
                TepihId = plannedItem.Product.Id,
                ProductName = plannedItem.Product.Name,
                ProductNumber = plannedItem.Product.ProductNumber,
                Model = plannedItem.Product.Model,
                Color = plannedItem.Product.Color,
                Width = plannedItem.Product.Width,
                Length = plannedItem.Product.Length,
                PerM2 = plannedItem.Product.PerM2,
                PoMjeri = false,
                Quantity = plannedItem.Input.Quantity,
                UnitPrice = plannedItem.UnitPrice,
                LineTotal = plannedItem.LineTotal,
                PrimaryImageUrl = plannedItem.PrimaryImageUrl
            };

            order.Items.Add(orderItem);
            order.Reservations.Add(new InventoryReservation
            {
                WebOrderItem = orderItem,
                TepihId = plannedItem.Product.Id,
                Quantity = plannedItem.Input.Quantity,
                Status = InventoryReservationStatuses.Active,
                CreatedUtc = utcNow,
                ExpiresUtc = earliestExpiresUtc,
                Reason = OrderEditedReason
            });

            plannedItem.Product.ReservedQuantity += plannedItem.Input.Quantity;
            order.ItemsTotal += plannedItem.LineTotal;
        }

        foreach (var plannedItem in plannedPoMjeriItems)
        {
            var orderItem = new WebOrderItem
            {
                TepihId = plannedItem.Product.Id,
                ProductName = plannedItem.Product.Name,
                ProductNumber = plannedItem.Product.ProductNumber,
                Model = plannedItem.Product.Model,
                Color = plannedItem.Product.Color,
                Width = plannedItem.Input.Width,
                Length = plannedItem.Input.Length,
                PerM2 = true,
                PoMjeri = true,
                Quantity = plannedItem.Input.Quantity,
                UnitPrice = plannedItem.UnitPrice,
                LineTotal = plannedItem.LineTotal,
                PrimaryImageUrl = plannedItem.PrimaryImageUrl
            };

            order.Items.Add(orderItem);

            foreach (var slice in plannedItem.Plan.Slices)
            {
                order.Reservations.Add(new InventoryReservation
                {
                    WebOrderItem = orderItem,
                    TepihId = slice.ProductId,
                    Quantity = slice.Quantity,
                    CutWidth = plannedItem.Input.Width,
                    CutLength = plannedItem.Input.Length,
                    ConsumedLengthPerUnit = slice.ConsumedLengthPerUnit,
                    Status = InventoryReservationStatuses.Active,
                    CreatedUtc = utcNow,
                    ExpiresUtc = earliestExpiresUtc,
                    Reason = OrderEditedReason
                });
            }

            order.ItemsTotal += plannedItem.LineTotal;
        }

        order.ShippingTotal = order.Items.Count == 0 ? 0m : order.ShippingTotal;
        order.GrandTotal = order.ItemsTotal + order.ShippingTotal - order.DiscountTotal;

        _context.WebOrderStatusHistory.Add(new WebOrderStatusHistory
        {
            WebOrderId = order.Id,
            Status = order.Status,
            ChangedBy = changedBy,
            Note = "Order items have been updated by admin.",
            ChangedUtc = utcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Success("Stavke narudzbine su uspjesno azurirane.");
    }

    private async Task<StorefrontOrderPoMjeriPreviewResult> PreviewPoMjeriSelectionAsync(
        int webOrderId,
        Tepih product,
        int customWidth,
        int customLength,
        int requestedQuantity,
        Dictionary<int, int>? provisionalConsumedLengths,
        CancellationToken cancellationToken)
    {
        if (!product.PoMjeri || !product.Width.HasValue || !product.Length.HasValue)
        {
            return StorefrontOrderPoMjeriPreviewResult.Invalid("Izabrani proizvod nije po mjeri.");
        }

        if (customWidth <= 0 || customLength <= 0)
        {
            return StorefrontOrderPoMjeriPreviewResult.Invalid("Unesite zeljenu sirinu i duzinu.");
        }

        if (product.Width != customWidth)
        {
            return StorefrontOrderPoMjeriPreviewResult.Invalid($"Sirina za dati proizvod mora biti {product.Width}.");
        }

        if (requestedQuantity < 1)
        {
            return StorefrontOrderPoMjeriPreviewResult.Invalid("Kolicina mora biti najmanje 1.");
        }

        var groupProducts = await _context.Tepisi
            .AsNoTracking()
            .Where(item =>
                !item.Disabled &&
                item.PoMjeri &&
                item.Name == product.Name &&
                item.ProductNumber == product.ProductNumber &&
                item.Model == product.Model &&
                item.Color == product.Color &&
                item.Width == customWidth &&
                item.Width.HasValue &&
                item.Length.HasValue)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        if (groupProducts.Count == 0)
        {
            return StorefrontOrderPoMjeriPreviewResult.Invalid("Nema dostupnih po mjeri instanci za ovaj proizvod.");
        }

        var availableLengths = await LoadAvailablePoMjeriLengthsAsync(
            groupProducts,
            webOrderId,
            provisionalConsumedLengths,
            cancellationToken);

        var candidates = new List<PoMjeriAllocationPlannerCandidate>();
        foreach (var candidateProduct in groupProducts)
        {
            var remainingWidth = candidateProduct.Width ?? 0;
            var remainingLength = availableLengths.GetValueOrDefault(candidateProduct.Id);

            if (customWidth != remainingWidth || customLength > remainingLength)
            {
                continue;
            }

            var consumedLengthPerUnit = customLength;
            var maxAvailableQuantity = Math.Max(remainingLength / customLength, 0);

            if (maxAvailableQuantity < 1)
            {
                continue;
            }

            candidates.Add(new PoMjeriAllocationPlannerCandidate(
                candidateProduct.Id,
                remainingWidth,
                remainingLength,
                consumedLengthPerUnit,
                maxAvailableQuantity));
        }

        var evaluation = PoMjeriAllocationPlanner.Evaluate(
            candidates,
            requestedQuantity,
            MaxOrderQuantityPerItem);

        return new StorefrontOrderPoMjeriPreviewResult
        {
            IsValid = evaluation.IsValid,
            Message = evaluation.Message,
            MaxAvailableQuantity = evaluation.MaxAvailableQuantity,
            BestPlan = evaluation.BestPlan
        };
    }

    private async Task<Dictionary<int, int>> LoadAvailablePoMjeriLengthsAsync(
        IReadOnlyList<Tepih> products,
        int webOrderId,
        Dictionary<int, int>? provisionalConsumedLengths,
        CancellationToken cancellationToken)
    {
        var productIds = products.Select(product => product.Id).ToList();

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
                reservation.WebOrderId != webOrderId &&
                reservation.ConsumedLengthPerUnit.HasValue)
            .GroupBy(reservation => reservation.TepihId)
            .Select(group => new
            {
                TepihId = group.Key,
                ConsumedLength = group.Sum(reservation => reservation.Quantity * (reservation.ConsumedLengthPerUnit ?? 0))
            })
            .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength, cancellationToken);

        return products.ToDictionary(
            product => product.Id,
            product =>
            {
                var originalLength = product.Length ?? 0;
                var usedLength =
                    soldLengths.GetValueOrDefault(product.Id) +
                    reservedLengths.GetValueOrDefault(product.Id) +
                    (provisionalConsumedLengths?.GetValueOrDefault(product.Id) ?? 0);

                return Math.Max(originalLength - usedLength, 0);
            });
    }

    private static List<StorefrontOrderItemEditInputViewModel> NormalizeInputs(
        IReadOnlyCollection<StorefrontOrderItemEditInputViewModel>? items)
    {
        var normalized = new List<StorefrontOrderItemEditInputViewModel>();
        if (items == null)
        {
            return normalized;
        }

        foreach (var item in items.Where(item => item.ProductId > 0 && item.Quantity > 0))
        {
            var matchingItem = normalized.FirstOrDefault(existing =>
                existing.ProductId == item.ProductId &&
                existing.PoMjeri == item.PoMjeri &&
                string.Equals(existing.Color, item.Color, StringComparison.OrdinalIgnoreCase) &&
                existing.Width == item.Width &&
                existing.Length == item.Length);

            if (matchingItem == null)
            {
                normalized.Add(new StorefrontOrderItemEditInputViewModel
                {
                    ExistingItemId = item.ExistingItemId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    PoMjeri = item.PoMjeri,
                    PerM2 = item.PerM2,
                    Color = item.Color,
                    Width = item.Width,
                    Length = item.Length
                });
                continue;
            }

            matchingItem.Quantity += item.Quantity;
            matchingItem.ExistingItemId ??= item.ExistingItemId;
        }

        return normalized;
    }

    private static WebOrderProcessingResult Success(string message)
    {
        return new WebOrderProcessingResult
        {
            Succeeded = true,
            Message = message
        };
    }

    private static WebOrderProcessingResult Failure(string message)
    {
        return new WebOrderProcessingResult
        {
            Succeeded = false,
            Message = message
        };
    }

    private sealed class PlannedRegularOrderItem
    {
        public Tepih Product { get; set; } = null!;
        public StorefrontOrderItemEditInputViewModel Input { get; set; } = null!;
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public string? PrimaryImageUrl { get; set; }
    }

    private sealed class PlannedPoMjeriOrderItem
    {
        public Tepih Product { get; set; } = null!;
        public StorefrontOrderItemEditInputViewModel Input { get; set; } = null!;
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public string? PrimaryImageUrl { get; set; }
        public PoMjeriAllocationPlannerPlan Plan { get; set; } = null!;
    }
}

public sealed class StorefrontOrderPoMjeriPreviewResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
    public int MaxAvailableQuantity { get; init; }
    public PoMjeriAllocationPlannerPlan? BestPlan { get; init; }

    public static StorefrontOrderPoMjeriPreviewResult Invalid(string message)
    {
        return new StorefrontOrderPoMjeriPreviewResult
        {
            IsValid = false,
            Message = message
        };
    }
}
