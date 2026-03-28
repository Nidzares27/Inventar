using Inventar.Storefront.Data;
using Inventar.Storefront.Services;
using Inventar.Storefront.ViewModels.Checkout;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventar.Storefront.Controllers;

public class CheckoutController : Controller
{
    private readonly StorefrontDbContext _dbContext;
    private readonly ICartService _cartService;
    private readonly ICheckoutService _checkoutService;
    private readonly StorefrontSettings _settings;

    public CheckoutController(
        StorefrontDbContext dbContext,
        ICartService cartService,
        ICheckoutService checkoutService,
        IOptions<StorefrontSettings> settings)
    {
        _dbContext = dbContext;
        _cartService = cartService;
        _checkoutService = checkoutService;
        _settings = settings.Value;
    }

    [HttpGet("narudzba")]
    public async Task<IActionResult> Index()
    {
        var viewModel = await BuildCheckoutPageViewModelAsync(new CheckoutFormViewModel());
        if (viewModel.Lines.Count == 0)
        {
            TempData["CartErrorMessage"] = "Korpa je prazna.";
            return RedirectToAction("Index", "Cart");
        }

        return View(viewModel);
    }

    [HttpPost("narudzba")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutFormViewModel form)
    {
        var viewModel = await BuildCheckoutPageViewModelAsync(form);
        if (viewModel.Lines.Count == 0)
        {
            TempData["CartErrorMessage"] = "Korpa je prazna.";
            return RedirectToAction("Index", "Cart");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        if (viewModel.Lines.Any(line => line.HasAvailabilityIssue))
        {
            ModelState.AddModelError(string.Empty, "Neki proizvodi više nemaju traženu količinu. Pregledajte korpu prije slanja narudžbe.");
            return View(viewModel);
        }

        var result = await _checkoutService.CreateCashOnDeliveryOrderAsync(
            new CheckoutRequest
            {
                FirstName = form.FirstName,
                LastName = form.LastName,
                Email = form.Email,
                Phone = form.Phone,
                AddressLine1 = form.AddressLine1,
                AddressLine2 = null,
                City = form.City,
                PostalCode = null,
                Country = string.IsNullOrWhiteSpace(form.Country) ? "Crna Gora" : form.Country,
                CustomerNote = form.CustomerNote
            },
            _cartService.GetCart(),
            HttpContext.RequestAborted);

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.OrderNumber))
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(viewModel);
        }

        _cartService.Clear();
        return RedirectToAction(nameof(Success), new { orderNumber = result.OrderNumber });
    }

    [HttpGet("narudzba/uspjeh/{orderNumber}")]
    public async Task<IActionResult> Success(string orderNumber)
    {
        var order = await _dbContext.WebOrders
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.OrderNumber == orderNumber);

        if (order == null)
        {
            return RedirectToAction("Index", "Home");
        }

        var viewModel = new OrderConfirmationViewModel
        {
            OrderNumber = order.OrderNumber,
            CustomerFirstName = order.CustomerFirstName,
            GrandTotal = order.GrandTotal,
            TotalItems = order.Items.Sum(item => item.Quantity)
        };

        return View(viewModel);
    }

    private async Task<CheckoutPageViewModel> BuildCheckoutPageViewModelAsync(CheckoutFormViewModel form)
    {
        var cartItems = _cartService.GetCart();
        if (cartItems.Count == 0)
        {
            return new CheckoutPageViewModel
            {
                Form = form
            };
        }

        var productIds = cartItems.Select(item => item.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Include(product => product.ProductImages)
            .Where(product => productIds.Contains(product.Id) && !product.Disabled)
            .ToDictionaryAsync(product => product.Id);

        var lines = cartItems
            .Where(item => products.ContainsKey(item.ProductId))
            .Select(item => StorefrontViewModelMapper.ToCartLine(products[item.ProductId], item.Quantity))
            .ToList();

        var subtotal = lines.Sum(line => line.LineTotal);
        var shippingTotal = lines.Count == 0 ? 0m : _settings.FlatShippingCost;

        return new CheckoutPageViewModel
        {
            Form = form,
            Lines = lines,
            Subtotal = subtotal,
            ShippingTotal = shippingTotal,
            GrandTotal = subtotal + shippingTotal,
            TotalItems = lines.Sum(line => line.Quantity)
        };
    }
}
