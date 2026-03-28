using System.Security.Claims;
using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Inventar.ViewModels.StorefrontAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Controllers
{
    [Authorize(Roles = "admin,superadmin")]
    public class StorefrontOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebOrderProcessingService _orderProcessingService;
        private readonly ILogger<StorefrontOrderController> _logger;

        public StorefrontOrderController(
            ApplicationDbContext context,
            IWebOrderProcessingService orderProcessingService,
            ILogger<StorefrontOrderController> logger)
        {
            _context = context;
            _orderProcessingService = orderProcessingService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _context.WebOrders
                .AsNoTracking()
                .OrderByDescending(order => order.CreatedUtc)
                .Select(order => new WebOrderAdminListItemViewModel
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    CustomerName = $"{order.CustomerFirstName} {order.CustomerLastName}".Trim(),
                    CustomerEmail = order.CustomerEmail,
                    CreatedUtc = order.CreatedUtc,
                    Status = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    FulfillmentStatus = order.FulfillmentStatus,
                    TotalQuantity = order.Items.Select(item => (int?)item.Quantity).Sum() ?? 0,
                    GrandTotal = order.GrandTotal
                })
                .ToListAsync();

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

            var items = await _context.WebOrderItems
                .AsNoTracking()
                .Where(item => item.WebOrderId == id)
                .OrderBy(item => item.Id)
                .ToListAsync();

            var statusHistory = await _context.WebOrderStatusHistory
                .AsNoTracking()
                .Where(history => history.WebOrderId == id)
                .OrderByDescending(history => history.ChangedUtc)
                .ToListAsync();

            var reservations = await _context.InventoryReservations
                .AsNoTracking()
                .Include(reservation => reservation.Tepih)
                .Where(reservation => reservation.WebOrderId == id)
                .OrderByDescending(reservation => reservation.CreatedUtc)
                .ToListAsync();

            var viewModel = new WebOrderAdminDetailsViewModel
            {
                Order = order,
                Items = items,
                StatusHistory = statusHistory,
                Reservations = reservations,
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

            try
            {
                var result = await _orderProcessingService.ApplyStatusUpdateAsync(
                    id,
                    statusUpdate.Status,
                    statusUpdate.PaymentStatus,
                    statusUpdate.FulfillmentStatus,
                    statusUpdate.Note,
                    statusUpdate.InternalNote,
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
    }
}
