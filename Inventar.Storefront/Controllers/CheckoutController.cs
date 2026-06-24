using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Inventar.Storefront.Services;
using Inventar.Storefront.ViewModels.Checkout;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventar.Storefront.Controllers;

public class CheckoutController : Controller
{
    private readonly StorefrontDbContext _dbContext;
    private readonly ICartService _cartService;
    private readonly ICheckoutService _checkoutService;
    private readonly IPendingCheckoutStore _pendingCheckoutStore;
    private readonly IStorefrontEmailService _emailService;
    private readonly IStorefrontCustomerService _customerService;
    private readonly StorefrontSettings _settings;
    private readonly StorefrontEmailSettings _emailSettings;
    private readonly StorefrontPoMjeriInventoryService _poMjeriInventoryService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        StorefrontDbContext dbContext,
        ICartService cartService,
        ICheckoutService checkoutService,
        IPendingCheckoutStore pendingCheckoutStore,
        IStorefrontEmailService emailService,
        IStorefrontCustomerService customerService,
        IOptions<StorefrontSettings> settings,
        IOptions<StorefrontEmailSettings> emailSettings,
        StorefrontPoMjeriInventoryService poMjeriInventoryService,
        ILogger<CheckoutController> logger)
    {
        _dbContext = dbContext;
        _cartService = cartService;
        _checkoutService = checkoutService;
        _pendingCheckoutStore = pendingCheckoutStore;
        _emailService = emailService;
        _customerService = customerService;
        _settings = settings.Value;
        _emailSettings = emailSettings.Value;
        _poMjeriInventoryService = poMjeriInventoryService;
        _logger = logger;
    }

    [HttpGet("narudzba")]
    public async Task<IActionResult> Index()
    {
        var currentCustomer = await _customerService.GetCurrentCustomerAsync(User, HttpContext.RequestAborted);
        var pendingCheckout = _pendingCheckoutStore.Get();
        var form = pendingCheckout?.Form ?? CreateCheckoutFormFromCustomer(currentCustomer);
        var viewModel = await BuildCheckoutPageViewModelAsync(form, currentCustomer);

        if (viewModel.Lines.Count == 0)
        {
            _pendingCheckoutStore.Clear();
            TempData["CartErrorMessage"] = "Korpa je prazna.";
            return RedirectToAction("Index", "Cart");
        }

        return View(viewModel);
    }

    [HttpPost("narudzba")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutFormViewModel form)
    {
        var currentCustomer = await _customerService.GetCurrentCustomerAsync(User, HttpContext.RequestAborted);

        form = NormalizeForm(form);
        if (currentCustomer is not null)
        {
            form.Email = currentCustomer.Email;
        }

        var viewModel = await BuildCheckoutPageViewModelAsync(form, currentCustomer);
        if (viewModel.Lines.Count == 0)
        {
            _pendingCheckoutStore.Clear();
            TempData["CartErrorMessage"] = "Korpa je prazna.";
            return RedirectToAction("Index", "Cart");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        if (viewModel.Lines.Any(line => line.HasAvailabilityIssue))
        {
            ModelState.AddModelError(
                string.Empty,
                "Neki proizvodi više nemaju traženu količinu. Pregledajte korpu prije slanja narudžbine.");
            return View(viewModel);
        }

        if (currentCustomer is not null)
        {
            try
            {
                await _customerService.LinkOrdersByEmailAsync(currentCustomer, HttpContext.RequestAborted);
                await _customerService.SaveProfileAsync(
                    currentCustomer,
                    CreateProfileData(form),
                    HttpContext.RequestAborted);

                var checkoutResult = await _checkoutService.CreateCashOnDeliveryOrderAsync(
                    BuildCheckoutRequest(form),
                    _cartService.GetCart(),
                    currentCustomer.Id,
                    HttpContext.RequestAborted);

                if (!checkoutResult.Succeeded || string.IsNullOrWhiteSpace(checkoutResult.OrderNumber))
                {
                    TempData["CartErrorMessage"] = checkoutResult.Message;
                    return RedirectToAction("Index", "Cart");
                }

                return await FinishSuccessfulOrderAsync(checkoutResult.OrderNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create checkout order for authenticated customer {CustomerEmail}.", currentCustomer.Email);
                ModelState.AddModelError(
                    string.Empty,
                    "Trenutno nijesmo uspjeli da obradimo vašu narudžbinu. Pokušajte ponovo za nekoliko trenutaka.");
                return View(viewModel);
            }
        }

        var verificationCode = StorefrontVerificationCodeHelper.GenerateCode();
        var utcNow = DateTime.UtcNow;
        _pendingCheckoutStore.Save(new PendingCheckoutSessionModel
        {
            Form = form,
            CartItems = _cartService.GetCart()
                .Select(item => new CartItem
                {
                    LineId = item.LineId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    PoMjeri = item.PoMjeri,
                    CustomWidth = item.CustomWidth,
                    CustomLength = item.CustomLength,
                    SelectedColor = item.SelectedColor,
                    Allocations = item.Allocations
                        .Select(allocation => new CartItemAllocation
                        {
                            SourceProductId = allocation.SourceProductId,
                            Quantity = allocation.Quantity,
                            ConsumedLengthPerUnit = allocation.ConsumedLengthPerUnit
                        })
                        .ToList()
                })
                .ToList(),
            Email = form.Email,
            VerificationCodeHash = StorefrontVerificationCodeHelper.HashCode(verificationCode),
            ExpiresUtc = utcNow.AddMinutes(_emailSettings.VerificationCodeLifetimeMinutes),
            LastSentUtc = utcNow
        });

        try
        {
            await _emailService.SendCheckoutVerificationCodeAsync(
                new CheckoutVerificationEmailModel
                {
                    CustomerFirstName = form.FirstName,
                    Email = form.Email,
                    VerificationCode = verificationCode,
                    ExpiresInMinutes = _emailSettings.VerificationCodeLifetimeMinutes
                },
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send checkout verification email to {CustomerEmail}.", form.Email);
            _pendingCheckoutStore.Clear();
            ModelState.AddModelError(
                string.Empty,
                "Trenutno nismo u mogućnosti da pošaljemo verifikacioni email. Pokušajte ponovo malo kasnije.");
            return View(viewModel);
        }

        TempData["CartSuccessMessage"] = $"Poslali smo verifikacioni kod na {form.Email}.";
        return RedirectToAction(nameof(VerifyEmail));
    }

    [HttpGet("narudzba/verifikacija-emaila")]
    public IActionResult VerifyEmail()
    {
        var pendingCheckout = _pendingCheckoutStore.Get();
        if (pendingCheckout == null || pendingCheckout.CartItems.Count == 0)
        {
            TempData["CartErrorMessage"] = "Sesija za potvrdu emaila je istekla. Pošaljite narudžbinu ponovo.";
            return RedirectToAction(nameof(Index));
        }

        return View(BuildVerificationViewModel(pendingCheckout));
    }

    [HttpPost("narudzba/verifikacija-emaila")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(CheckoutVerificationViewModel form)
    {
        var pendingCheckout = _pendingCheckoutStore.Get();
        if (pendingCheckout == null || pendingCheckout.CartItems.Count == 0)
        {
            TempData["CartErrorMessage"] = "Sesija za potvrdu emaila je istekla. Pošaljite narudžbinu ponovo.";
            return RedirectToAction(nameof(Index));
        }

        var viewModel = BuildVerificationViewModel(pendingCheckout, form.Code);
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        if (pendingCheckout.ExpiresUtc <= DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "Verifikacioni kod je istekao. Zatražite novi kod.");
            return View(viewModel);
        }

        if (!string.Equals(
                pendingCheckout.VerificationCodeHash,
                StorefrontVerificationCodeHelper.HashCode(form.Code),
                StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(form.Code), "Verifikacioni kod nije ispravan.");
            return View(viewModel);
        }

        try
        {
            var customer = await _customerService.GetOrCreateByVerifiedEmailAsync(
                pendingCheckout.Form.Email,
                cancellationToken: HttpContext.RequestAborted);
            await _customerService.LinkOrdersByEmailAsync(customer, HttpContext.RequestAborted);
            await _customerService.SaveProfileAsync(
                customer,
                CreateProfileData(pendingCheckout.Form),
                HttpContext.RequestAborted);

            var result = await _checkoutService.CreateCashOnDeliveryOrderAsync(
                BuildCheckoutRequest(pendingCheckout.Form),
                pendingCheckout.CartItems,
                customer.Id,
                HttpContext.RequestAborted);

            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.OrderNumber))
            {
                _pendingCheckoutStore.Clear();
                TempData["CartErrorMessage"] = result.Message;
                return RedirectToAction("Index", "Cart");
            }

            await SignInCustomerAsync(customer, rememberMe: true);
            _pendingCheckoutStore.Clear();
            return await FinishSuccessfulOrderAsync(result.OrderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finalize checkout order after email verification for {CustomerEmail}.", pendingCheckout.Form.Email);
            ModelState.AddModelError(
                string.Empty,
                "Trenutno nijesmo uspjeli da obradimo vašu narudžbinu. Pokušajte ponovo za nekoliko trenutaka.");
            return View(viewModel);
        }
    }

    [HttpPost("narudzba/verifikacija-emaila/ponovo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendVerificationCode()
    {
        var pendingCheckout = _pendingCheckoutStore.Get();
        if (pendingCheckout == null || pendingCheckout.CartItems.Count == 0)
        {
            TempData["CartErrorMessage"] = "Sesija za potvrdu emaila je istekla. Pošaljite narudžbinu ponovo.";
            return RedirectToAction(nameof(Index));
        }

        var verificationCode = StorefrontVerificationCodeHelper.GenerateCode();
        pendingCheckout.VerificationCodeHash = StorefrontVerificationCodeHelper.HashCode(verificationCode);
        pendingCheckout.ExpiresUtc = DateTime.UtcNow.AddMinutes(_emailSettings.VerificationCodeLifetimeMinutes);
        pendingCheckout.LastSentUtc = DateTime.UtcNow;
        _pendingCheckoutStore.Save(pendingCheckout);

        try
        {
            await _emailService.SendCheckoutVerificationCodeAsync(
                new CheckoutVerificationEmailModel
                {
                    CustomerFirstName = pendingCheckout.Form.FirstName,
                    Email = pendingCheckout.Email,
                    VerificationCode = verificationCode,
                    ExpiresInMinutes = _emailSettings.VerificationCodeLifetimeMinutes
                },
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend checkout verification email to {CustomerEmail}.", pendingCheckout.Email);
            TempData["CartErrorMessage"] =
                "Nismo uspjeli da pošaljemo novi verifikacioni kod. Pokušajte ponovo.";
            return RedirectToAction(nameof(VerifyEmail));
        }

        TempData["CartSuccessMessage"] = $"Novi verifikacioni kod je poslat na {pendingCheckout.Email}.";
        return RedirectToAction(nameof(VerifyEmail));
    }

    [HttpGet("narudzba/uspjeh/{orderNumber}")]
    public async Task<IActionResult> Success(string orderNumber)
    {
        var viewModel = await BuildOrderConfirmationViewModelAsync(orderNumber);
        if (viewModel == null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (TempData["OrderConfirmationEmailSent"] is string sentValue &&
            bool.TryParse(sentValue, out var confirmationEmailSent))
        {
            viewModel.ConfirmationEmailSent = confirmationEmailSent;
        }

        if (TempData["OrderConfirmationEmailStatusMessage"] is string statusMessage &&
            !string.IsNullOrWhiteSpace(statusMessage))
        {
            viewModel.ConfirmationEmailStatusMessage = statusMessage;
        }

        return View(viewModel);
    }

    private CheckoutRequest BuildCheckoutRequest(CheckoutFormViewModel form)
    {
        return new CheckoutRequest
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
        };
    }

    private async Task<OrderConfirmationViewModel?> BuildOrderConfirmationViewModelAsync(string orderNumber)
    {
        var order = await _dbContext.WebOrders
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(item => item.OrderNumber == orderNumber);

        if (order == null)
        {
            return null;
        }

        return new OrderConfirmationViewModel
        {
            OrderNumber = order.OrderNumber,
            CustomerFirstName = order.CustomerFirstName,
            CustomerEmail = order.CustomerEmail ?? string.Empty,
            CustomerPhone = order.CustomerPhone,
            CustomerNote = order.CustomerNote,
            ShippingAddressLine1 = order.ShippingAddressLine1,
            ShippingCity = order.ShippingCity,
            ShippingCountry = order.ShippingCountry,
            ItemsTotal = order.ItemsTotal,
            ShippingTotal = order.ShippingTotal,
            GrandTotal = order.GrandTotal,
            TotalItems = order.Items.Sum(item => item.Quantity),
            Lines = order.Items
                .Select(item => new OrderConfirmationLineViewModel
                {
                    ImageUrl = item.PrimaryImageUrl,
                    Title = BuildOrderLineShortDescription(item),
                    Meta = BuildOrderLineMeta(item),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal
                })
                .ToList()
        };
    }

    private async Task<CheckoutPageViewModel> BuildCheckoutPageViewModelAsync(
        CheckoutFormViewModel form,
        StorefrontCustomer? currentCustomer)
    {
        var cartItems = _cartService.GetCart();
        if (cartItems.Count == 0)
        {
            return new CheckoutPageViewModel
            {
                Form = form,
                IsAuthenticatedCustomer = currentCustomer is not null,
                AuthenticatedCustomerEmail = currentCustomer?.Email
            };
        }

        var productIds = cartItems.Select(item => item.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Include(product => product.ProductImages)
            .Where(product => productIds.Contains(product.Id) && !product.Disabled)
            .ToDictionaryAsync(product => product.Id);

        var lines = new List<Inventar.Storefront.ViewModels.Cart.CartLineViewModel>();
        foreach (var item in cartItems)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                continue;
            }

            var maxOrderQuantity = item.PoMjeri
                ? await ResolvePoMjeriMaxQuantityAsync(product, item, cartItems)
                : StorefrontStockRules.GetMaxOrderQuantity(product.AvailableQuantity);

            if (maxOrderQuantity <= 0)
            {
                continue;
            }

            lines.Add(StorefrontViewModelMapper.ToCartLine(product, item, maxOrderQuantity));
        }

        var subtotal = lines.Sum(line => line.LineTotal);
        var shippingTotal = lines.Count == 0 ? 0m : _settings.FlatShippingCost;

        return new CheckoutPageViewModel
        {
            Form = form,
            Lines = lines,
            Subtotal = subtotal,
            ShippingTotal = shippingTotal,
            GrandTotal = subtotal + shippingTotal,
            TotalItems = lines.Sum(line => line.Quantity),
            IsAuthenticatedCustomer = currentCustomer is not null,
            AuthenticatedCustomerEmail = currentCustomer?.Email
        };
    }

    private CheckoutVerificationViewModel BuildVerificationViewModel(
        PendingCheckoutSessionModel pendingCheckout,
        string? code = null)
    {
        var remainingMinutes = pendingCheckout.ExpiresUtc <= DateTime.UtcNow
            ? 0
            : Math.Max(
                1,
                (int)Math.Ceiling((pendingCheckout.ExpiresUtc - DateTime.UtcNow).TotalMinutes));

        return new CheckoutVerificationViewModel
        {
            Code = code ?? string.Empty,
            MaskedEmail = StorefrontVerificationCodeHelper.MaskEmail(pendingCheckout.Email),
            ExpiresInMinutes = remainingMinutes
        };
    }

    private static CheckoutFormViewModel NormalizeForm(CheckoutFormViewModel form)
    {
        form.FirstName = form.FirstName.Trim();
        form.LastName = form.LastName.Trim();
        form.Email = form.Email.Trim();
        form.Phone = form.Phone.Trim();
        form.AddressLine1 = form.AddressLine1.Trim();
        form.City = form.City.Trim();
        form.Country = string.IsNullOrWhiteSpace(form.Country) ? "Crna Gora" : form.Country.Trim();
        form.CustomerNote = string.IsNullOrWhiteSpace(form.CustomerNote) ? null : form.CustomerNote.Trim();
        return form;
    }

    private CheckoutFormViewModel CreateCheckoutFormFromCustomer(StorefrontCustomer? customer)
    {
        if (customer is null)
        {
            return new CheckoutFormViewModel();
        }

        return new CheckoutFormViewModel
        {
            FirstName = customer.FirstName ?? string.Empty,
            LastName = customer.LastName ?? string.Empty,
            Email = customer.Email,
            Phone = customer.Phone ?? string.Empty,
            AddressLine1 = customer.AddressLine1 ?? string.Empty,
            AddressLine2 = customer.AddressLine2,
            City = customer.City ?? string.Empty,
            PostalCode = customer.PostalCode,
            Country = string.IsNullOrWhiteSpace(customer.Country) ? "Crna Gora" : customer.Country
        };
    }

    private static StorefrontCustomerProfileData CreateProfileData(CheckoutFormViewModel form)
    {
        return new StorefrontCustomerProfileData
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            Phone = form.Phone,
            AddressLine1 = form.AddressLine1,
            AddressLine2 = form.AddressLine2,
            City = form.City,
            PostalCode = form.PostalCode,
            Country = form.Country
        };
    }

    private async Task SignInCustomerAsync(StorefrontCustomer customer, bool rememberMe)
    {
        var principal = _customerService.CreatePrincipal(customer);
        await HttpContext.SignInAsync(
            StorefrontAuthenticationConstants.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                AllowRefresh = true,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(_settings.RememberCustomerForDays)
                    : null
            });
    }

    private async Task<IActionResult> FinishSuccessfulOrderAsync(string orderNumber)
    {
        var orderConfirmation = await BuildOrderConfirmationViewModelAsync(orderNumber);
        if (orderConfirmation == null)
        {
            _cartService.Clear();
            TempData["CartErrorMessage"] = "Narudžbina je kreirana, ali potvrda trenutno nije dostupna.";
            return RedirectToAction(nameof(Success), new { orderNumber });
        }

        try
        {
            await _emailService.SendOrderConfirmationAsync(orderConfirmation, HttpContext.RequestAborted);
            TempData["OrderConfirmationEmailSent"] = bool.TrueString;
            TempData["OrderConfirmationEmailStatusMessage"] =
                $"Potvrda narudžbine je poslata na {orderConfirmation.CustomerEmail}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order confirmation email for order {OrderNumber}.", orderNumber);
            TempData["OrderConfirmationEmailSent"] = bool.FalseString;
            TempData["OrderConfirmationEmailStatusMessage"] =
                "Narudžbina je kreirana, ali potvrda emailom trenutno nije poslata.";
        }

        _cartService.Clear();
        return RedirectToAction(nameof(Success), new { orderNumber });
    }

    private static string BuildOrderLineTitle(WebOrderItem item)
    {
        return string.IsNullOrWhiteSpace(item.Model)
            ? item.ProductName
            : $"{item.ProductName} - {item.Model}";
    }

    private static string BuildOrderLineShortDescription(WebOrderItem item)
    {
        if (item.Product != null)
        {
            return StorefrontViewModelMapper.BuildShortDescriptionText(item.Product);
        }

        return BuildOrderLineTitle(item);
    }

    private static string BuildOrderLineMeta(WebOrderItem item)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.Color))
        {
            parts.Add(item.Color);
        }

        if (item.Width.HasValue && item.Length.HasValue)
        {
            parts.Add($"{item.Width.Value} x {item.Length.Value} cm");
        }

        return string.Join(" | ", parts);
    }

    private async Task<int> ResolvePoMjeriMaxQuantityAsync(
        StorefrontProduct product,
        CartItem item,
        IReadOnlyCollection<CartItem> cartItems)
    {
        if (!product.PoMjeri || !item.CustomWidth.HasValue || !item.CustomLength.HasValue)
        {
            return 0;
        }

        var variants = await LoadGroupVariantsAsync(product);
        var snapshot = await _poMjeriInventoryService.LoadSnapshotAsync(
            variants,
            cartItems,
            item.LineId,
            HttpContext.RequestAborted);

        return StorefrontPoMjeriPlanner.Evaluate(
            variants,
            snapshot,
            item.SelectedColor ?? product.Color,
            item.CustomWidth.Value,
            item.CustomLength.Value,
            item.Quantity).MaxAvailableQuantity;
    }

    private async Task<List<StorefrontProduct>> LoadGroupVariantsAsync(StorefrontProduct product)
    {
        var query = _dbContext.Products
            .AsNoTracking()
            .Include(item => item.ProductImages)
            .Where(item => item.IsPublished && !item.Disabled && item.Slug != null);

        if (product.PoMjeri)
        {
            query = query.Where(item =>
                item.PoMjeri &&
                item.Name == product.Name &&
                item.ProductNumber == product.ProductNumber &&
                item.Model == product.Model);
        }
        else
        {
            query = query.Where(item =>
                item.Name == product.Name &&
                item.Model == product.Model);
        }

        return await query
            .OrderBy(item => item.Id)
            .ToListAsync();
    }
}
