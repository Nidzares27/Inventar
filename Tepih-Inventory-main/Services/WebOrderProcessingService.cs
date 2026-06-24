using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Utils;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Services
{
    public class WebOrderProcessingService : IWebOrderProcessingService
    {
        private const string WebStoreSellerName = "WEB STORE";

        private readonly ApplicationDbContext _context;
        private readonly ILogger<WebOrderProcessingService> _logger;

        public WebOrderProcessingService(ApplicationDbContext context, ILogger<WebOrderProcessingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<WebOrderProcessingResult> ApplyStatusUpdateAsync(
            int orderId,
            string status,
            string paymentStatus,
            string fulfillmentStatus,
            string? note,
            string? internalNote,
            string changedBy,
            CancellationToken cancellationToken = default)
        {
            var order = await _context.WebOrders
                .Include(webOrder => webOrder.Items)
                .Include(webOrder => webOrder.Reservations)
                .FirstOrDefaultAsync(webOrder => webOrder.Id == orderId, cancellationToken);

            if (order == null)
            {
                return Failure("Web order not found.");
            }

            var originalStatus = order.Status;
            var originalPaymentStatus = order.PaymentStatus;
            var originalFulfillmentStatus = order.FulfillmentStatus;

            var activeReservations = order.Reservations
                .Where(reservation => reservation.Status == InventoryReservationStatuses.Active)
                .ToList();

            if (originalStatus == WebOrderStatuses.Cancelled && status != WebOrderStatuses.Cancelled)
            {
                return Failure("Cancelled orders cannot be re-opened automatically.");
            }

            if (originalStatus == WebOrderStatuses.Completed
                && status != WebOrderStatuses.Completed
                && status != WebOrderStatuses.Refunded)
            {
                return Failure("Completed orders can only remain completed or move to refunded.");
            }

            var utcNow = DateTime.UtcNow;
            changedBy = TextEncodingHelper.NormalizeInput(changedBy) ?? "Admin";
            note = TextEncodingHelper.NormalizeInput(note, trim: false);

            order.PaymentStatus = paymentStatus;
            order.InternalNote = string.IsNullOrWhiteSpace(internalNote)
                ? null
                : TextEncodingHelper.NormalizeInput(internalNote);

            if (paymentStatus == WebPaymentStatuses.Paid && order.PaidUtc == null)
            {
                order.PaidUtc = utcNow;
            }

            string? systemNote = null;
            if (status == WebOrderStatuses.Completed)
            {
                var completionResult = await CompleteOrderAsync(order, activeReservations, utcNow, cancellationToken);
                if (!completionResult.Succeeded)
                {
                    return completionResult;
                }

                systemNote = completionResult.Message == "Order already completed."
                    ? null
                    : completionResult.Message;
            }
            else if (status == WebOrderStatuses.Cancelled)
            {
                var cancellationResult = await CancelOrderAsync(order, activeReservations, utcNow, false, cancellationToken);
                if (!cancellationResult.Succeeded)
                {
                    return cancellationResult;
                }

                systemNote = cancellationResult.Message == "Order already cancelled."
                    ? null
                    : cancellationResult.Message;
            }
            else
            {
                order.Status = status;
                order.FulfillmentStatus = fulfillmentStatus;

                if (status == WebOrderStatuses.Refunded)
                {
                    order.PaymentStatus = paymentStatus == WebPaymentStatuses.Pending
                        ? WebPaymentStatuses.Refunded
                        : paymentStatus;
                }

                systemNote = null;
            }

            var statusChanged = originalStatus != order.Status;
            var paymentChanged = originalPaymentStatus != order.PaymentStatus;
            var fulfillmentChanged = originalFulfillmentStatus != order.FulfillmentStatus;

            AddStatusHistoryIfNeeded(
                order,
                changedBy,
                statusChanged,
                paymentChanged,
                fulfillmentChanged,
                CombineNotes(systemNote, note));

            await _context.SaveChangesAsync(cancellationToken);

            return status switch
            {
                WebOrderStatuses.Completed => Success("Storefront order completed."),
                WebOrderStatuses.Cancelled => Success("Storefront order cancelled."),
                _ => Success("Storefront order updated.")
            };
        }

        public async Task<int> ExpireReservationsAsync(CancellationToken cancellationToken = default)
        {
            var expiredOrderIds = await _context.InventoryReservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.Status == InventoryReservationStatuses.Active
                    && reservation.ExpiresUtc != null
                    && reservation.ExpiresUtc <= DateTime.UtcNow)
                .Select(reservation => reservation.WebOrderId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var expiredCount = 0;
            foreach (var orderId in expiredOrderIds)
            {
                var order = await _context.WebOrders
                    .Include(webOrder => webOrder.Reservations)
                    .FirstOrDefaultAsync(webOrder => webOrder.Id == orderId, cancellationToken);

                if (order == null || order.Status == WebOrderStatuses.Completed || order.Status == WebOrderStatuses.Cancelled)
                {
                    continue;
                }

                var originalStatus = order.Status;
                var originalPaymentStatus = order.PaymentStatus;
                var originalFulfillmentStatus = order.FulfillmentStatus;

                var activeReservations = order.Reservations
                    .Where(reservation =>
                        reservation.Status == InventoryReservationStatuses.Active
                        && reservation.ExpiresUtc != null
                        && reservation.ExpiresUtc <= DateTime.UtcNow)
                    .ToList();

                if (activeReservations.Count == 0)
                {
                    continue;
                }

                var cancellationResult = await CancelOrderAsync(
                    order,
                    activeReservations,
                    DateTime.UtcNow,
                    true,
                    cancellationToken);

                if (!cancellationResult.Succeeded)
                {
                    _logger.LogWarning("Failed to expire reservations for storefront order {OrderId}: {Reason}", orderId, cancellationResult.Message);
                    continue;
                }

                AddStatusHistoryIfNeeded(
                    order,
                    "SYSTEM",
                    originalStatus != order.Status,
                    originalPaymentStatus != order.PaymentStatus,
                    originalFulfillmentStatus != order.FulfillmentStatus,
                    cancellationResult.Message);

                await _context.SaveChangesAsync(cancellationToken);
                expiredCount += activeReservations.Count;
            }

            return expiredCount;
        }

        private async Task<WebOrderProcessingResult> CompleteOrderAsync(
            WebOrder order,
            List<InventoryReservation> activeReservations,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            if (activeReservations.Count == 0 && order.CompletedUtc != null)
            {
                order.Status = WebOrderStatuses.Completed;
                order.FulfillmentStatus = WebFulfillmentStatuses.Completed;
                return Success("Order already completed.");
            }

            if (activeReservations.Count == 0)
            {
                return Failure("Cannot complete an order without active reservations.");
            }

            var orderItemsById = order.Items.ToDictionary(item => item.Id);
            var poMjeriReservations = activeReservations
                .Where(reservation => IsPoMjeriReservation(reservation, orderItemsById))
                .ToList();
            var regularReservations = activeReservations
                .Where(reservation => !IsPoMjeriReservation(reservation, orderItemsById))
                .ToList();

            var reservedByProduct = regularReservations
                .GroupBy(reservation => reservation.TepihId)
                .ToDictionary(group => group.Key, group => group.Sum(reservation => reservation.Quantity));

            var orderedByProduct = order.Items
                .Where(item => !item.PoMjeri)
                .GroupBy(item => item.TepihId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

            if (reservedByProduct.Count != orderedByProduct.Count
                || reservedByProduct.Any(pair => !orderedByProduct.TryGetValue(pair.Key, out var orderedQuantity) || orderedQuantity != pair.Value))
            {
                return Failure("Active reservations no longer match the order items.");
            }

            var products = reservedByProduct.Count == 0
                ? new Dictionary<int, Tepih>()
                : await _context.Tepisi
                .Where(product => reservedByProduct.Keys.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            foreach (var (productId, reservedQuantity) in reservedByProduct)
            {
                if (!products.TryGetValue(productId, out var product))
                {
                    return Failure($"Product {productId} was not found while completing the order.");
                }

                if (product.Quantity < reservedQuantity)
                {
                    return Failure($"Not enough physical stock remains for product {product.Name}.");
                }

                if (product.ReservedQuantity < reservedQuantity)
                {
                    return Failure($"Reserved stock is inconsistent for product {product.Name}.");
                }
            }

            foreach (var reservation in regularReservations)
            {
                var product = products[reservation.TepihId];
                product.Quantity -= reservation.Quantity;
                product.ReservedQuantity -= reservation.Quantity;
                reservation.Status = InventoryReservationStatuses.Converted;
                reservation.ReleasedUtc = utcNow;
            }

            foreach (var reservation in poMjeriReservations)
            {
                reservation.Status = InventoryReservationStatuses.Converted;
                reservation.ReleasedUtc = utcNow;
            }

            var saleTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local);
            var customerName = Truncate($"{order.CustomerFirstName} {order.CustomerLastName}".Trim().ToUpperInvariant(), 50);
            var plannedPaymentType = Truncate(
                string.IsNullOrWhiteSpace(order.PaymentProvider) ? order.PaymentStatus : order.PaymentProvider!,
                20);

            foreach (var item in order.Items.Where(orderItem => !orderItem.PoMjeri))
            {
                _context.Prodaje.Add(new Prodaja
                {
                    TepihId = item.TepihId,
                    Quantity = item.Quantity,
                    CustomerFullName = customerName,
                    VrijemeProdaje = saleTime,
                    Price = item.UnitPrice,
                    PlannedPaymentType = plannedPaymentType,
                    Prodavac = WebStoreSellerName,
                    Disabled = false
                });
            }

            foreach (var reservation in poMjeriReservations)
            {
                if (!reservation.WebOrderItemId.HasValue ||
                    !orderItemsById.TryGetValue(reservation.WebOrderItemId.Value, out var poMjeriItem))
                {
                    return Failure("Po mjeri rezervacija više nije povezana sa stavkom narudžbine.");
                }

                _context.Prodaje.Add(new Prodaja
                {
                    TepihId = reservation.TepihId,
                    Quantity = reservation.Quantity,
                    CustomerFullName = customerName,
                    VrijemeProdaje = saleTime,
                    Price = poMjeriItem.UnitPrice,
                    CustomWidth = reservation.CutWidth,
                    CustomLength = reservation.CutLength,
                    ConsumedLength = reservation.ConsumedLengthPerUnit,
                    PlannedPaymentType = plannedPaymentType,
                    Prodavac = WebStoreSellerName,
                    Disabled = false
                });
            }

            order.Status = WebOrderStatuses.Completed;
            order.FulfillmentStatus = WebFulfillmentStatuses.Completed;
            order.CompletedUtc ??= utcNow;

            return Success("Converted active reservations into sales records.");
        }

        private async Task<WebOrderProcessingResult> CancelOrderAsync(
            WebOrder order,
            List<InventoryReservation> activeReservations,
            DateTime utcNow,
            bool expired,
            CancellationToken cancellationToken)
        {
            if (order.CompletedUtc != null)
            {
                return Failure("Completed orders cannot be cancelled automatically.");
            }

            if (activeReservations.Count == 0 && order.CancelledUtc != null)
            {
                order.Status = WebOrderStatuses.Cancelled;
                order.FulfillmentStatus = WebFulfillmentStatuses.Cancelled;
                return Success("Order already cancelled.");
            }

            var poMjeriReservationItemIds = order.Items
                .Where(item => item.PoMjeri)
                .Select(item => item.Id)
                .ToHashSet();

            var reservedByProduct = activeReservations
                .Where(reservation => !(reservation.WebOrderItemId.HasValue && poMjeriReservationItemIds.Contains(reservation.WebOrderItemId.Value)))
                .GroupBy(reservation => reservation.TepihId)
                .ToDictionary(group => group.Key, group => group.Sum(reservation => reservation.Quantity));

            var products = reservedByProduct.Count == 0
                ? new Dictionary<int, Tepih>()
                : await _context.Tepisi
                    .Where(product => reservedByProduct.Keys.Contains(product.Id))
                    .ToDictionaryAsync(product => product.Id, cancellationToken);

            foreach (var reservation in activeReservations)
            {
                if (!(reservation.WebOrderItemId.HasValue && poMjeriReservationItemIds.Contains(reservation.WebOrderItemId.Value))
                    && products.TryGetValue(reservation.TepihId, out var product))
                {
                    product.ReservedQuantity = Math.Max(product.ReservedQuantity - reservation.Quantity, 0);
                }

                reservation.Status = expired
                    ? InventoryReservationStatuses.Expired
                    : InventoryReservationStatuses.Released;
                reservation.ReleasedUtc = utcNow;
            }

            order.Status = WebOrderStatuses.Cancelled;
            order.FulfillmentStatus = WebFulfillmentStatuses.Cancelled;
            order.CancelledUtc ??= utcNow;

            return Success(expired
                ? "Reservations expired automatically."
                : "Released active reservations.");
        }

        private void AddStatusHistoryIfNeeded(
            WebOrder order,
            string changedBy,
            bool statusChanged,
            bool paymentChanged,
            bool fulfillmentChanged,
            string? note)
        {
            if (!statusChanged && !paymentChanged && !fulfillmentChanged && string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            var noteParts = new List<string>();
            if (paymentChanged)
            {
                noteParts.Add($"Payment: {order.PaymentStatus}");
            }

            if (fulfillmentChanged)
            {
                noteParts.Add($"Fulfillment: {order.FulfillmentStatus}");
            }

            if (!string.IsNullOrWhiteSpace(note))
            {
                noteParts.Add(TextEncodingHelper.NormalizeInput(note) ?? note.Trim());
            }

            AddStatusHistory(order, changedBy, order.Status, noteParts.Count == 0 ? null : string.Join(" | ", noteParts));
        }

        private void AddStatusHistory(WebOrder order, string changedBy, string status, string? note)
        {
            _context.WebOrderStatusHistory.Add(new WebOrderStatusHistory
            {
                WebOrderId = order.Id,
                Status = status,
                ChangedBy = TextEncodingHelper.NormalizeInput(changedBy) ?? changedBy,
                Note = TextEncodingHelper.NormalizeInput(note, trim: false),
                ChangedUtc = DateTime.UtcNow
            });
        }

        private static string? CombineNotes(string? first, string? second)
        {
            first = TextEncodingHelper.NormalizeInput(first, trim: false);
            second = TextEncodingHelper.NormalizeInput(second, trim: false);
            return (string.IsNullOrWhiteSpace(first), string.IsNullOrWhiteSpace(second)) switch
            {
                (true, true) => null,
                (false, true) => first!.Trim(),
                (true, false) => second!.Trim(),
                _ => $"{first!.Trim()} | {second!.Trim()}"
            };
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static bool IsPoMjeriReservation(
            InventoryReservation reservation,
            IReadOnlyDictionary<int, WebOrderItem> orderItemsById)
        {
            return reservation.WebOrderItemId.HasValue
                && orderItemsById.TryGetValue(reservation.WebOrderItemId.Value, out var orderItem)
                && orderItem.PoMjeri;
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
    }
}
