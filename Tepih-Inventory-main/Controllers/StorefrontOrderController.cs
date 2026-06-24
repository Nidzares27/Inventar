using System.Security.Claims;
using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.Services;
using Inventar.Utils;
using Inventar.ViewModels.StorefrontAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Controllers
{
    [Authorize(Roles = "admin,superadmin,employee")]
    public class StorefrontOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebOrderProcessingService _orderProcessingService;
        private readonly StorefrontPoMjeriAllocationService _poMjeriAllocationService;
        private readonly StorefrontOrderAdminService _orderAdminService;
        private readonly ILogger<StorefrontOrderController> _logger;

        public StorefrontOrderController(
            ApplicationDbContext context,
            IWebOrderProcessingService orderProcessingService,
            StorefrontPoMjeriAllocationService poMjeriAllocationService,
            StorefrontOrderAdminService orderAdminService,
            ILogger<StorefrontOrderController> logger)
        {
            _context = context;
            _orderProcessingService = orderProcessingService;
            _poMjeriAllocationService = poMjeriAllocationService;
            _orderAdminService = orderAdminService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? customerEmail, string? customerName, bool completedOnly = false)
        {
            customerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail.Trim();
            customerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();

            var query = _context.WebOrders.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(customerEmail))
            {
                query = query.Where(order => order.CustomerEmail == customerEmail);
            }

            if (completedOnly)
            {
                query = query.Where(order => order.Status == WebOrderStatuses.Completed);
            }

            var orders = await query
                .OrderByDescending(order => order.CreatedUtc)
                .Select(order => new WebOrderAdminListItemViewModel
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    CustomerEmail = order.CustomerEmail,
                    CustomerName = ((order.CustomerFirstName ?? string.Empty) + " " + (order.CustomerLastName ?? string.Empty)).Trim(),
                    CreatedUtc = order.CreatedUtc,
                    Status = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    FulfillmentStatus = order.FulfillmentStatus,
                    TotalQuantity = order.Items.Select(item => (int?)item.Quantity).Sum() ?? 0,
                    GrandTotal = order.GrandTotal
                })
                .ToListAsync();

            foreach (var order in orders)
            {
                order.CustomerName = TextEncodingHelper.Decode(order.CustomerName) ?? order.CustomerName;
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                orders = orders
                    .Where(order => string.Equals(
                        TextEncodingHelper.NormalizeInput(order.CustomerName),
                        TextEncodingHelper.NormalizeInput(customerName),
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewData["FilteredCustomerEmail"] = customerEmail;
            ViewData["FilteredCustomerName"] = customerName;
            ViewData["CompletedOnly"] = completedOnly;

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.WebOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(webOrder => webOrder.Id == id);

            if (order == null)
            {
                return NotFound("Web order not found.");
            }
            TextEncodingHelper.DecodeOrderForDisplay(order);

            var items = await _context.WebOrderItems
                .AsNoTracking()
                .Where(item => item.WebOrderId == id)
                .OrderBy(item => item.Id)
                .ToListAsync();
            TextEncodingHelper.DecodeOrderItemsForDisplay(items);

            var statusHistory = await _context.WebOrderStatusHistory
                .AsNoTracking()
                .Where(history => history.WebOrderId == id)
                .OrderByDescending(history => history.ChangedUtc)
                .ToListAsync();
            TextEncodingHelper.DecodeStatusHistoryForDisplay(statusHistory);

            var reservations = await _context.InventoryReservations
                .AsNoTracking()
                .Include(reservation => reservation.Tepih)
                .Where(reservation => reservation.WebOrderId == id)
                .OrderByDescending(reservation => reservation.CreatedUtc)
                .ToListAsync();

            foreach (var reservation in reservations.Where(reservation => reservation.Tepih is not null))
            {
                TextEncodingHelper.DecodeProductForDisplay(reservation.Tepih!);
            }

            var poMjeriItems = new List<PoMjeriOrderItemAdminViewModel>();
            foreach (var item in items.Where(orderItem => orderItem.PoMjeri))
            {
                var activeReservations = reservations
                    .Where(reservation =>
                        reservation.WebOrderItemId == item.Id &&
                        reservation.Status == InventoryReservationStatuses.Active)
                    .OrderBy(reservation => reservation.TepihId)
                    .ToList();

                var currentAllocationQuantities = activeReservations
                    .Where(reservation => reservation.TepihId > 0)
                    .GroupBy(reservation => reservation.TepihId)
                    .ToDictionary(group => group.Key, group => group.Sum(reservation => reservation.Quantity));

                var candidateOptions = await _poMjeriAllocationService.BuildCandidateOptionsAsync(
                    item,
                    currentAllocationQuantities);

                poMjeriItems.Add(new PoMjeriOrderItemAdminViewModel
                {
                    Item = item,
                    ActiveReservations = activeReservations,
                    Candidates = candidateOptions
                });
            }

            var viewModel = new WebOrderAdminDetailsViewModel
            {
                Order = order,
                Items = items,
                EditableItems = await _orderAdminService.BuildEditableLinesAsync(order, items, reservations),
                StatusHistory = statusHistory,
                Reservations = reservations,
                PoMjeriItems = poMjeriItems,
                CanEditItems = StorefrontOrderAdminService.CanEditItems(order),
                StatusUpdate = new WebOrderStatusUpdateViewModel
                {
                    WebOrderId = order.Id,
                    Status = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    FulfillmentStatus = order.FulfillmentStatus,
                    InternalNote = order.InternalNote
                },
                AvailableOrderStatuses = WebOrderStatuses.All,
                AvailablePaymentStatuses = WebPaymentStatuses.All,
                AvailableFulfillmentStatuses = WebFulfillmentStatuses.All
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var changedBy = BuildChangedBy();

            try
            {
                var result = await _orderAdminService.DeleteOrderAsync(id, changedBy);
                TempData[result.Succeeded ? "StorefrontOrderSuccessMessage" : "StorefrontOrderErrorMessage"] = result.Message;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to delete storefront order {OrderId}.", id);
                TempData["StorefrontOrderErrorMessage"] = "Brisanje narudzbine nije uspjelo.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> SearchProducts(
            int id,
            string? name,
            string? model,
            string? color,
            string? size)
        {
            var order = await _context.WebOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (order == null || !StorefrontOrderAdminService.CanEditItems(order))
            {
                return Json(Array.Empty<object>());
            }

            var allEmpty =
                string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(model) &&
                string.IsNullOrWhiteSpace(color) &&
                string.IsNullOrWhiteSpace(size);

            if (allEmpty)
            {
                return Json(Array.Empty<object>());
            }

            var query = _context.Tepisi
                .AsNoTracking()
                .Where(product => !product.Disabled && product.IsPublished);

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(product => product.Name.StartsWith(name));
            }

            if (!string.IsNullOrWhiteSpace(model))
            {
                query = query.Where(product => product.Model.StartsWith(model));
            }

            if (!string.IsNullOrWhiteSpace(color))
            {
                query = query.Where(product => product.Color.StartsWith(color));
            }

            if (!string.IsNullOrWhiteSpace(size))
            {
                var parts = size.Split('X', 'x');

                int? width = null;
                int? length = null;

                if (parts.Length > 0 && int.TryParse(parts[0], out var widthPart))
                {
                    width = widthPart;
                }

                if (parts.Length > 1 && int.TryParse(parts[1], out var lengthPart))
                {
                    length = lengthPart;
                }

                if (width.HasValue)
                {
                    query = query.Where(product =>
                        product.Width.HasValue &&
                        product.Width.Value.ToString().StartsWith(width.Value.ToString()));
                }

                if (length.HasValue)
                {
                    query = query.Where(product =>
                        product.Length.HasValue &&
                        product.Length.Value.ToString().StartsWith(length.Value.ToString()));
                }
            }

            var currentRegularReservedByProduct = await _context.InventoryReservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.WebOrderId == id &&
                    reservation.Status == InventoryReservationStatuses.Active &&
                    reservation.WebOrderItemId.HasValue)
                .Join(
                    _context.WebOrderItems.AsNoTracking().Where(item => !item.PoMjeri),
                    reservation => reservation.WebOrderItemId!.Value,
                    item => item.Id,
                    (reservation, item) => reservation)
                .GroupBy(reservation => reservation.TepihId)
                .ToDictionaryAsync(group => group.Key, group => group.Sum(reservation => reservation.Quantity));

            var rawResults = await query
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Model)
                .ThenBy(product => product.Color)
                .Take(30)
                .Select(product => new
                {
                    id = product.Id,
                    productNumber = product.ProductNumber,
                    name = product.Name,
                    model = product.Model,
                    color = product.Color,
                    width = product.Width,
                    length = product.Length,
                    perM2 = product.PerM2,
                    poMjeri = product.PoMjeri,
                    unId = product.UnID,
                    effectivePrice = product.OnlinePrice ?? product.Price,
                    quantity = product.Quantity,
                    reservedQuantity = product.ReservedQuantity
                })
                .ToListAsync();

            var results = rawResults
                .Select(result => new
                {
                    result.id,
                    result.productNumber,
                    result.name,
                    result.model,
                    result.color,
                    result.width,
                    result.length,
                    result.perM2,
                    result.poMjeri,
                    result.unId,
                    result.effectivePrice,
                    maxQuantity = result.poMjeri
                        ? 0
                        : Math.Max(result.quantity - (result.reservedQuantity - currentRegularReservedByProduct.GetValueOrDefault(result.id)), 0)
                })
                .ToList();

            var poMjeriProductIds = results
                .Where(result => result.poMjeri)
                .Select(result => result.id)
                .ToList();

            var remainingLengths = await LoadRemainingPoMjeriLengthsForOrderAsync(id, poMjeriProductIds);

            var payload = results
                .Where(result => result.poMjeri || result.maxQuantity > 0)
                .Select(result => new
                {
                    result.id,
                    productNumber = TextEncodingHelper.Decode(result.productNumber),
                    name = TextEncodingHelper.Decode(result.name),
                    model = TextEncodingHelper.Decode(result.model),
                    color = TextEncodingHelper.Decode(result.color),
                    result.width,
                    result.length,
                    result.poMjeri,
                    unId = TextEncodingHelper.Decode(result.unId),
                    result.effectivePrice,
                    result.maxQuantity,
                    remainingWidth = result.poMjeri ? result.width : null,
                    remainingLength = result.poMjeri && remainingLengths.TryGetValue(result.id, out var remainingLength)
                        ? remainingLength
                        : (int?)null
                });

            return Json(payload);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewPoMjeriSelection(int id, [FromBody] StorefrontOrderPoMjeriPreviewRequestViewModel previewRequest)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Unesite ispravne dimenzije i kolicinu." });
            }

            var order = await _context.WebOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (order == null || !StorefrontOrderAdminService.CanEditItems(order))
            {
                return Json(new { success = false, message = "Narudzbinu vise nije moguce uredjivati." });
            }

            var preview = await _orderAdminService.PreviewPoMjeriSelectionAsync(
                id,
                previewRequest.ProductId,
                previewRequest.CustomWidth,
                previewRequest.CustomLength,
                previewRequest.Quantity);

            return Json(new
            {
                success = preview.IsValid,
                message = preview.Message,
                maxAvailableQuantity = preview.MaxAvailableQuantity
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItems(int id, [FromBody] StorefrontOrderItemsUpdateViewModel itemUpdate)
        {
            if (id != itemUpdate.WebOrderId)
            {
                return BadRequest(new { success = false, message = "Neispravan zahtjev za azuriranje narudzbine." });
            }

            var changedBy = BuildChangedBy();

            try
            {
                var result = await _orderAdminService.ApplyItemEditsAsync(id, itemUpdate.Items, changedBy);
                return Json(new { success = result.Succeeded, message = result.Message });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save storefront order item edits for order {OrderId}.", id);
                return StatusCode(500, new { success = false, message = "Azuriranje stavki nije uspjelo." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while saving storefront order item edits for order {OrderId}.", id);
                return StatusCode(500, new { success = false, message = "Azuriranje stavki nije uspjelo." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplacePoMjeriAllocation(int id, PoMjeriAllocationManualUpdateViewModel allocationUpdate)
        {
            var changedBy = BuildChangedBy();

            try
            {
                var result = await _poMjeriAllocationService.ReplaceAllocationsAsync(
                    id,
                    allocationUpdate.WebOrderItemId,
                    allocationUpdate.Entries,
                    changedBy);

                if (result.Succeeded)
                {
                    TempData["StorefrontOrderSuccessMessage"] = result.Message;
                }
                else
                {
                    TempData["StorefrontOrderErrorMessage"] = result.Message;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to replace po mjeri allocation for storefront order {OrderId}, item {ItemId}.",
                    id,
                    allocationUpdate.WebOrderItemId);
                TempData["StorefrontOrderErrorMessage"] = "Promjena alokacije nije uspjela.";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while replacing po mjeri allocation for storefront order {OrderId}, item {ItemId}.",
                    id,
                    allocationUpdate.WebOrderItemId);
                TempData["StorefrontOrderErrorMessage"] = "Promjena alokacije nije uspjela.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, WebOrderStatusUpdateViewModel statusUpdate)
        {
            if (id != statusUpdate.WebOrderId)
            {
                return BadRequest("Invalid order update request.");
            }

            if (!WebOrderStatuses.All.Contains(statusUpdate.Status)
                || !WebPaymentStatuses.All.Contains(statusUpdate.PaymentStatus)
                || !WebFulfillmentStatuses.All.Contains(statusUpdate.FulfillmentStatus))
            {
                TempData["StorefrontOrderErrorMessage"] = "Invalid storefront order status values.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var changedBy = BuildChangedBy();
            var internalNote = statusUpdate.InternalNote;

            if (User.IsInRole("employee"))
            {
                internalNote = await _context.WebOrders
                    .AsNoTracking()
                    .Where(order => order.Id == id)
                    .Select(order => order.InternalNote)
                    .FirstOrDefaultAsync();
            }

            try
            {
                var result = await _orderProcessingService.ApplyStatusUpdateAsync(
                    id,
                    statusUpdate.Status,
                    statusUpdate.PaymentStatus,
                    statusUpdate.FulfillmentStatus,
                    statusUpdate.Note,
                    internalNote,
                    changedBy);

                if (result.Succeeded)
                {
                    TempData["StorefrontOrderSuccessMessage"] = result.Message;
                }
                else
                {
                    TempData["StorefrontOrderErrorMessage"] = result.Message;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Storefront order {OrderId} changed before the update could be saved.", id);
                TempData["StorefrontOrderErrorMessage"] = "Storefront order changed while you were editing it. Refresh and try again.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update storefront order {OrderId}.", id);
                TempData["StorefrontOrderErrorMessage"] = "Storefront order update failed.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating storefront order {OrderId}.", id);
                TempData["StorefrontOrderErrorMessage"] = "Storefront order update failed.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private string BuildChangedBy()
        {
            var firstName = User.FindFirstValue(ClaimTypes.GivenName);
            var lastName = User.FindFirstValue(ClaimTypes.Surname);
            var fullName = $"{firstName} {lastName}".Trim();

            return string.IsNullOrWhiteSpace(fullName)
                ? (User.Identity?.Name ?? "Admin")
                : fullName;
        }

        private async Task<Dictionary<int, int>> LoadRemainingPoMjeriLengthsForOrderAsync(int orderId, IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            var products = await _context.Tepisi
                .AsNoTracking()
                .Where(product => ids.Contains(product.Id))
                .ToListAsync();

            var soldLengths = await _context.Prodaje
                .AsNoTracking()
                .Where(sale => ids.Contains(sale.TepihId) && !sale.Disabled)
                .GroupBy(sale => sale.TepihId)
                .Select(group => new
                {
                    TepihId = group.Key,
                    ConsumedLength = group.Sum(sale => (sale.ConsumedLength ?? sale.CustomLength ?? 0) * sale.Quantity)
                })
                .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength);

            var reservedLengths = await _context.InventoryReservations
                .AsNoTracking()
                .Where(reservation =>
                    ids.Contains(reservation.TepihId) &&
                    reservation.Status == InventoryReservationStatuses.Active &&
                    reservation.WebOrderId != orderId &&
                    reservation.ConsumedLengthPerUnit.HasValue)
                .GroupBy(reservation => reservation.TepihId)
                .Select(group => new
                {
                    TepihId = group.Key,
                    ConsumedLength = group.Sum(reservation => reservation.Quantity * (reservation.ConsumedLengthPerUnit ?? 0))
                })
                .ToDictionaryAsync(item => item.TepihId, item => item.ConsumedLength);

            return products.ToDictionary(
                product => product.Id,
                product =>
                {
                    var originalLength = product.Length ?? 0;
                    return Math.Max(
                        originalLength -
                        soldLengths.GetValueOrDefault(product.Id) -
                        reservedLengths.GetValueOrDefault(product.Id),
                        0);
                });
        }
    }
}
