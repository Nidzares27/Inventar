using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Inventar.Storefront.Services;
using Inventar.Storefront.Utils;
using Inventar.Storefront.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Inventar.Storefront.Controllers;

[Route("nalog")]
public class AccountController : Controller
{
    private readonly StorefrontDbContext _dbContext;
    private readonly IStorefrontCustomerService _customerService;
    private readonly IPendingAccountLoginStore _pendingAccountLoginStore;
    private readonly IStorefrontEmailService _emailService;
    private readonly StorefrontEmailSettings _emailSettings;
    private readonly StorefrontGoogleAuthSettings _googleAuthSettings;
    private readonly StorefrontSettings _storefrontSettings;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        StorefrontDbContext dbContext,
        IStorefrontCustomerService customerService,
        IPendingAccountLoginStore pendingAccountLoginStore,
        IStorefrontEmailService emailService,
        IOptions<StorefrontEmailSettings> emailSettings,
        IOptions<StorefrontGoogleAuthSettings> googleAuthSettings,
        IOptions<StorefrontSettings> storefrontSettings,
        ILogger<AccountController> logger)
    {
        _dbContext = dbContext;
        _customerService = customerService;
        _pendingAccountLoginStore = pendingAccountLoginStore;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
        _googleAuthSettings = googleAuthSettings.Value;
        _storefrontSettings = storefrontSettings.Value;
        _logger = logger;
    }

    [HttpGet("")]
    [Authorize(AuthenticationSchemes = StorefrontAuthenticationConstants.AuthenticationScheme)]
    public async Task<IActionResult> Index()
    {
        var customer = await RequireCurrentCustomerAsync();
        if (customer is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var recentOrderEntities = await _dbContext.WebOrders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.StorefrontCustomerId == customer.Id)
            .OrderByDescending(order => order.CreatedUtc)
            .Take(5)
            .ToListAsync();

        var recentOrders = recentOrderEntities
            .Select(MapOrderListItem)
            .ToList();

        var viewModel = new AccountDashboardViewModel
        {
            DisplayName = TextEncodingHelper.Decode(string.IsNullOrWhiteSpace(customer.DisplayName) ? customer.Email : customer.DisplayName) ?? customer.Email,
            Email = customer.Email,
            IsProfileComplete = IsProfileComplete(customer),
            RecentOrders = recentOrders
        };

        return View(viewModel);
    }

    [HttpGet("prijava")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var currentCustomer = await _customerService.GetCurrentCustomerAsync(User, HttpContext.RequestAborted);
            if (currentCustomer is not null)
            {
                return Redirect(GetSafeReturnUrl(returnUrl) ?? Url.Action(nameof(Index)) ?? "/");
            }

            await HttpContext.SignOutAsync(StorefrontAuthenticationConstants.AuthenticationScheme);
        }

        return View(CreateLoginViewModel(returnUrl));
    }

    [HttpPost("prijava")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AccountLoginViewModel form)
    {
        form.Email = form.Email.Trim();
        form.ReturnUrl = GetSafeReturnUrl(form.ReturnUrl);

        if (!ModelState.IsValid)
        {
            form.IsGoogleLoginAvailable = _googleAuthSettings.IsConfigured;
            return View(form);
        }

        IssuedLoginCodeResult loginCode;
        try
        {
            loginCode = await _customerService.IssueLoginCodeAsync(
                form.Email,
                form.RememberMe,
                HttpContext.RequestAborted);

            await _emailService.SendAccountLoginCodeAsync(
                new AccountLoginEmailViewModel
                {
                    Email = loginCode.Email,
                    VerificationCode = loginCode.VerificationCode,
                    ExpiresInMinutes = _emailSettings.VerificationCodeLifetimeMinutes
                },
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send storefront account login code to {CustomerEmail}.", form.Email);
            ModelState.AddModelError(
                string.Empty,
                "Trenutno nismo u mogućnosti da pošaljemo kod za prijavu. Pokušajte ponovo malo kasnije.");
            form.IsGoogleLoginAvailable = _googleAuthSettings.IsConfigured;
            return View(form);
        }

        _pendingAccountLoginStore.Save(new PendingAccountLoginSessionModel
        {
            Email = loginCode.Email,
            RememberMe = form.RememberMe,
            ReturnUrl = form.ReturnUrl,
            ExpiresUtc = loginCode.ExpiresUtc
        });

        TempData["CartSuccessMessage"] = $"Poslali smo kod za prijavu na {loginCode.Email}.";
        return RedirectToAction(nameof(VerifyLogin));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);
        if (!_googleAuthSettings.IsConfigured)
        {
            _logger.LogWarning("Google storefront login was requested while OAuth credentials are missing or still set to placeholders.");
            TempData["CartErrorMessage"] = "Google prijava trenutno nije dostupna.";
            return RedirectToAction(nameof(Login), new { returnUrl = safeReturnUrl });
        }

        var redirectUrl = Url.Action(nameof(GoogleResponse), new { returnUrl = safeReturnUrl })
            ?? Url.Action(nameof(Index))
            ?? "/";

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Challenge(properties, StorefrontAuthenticationConstants.GoogleAuthenticationScheme);
    }

    [HttpGet("google/povratak")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleResponse(string? returnUrl = null)
    {
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);
        if (!_googleAuthSettings.IsConfigured)
        {
            _logger.LogWarning("Google storefront callback was reached while OAuth credentials are missing or still set to placeholders.");
            TempData["CartErrorMessage"] = "Google prijava trenutno nije dostupna.";
            return RedirectToAction(nameof(Login), new { returnUrl = safeReturnUrl });
        }

        var externalAuthResult = await HttpContext.AuthenticateAsync(StorefrontAuthenticationConstants.ExternalAuthenticationScheme);
        if (!externalAuthResult.Succeeded || externalAuthResult.Principal is null)
        {
            TempData["CartErrorMessage"] = "Google prijava nije uspjela. Pokušajte ponovo.";
            return RedirectToAction(nameof(Login), new { returnUrl = safeReturnUrl });
        }

        try
        {
            var googlePrincipal = externalAuthResult.Principal;
            var email = googlePrincipal.FindFirst(ClaimTypes.Email)?.Value?.Trim();
            var isEmailVerified = bool.TryParse(
                googlePrincipal.FindFirst("urn:google:email_verified")?.Value,
                out var emailVerified) && emailVerified;

            if (string.IsNullOrWhiteSpace(email) || !isEmailVerified)
            {
                TempData["CartErrorMessage"] = "Google nalog nije vratio verifikovanu email adresu.";
                return RedirectToAction(nameof(Login), new { returnUrl = safeReturnUrl });
            }

            var customer = await _customerService.GetOrCreateByVerifiedEmailAsync(
                email,
                new StorefrontVerifiedIdentityData
                {
                    FirstName = googlePrincipal.FindFirst(ClaimTypes.GivenName)?.Value,
                    LastName = googlePrincipal.FindFirst(ClaimTypes.Surname)?.Value,
                    EmailVerifiedUtc = DateTime.UtcNow
                },
                HttpContext.RequestAborted);

            if (customer.Disabled)
            {
                TempData["CartErrorMessage"] = "Ovaj nalog trenutno nije dostupan.";
                return RedirectToAction(nameof(Login), new { returnUrl = safeReturnUrl });
            }

            customer.LastLoginUtc = DateTime.UtcNow;
            await _customerService.LinkOrdersByEmailAsync(customer, HttpContext.RequestAborted);
            await _dbContext.SaveChangesAsync(HttpContext.RequestAborted);
            await SignInCustomerAsync(customer, rememberMe: true);

            if (!IsProfileComplete(customer))
            {
                TempData["CartSuccessMessage"] = "Uspješno ste prijavljeni putem Google naloga. Dopunite profil za brzu kupovinu.";
                return RedirectToAction(nameof(Profile));
            }

            TempData["CartSuccessMessage"] = "Uspješno ste prijavljeni putem Google naloga.";
            return Redirect(safeReturnUrl ?? (Url.Action(nameof(Index)) ?? "/"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Google storefront login.");
            TempData["CartErrorMessage"] = "Google prijava trenutno nije dostupna. Pokušajte ponovo malo kasnije.";
            return RedirectToAction(nameof(Login), new { returnUrl = safeReturnUrl });
        }
        finally
        {
            await HttpContext.SignOutAsync(StorefrontAuthenticationConstants.ExternalAuthenticationScheme);
        }
    }

    [HttpGet("verifikacija")]
    [AllowAnonymous]
    public IActionResult VerifyLogin()
    {
        var pendingLogin = _pendingAccountLoginStore.Get();
        if (pendingLogin is null || pendingLogin.ExpiresUtc <= DateTime.UtcNow)
        {
            _pendingAccountLoginStore.Clear();
            TempData["CartErrorMessage"] = "Sesija za prijavu je istekla. Pokušajte ponovo.";
            return RedirectToAction(nameof(Login));
        }

        return View(new AccountLoginVerificationViewModel
        {
            MaskedEmail = StorefrontVerificationCodeHelper.MaskEmail(pendingLogin.Email),
            RememberMe = pendingLogin.RememberMe
        });
    }

    [HttpPost("verifikacija")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyLogin(AccountLoginVerificationViewModel form)
    {
        var pendingLogin = _pendingAccountLoginStore.Get();
        if (pendingLogin is null || pendingLogin.ExpiresUtc <= DateTime.UtcNow)
        {
            _pendingAccountLoginStore.Clear();
            TempData["CartErrorMessage"] = "Sesija za prijavu je istekla. Pokušajte ponovo.";
            return RedirectToAction(nameof(Login));
        }

        form.Code = (form.Code ?? string.Empty).Trim();
        form.MaskedEmail = StorefrontVerificationCodeHelper.MaskEmail(pendingLogin.Email);
        form.RememberMe = pendingLogin.RememberMe;

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var verificationResult = await _customerService.VerifyLoginCodeAsync(
            pendingLogin.Email,
            form.Code,
            HttpContext.RequestAborted);

        if (!verificationResult.Succeeded || verificationResult.Customer is null)
        {
            ModelState.AddModelError(nameof(form.Code), verificationResult.Message);
            return View(form);
        }

        if (verificationResult.Customer.Disabled)
        {
            ModelState.AddModelError(string.Empty, "Ovaj nalog trenutno nije dostupan.");
            return View(form);
        }

        _pendingAccountLoginStore.Clear();
        await SignInCustomerAsync(verificationResult.Customer, verificationResult.RememberMe);

        TempData["CartSuccessMessage"] = "Uspješno ste prijavljeni.";
        return Redirect(pendingLogin.ReturnUrl ?? (Url.Action(nameof(Index)) ?? "/"));
    }

    [HttpPost("verifikacija/ponovo")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendLoginCode()
    {
        var pendingLogin = _pendingAccountLoginStore.Get();
        if (pendingLogin is null)
        {
            TempData["CartErrorMessage"] = "Sesija za prijavu je istekla. Pokušajte ponovo.";
            return RedirectToAction(nameof(Login));
        }

        IssuedLoginCodeResult loginCode;
        try
        {
            loginCode = await _customerService.IssueLoginCodeAsync(
                pendingLogin.Email,
                pendingLogin.RememberMe,
                HttpContext.RequestAborted);

            await _emailService.SendAccountLoginCodeAsync(
                new AccountLoginEmailViewModel
                {
                    Email = loginCode.Email,
                    VerificationCode = loginCode.VerificationCode,
                    ExpiresInMinutes = _emailSettings.VerificationCodeLifetimeMinutes
                },
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend storefront account login code to {CustomerEmail}.", pendingLogin.Email);
            TempData["CartErrorMessage"] = "Nismo uspjeli da pošaljemo novi kod. Pokušajte ponovo.";
            return RedirectToAction(nameof(VerifyLogin));
        }

        pendingLogin.ExpiresUtc = loginCode.ExpiresUtc;
        _pendingAccountLoginStore.Save(pendingLogin);

        TempData["CartSuccessMessage"] = $"Novi kod za prijavu je poslat na {pendingLogin.Email}.";
        return RedirectToAction(nameof(VerifyLogin));
    }

    [HttpGet("profil")]
    [Authorize(AuthenticationSchemes = StorefrontAuthenticationConstants.AuthenticationScheme)]
    public async Task<IActionResult> Profile()
    {
        var customer = await RequireCurrentCustomerAsync();
        if (customer is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(MapProfile(customer));
    }

    [HttpPost("profil")]
    [Authorize(AuthenticationSchemes = StorefrontAuthenticationConstants.AuthenticationScheme)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(AccountProfileViewModel form)
    {
        var customer = await RequireCurrentCustomerAsync();
        if (customer is null)
        {
            return RedirectToAction(nameof(Login));
        }

        form.Email = customer.Email;
        form.FirstName = TextEncodingHelper.NormalizeInput(form.FirstName) ?? string.Empty;
        form.LastName = TextEncodingHelper.NormalizeInput(form.LastName) ?? string.Empty;
        form.Phone = TextEncodingHelper.NormalizeInput(form.Phone) ?? string.Empty;
        form.AddressLine1 = TextEncodingHelper.NormalizeInput(form.AddressLine1) ?? string.Empty;
        form.City = TextEncodingHelper.NormalizeInput(form.City) ?? string.Empty;
        form.Country = string.IsNullOrWhiteSpace(form.Country) ? "Crna Gora" : (TextEncodingHelper.NormalizeInput(form.Country) ?? "Crna Gora");
        form.AddressLine2 = TextEncodingHelper.NormalizeInput(form.AddressLine2);
        form.PostalCode = TextEncodingHelper.NormalizeInput(form.PostalCode);

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        await _customerService.SaveProfileAsync(
            customer,
            new StorefrontCustomerProfileData
            {
                FirstName = form.FirstName,
                LastName = form.LastName,
                Phone = form.Phone,
                AddressLine1 = form.AddressLine1,
                AddressLine2 = form.AddressLine2,
                City = form.City,
                PostalCode = form.PostalCode,
                Country = form.Country
            },
            HttpContext.RequestAborted);

        await SignInCustomerAsync(customer, rememberMe: true);
        TempData["CartSuccessMessage"] = "Profil je uspješno sačuvan.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("narudzbe")]
    [Authorize(AuthenticationSchemes = StorefrontAuthenticationConstants.AuthenticationScheme)]
    public async Task<IActionResult> Orders()
    {
        var customer = await RequireCurrentCustomerAsync();
        if (customer is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var orderEntities = await _dbContext.WebOrders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.StorefrontCustomerId == customer.Id)
            .OrderByDescending(order => order.CreatedUtc)
            .ToListAsync();

        var orders = orderEntities
            .Select(MapOrderListItem)
            .ToList();

        return View(new AccountOrdersViewModel
        {
            Orders = orders
        });
    }

    [HttpGet("narudzbe/{orderNumber}")]
    [Authorize(AuthenticationSchemes = StorefrontAuthenticationConstants.AuthenticationScheme)]
    public async Task<IActionResult> OrderDetails(string orderNumber)
    {
        var customer = await RequireCurrentCustomerAsync();
        if (customer is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var order = await _dbContext.WebOrders
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(item =>
                item.OrderNumber == orderNumber &&
                item.StorefrontCustomerId == customer.Id);

        if (order is null)
        {
            return NotFound();
        }

        var viewModel = new AccountOrderDetailsViewModel
        {
            OrderNumber = order.OrderNumber,
            CreatedUtc = order.CreatedUtc,
            Status = StorefrontStatusText.ToSerbianOrderStatus(order.Status),
            PaymentStatus = order.PaymentStatus,
            FulfillmentStatus = order.FulfillmentStatus,
            ItemsTotal = order.ItemsTotal,
            ShippingTotal = order.ShippingTotal,
            GrandTotal = order.GrandTotal,
            CustomerEmail = order.CustomerEmail ?? string.Empty,
            CustomerPhone = order.CustomerPhone ?? string.Empty,
            ShippingAddress = BuildShippingAddress(order),
            CustomerNote = TextEncodingHelper.Decode(order.CustomerNote),
            Lines = order.Items
                .Select(item => new AccountOrderLineViewModel
                {
                    ShortDescription = BuildOrderLineShortDescription(item),
                    Meta = BuildOrderLineMeta(item),
                    ImageUrl = item.PrimaryImageUrl,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal
                })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpPost("odjava")]
    [Authorize(AuthenticationSchemes = StorefrontAuthenticationConstants.AuthenticationScheme)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(StorefrontAuthenticationConstants.AuthenticationScheme);
        TempData["CartSuccessMessage"] = "Uspješno ste odjavljeni.";
        return RedirectToAction("Index", "Home");
    }

    private async Task<StorefrontCustomer?> RequireCurrentCustomerAsync()
    {
        return await _customerService.GetCurrentCustomerAsync(User, HttpContext.RequestAborted);
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
                    ? DateTimeOffset.UtcNow.AddDays(_storefrontSettings.RememberCustomerForDays)
                    : null
            });
    }

    private AccountProfileViewModel MapProfile(StorefrontCustomer customer)
    {
        return new AccountProfileViewModel
        {
            Email = customer.Email,
            FirstName = TextEncodingHelper.Decode(customer.FirstName) ?? string.Empty,
            LastName = TextEncodingHelper.Decode(customer.LastName) ?? string.Empty,
            Phone = TextEncodingHelper.Decode(customer.Phone) ?? string.Empty,
            AddressLine1 = TextEncodingHelper.Decode(customer.AddressLine1) ?? string.Empty,
            AddressLine2 = TextEncodingHelper.Decode(customer.AddressLine2),
            City = TextEncodingHelper.Decode(customer.City) ?? string.Empty,
            PostalCode = TextEncodingHelper.Decode(customer.PostalCode),
            Country = string.IsNullOrWhiteSpace(customer.Country) ? "Crna Gora" : (TextEncodingHelper.Decode(customer.Country) ?? customer.Country)
        };
    }

    private static bool IsProfileComplete(StorefrontCustomer customer)
    {
        return !string.IsNullOrWhiteSpace(customer.FirstName) &&
               !string.IsNullOrWhiteSpace(customer.LastName) &&
               !string.IsNullOrWhiteSpace(customer.Phone) &&
               !string.IsNullOrWhiteSpace(customer.AddressLine1) &&
               !string.IsNullOrWhiteSpace(customer.City) &&
               !string.IsNullOrWhiteSpace(customer.Country);
    }

    private string? GetSafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return null;
    }

    private AccountLoginViewModel CreateLoginViewModel(string? returnUrl = null)
    {
        return new AccountLoginViewModel
        {
            ReturnUrl = GetSafeReturnUrl(returnUrl),
            IsGoogleLoginAvailable = _googleAuthSettings.IsConfigured
        };
    }

    private static AccountOrderListItemViewModel MapOrderListItem(WebOrder order)
    {
        return new AccountOrderListItemViewModel
        {
            OrderNumber = order.OrderNumber,
            CreatedUtc = order.CreatedUtc,
            Status = StorefrontStatusText.ToSerbianOrderStatus(order.Status),
            PaymentStatus = order.PaymentStatus,
            FulfillmentStatus = order.FulfillmentStatus,
            GrandTotal = order.GrandTotal,
            TotalItems = order.Items.Sum(item => item.Quantity),
            PreviewImageUrls = BuildPreviewImageUrls(order.Items)
        };
    }

    private static IReadOnlyList<string> BuildPreviewImageUrls(IEnumerable<WebOrderItem> items)
    {
        return items
            .Select(item => item.PrimaryImageUrl?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Cast<string>()
            .ToList();
    }

    private static string BuildShippingAddress(WebOrder order)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                TextEncodingHelper.Decode(order.ShippingAddressLine1),
                TextEncodingHelper.Decode(order.ShippingAddressLine2),
                TextEncodingHelper.Decode(order.ShippingCity),
                TextEncodingHelper.Decode(order.ShippingPostalCode),
                TextEncodingHelper.Decode(order.ShippingCountry)
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildOrderLineTitle(WebOrderItem item)
    {
        var productName = TextEncodingHelper.Decode(item.ProductName) ?? item.ProductName;
        var model = TextEncodingHelper.Decode(item.Model) ?? item.Model;
        return string.IsNullOrWhiteSpace(model)
            ? productName
            : $"{productName} - {model}";
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
            parts.Add(TextEncodingHelper.Decode(item.Color) ?? item.Color);
        }

        if (item.Width.HasValue && item.Length.HasValue)
        {
            parts.Add($"{item.Width.Value} x {item.Length.Value} cm");
        }

        return string.Join(" | ", parts);
    }
}
