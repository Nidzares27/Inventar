using Inventar.Data;
using Inventar.Models;
using Inventar.Services;
using Inventar.Utils;
using Inventar.ViewModels.Login_Register;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;

namespace Inventar.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ISessionService _sessionService;

        public HomeController(ILogger<HomeController> logger, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ApplicationDbContext context, IEmailSender emailSender, ISessionService sessionService)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailSender = emailSender;
            _sessionService = sessionService;
        }
        [Authorize(Roles = "admin,superadmin")]
        public async Task<IActionResult> AllAccounts()
        {
            try
            {
                var users = _userManager.Users.ToList();
                var userList = new List<UserWithRoleViewModel>();

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userList.Add(new UserWithRoleViewModel
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        Role = roles.FirstOrDefault() ?? ""
                    });
                }

                return View(userList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading user accounts.");
                return View("Index");
            }
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "InventoryItem");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel LoginViewModel)
        {
            if (!ModelState.IsValid) return View(LoginViewModel);

            try
            {
                const string genericLoginErrorMessage = "Invalid login attempt. Please try again.";
                var user = await _userManager.FindByEmailAsync(LoginViewModel.EmailAddress);

                if (user != null)
                {
                    var result = await _signInManager.PasswordSignInAsync(user, LoginViewModel.Password, false, true);
                    if (result.Succeeded)
                    {
                        ViewBag.FullName = $"{user.FirstName} {user.LastName}";
                        return RedirectToAction("Index", "InventoryItem");
                    }

                    if (result.IsLockedOut)
                    {
                        TempData["Error"] = "This account is temporarily locked. Please try again later.";
                        _logger.LogWarning("User account locked out after repeated failed sign-in attempts for email {Email}.", LoginViewModel.EmailAddress);
                        return View(LoginViewModel);
                    }
                }

                TempData["Error"] = genericLoginErrorMessage;
                _logger.LogWarning("Failed login attempt for email {Email}.", LoginViewModel.EmailAddress);
                return View(LoginViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login for email: {Email}", LoginViewModel.EmailAddress);
                TempData["Error"] = "An unexpected error occurred. Please try again later.";
                return View(LoginViewModel);
            }
        }
        [Authorize(Roles = "admin,superadmin")]
        public IActionResult Register()
        {
            ViewData["ActivePage"] = "Register";
            var response = new RegisterViewModel();
            return View(response);
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel registerViewModel)
        {
            if (!ModelState.IsValid) return View(registerViewModel);

            var allowedRoles = User.IsInRole("superadmin")
                ? new[] { "user", "employee", "admin", "superadmin" }
                : new[] { "user", "employee", "admin" };

            if (!allowedRoles.Contains(registerViewModel.UserRole, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(registerViewModel.UserRole), "Selected role is not allowed.");
                return View(registerViewModel);
            }

            try
            {
                var user = await _userManager.FindByEmailAsync(registerViewModel.EmailAddress);

                if (user != null)
                {
                    TempData["Error"] = "This email address is already in use";
                    _logger.LogWarning("Tried to create new user with an email address that's already in use: {Email}", registerViewModel.EmailAddress);
                    return View(registerViewModel);
                }

                var newUser = new AppUser()
                {
                    FirstName = registerViewModel.FirstName,
                    LastName = registerViewModel.LastName,
                    Email = registerViewModel.EmailAddress,
                    UserName = registerViewModel.EmailAddress
                };

                var newUserResponse = await _userManager.CreateAsync(newUser, registerViewModel.Password);

                if (newUserResponse.Succeeded)
                {
                    await _userManager.AddToRoleAsync(newUser, registerViewModel.UserRole);
                    await _userManager.AddClaimAsync(newUser, new Claim(ClaimTypes.GivenName, registerViewModel.FirstName));
                    await _userManager.AddClaimAsync(newUser, new Claim(ClaimTypes.Surname, registerViewModel.LastName));

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    foreach (var error in newUserResponse.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    _logger.LogError("Error occurred during creation of a new user for email {Email}", registerViewModel.EmailAddress);
                    return View(registerViewModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during user registration for email {Email}", registerViewModel.EmailAddress);
                TempData["Error"] = "An unexpected error occurred during registration. Please try again later.";
                return View(registerViewModel);
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
                _sessionService.ClearScannedProducts(HttpContext.Session);
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during logout.");
                TempData["Error"] = "An error occurred while logging out. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // User not found; don't reveal this to the client
                return RedirectToAction("ForgotPasswordConfirmation");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Home", new { token, userId = user.Id }, Request.Scheme);

            await _emailSender.SendEmailAsync(model.Email, "Reset Password", $"Reset your password by clicking <a href='{resetLink}'>here</a>");

            return RedirectToAction("ForgotPasswordConfirmation");
        }

        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string userId)
        {
            if (token == null || userId == null)
                return BadRequest("Invalid password reset token.");

            var model = new ResetPasswordViewModel { Token = token, UserId = userId };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                // User not found; don't reveal this to the client
                return RedirectToAction("ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
                return RedirectToAction("ResetPasswordConfirmation");

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string email, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning("Tried to change a password but something went wrong trying to find an user by email: {Email}", email);
                    return NotFound("Couldn't find an user with provided email!");
                }

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("superadmin", StringComparer.OrdinalIgnoreCase) && !User.IsInRole("superadmin"))
                {
                    _logger.LogWarning("Non-superadmin user attempted to reset the password of a superadmin account: {Email}", email);
                    TempData["ErrorMessage"] = "You are not allowed to reset this account password.";
                    return RedirectToAction(nameof(AllAccounts));
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = $"Password reset for {email} was successful.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to reset password: {string.Join(", ", result.Errors.Select(e => e.Description))}";
                    _logger.LogWarning("Reseting password failed!");
                    return RedirectToAction(nameof(AllAccounts));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while resetting password for {Email}", email);
                TempData["ErrorMessage"] = "An unexpected error occurred while resetting the password.";
                return RedirectToAction(nameof(AllAccounts));

            }

            return RedirectToAction(nameof(AllAccounts));
        }

        [HttpPost]
        [Authorize(Roles = "admin,superadmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    _logger.LogWarning("Tried to delete an user, but something went wrong trying to find an user by email: {Email}", email);
                    return RedirectToAction("AllAccounts");
                }

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("superadmin", StringComparer.OrdinalIgnoreCase) && !User.IsInRole("superadmin"))
                {
                    _logger.LogWarning("Non-superadmin user attempted to delete a superadmin account: {Email}", email);
                    TempData["ErrorMessage"] = "You are not allowed to delete this account.";
                    return RedirectToAction("AllAccounts");
                }

                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "User successfully deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to delete user: {string.Join(", ", result.Errors.Select(e => e.Description))}";
                    _logger.LogError("Deleting user failed!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting user with email {Email}", email);
                TempData["ErrorMessage"] = "An unexpected error occurred while trying to delete the user.";
            }

            return RedirectToAction("AllAccounts");
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(); // Views/Home/Error.cshtml
            //return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult ChangeLanguage(string? lang)
        {
            try
            {
                var cultureName = LocalizationSettings.GetSupportedCultureOrDefault(lang);

                if (!LocalizationSettings.TryGetSupportedCulture(lang, out _))
                {
                    _logger.LogWarning("Unsupported culture change requested: {Lang}", lang);
                }

                CultureInfo.CurrentCulture = LocalizationSettings.CreateCultureInfo(cultureName);
                CultureInfo.CurrentUICulture = LocalizationSettings.CreateCultureInfo(cultureName);

                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax
                };

                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureName)),
                    cookieOptions);

                Response.Cookies.Append("Language", cultureName, cookieOptions);

                return Redirect(GetSafeReturnUrl());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while changing language to {Lang}", lang);
                TempData["ErrorMessage"] = "Could not change language due to an unexpected error.";
            }

            return Redirect(GetSafeReturnUrl());
        }

        private string GetSafeReturnUrl()
        {
            var referer = Request.GetTypedHeaders().Referer;
            if (referer is not null &&
                Uri.TryCreate($"{Request.Scheme}://{Request.Host}", UriKind.Absolute, out var currentRequestUri) &&
                string.Equals(referer.Host, currentRequestUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                var localUrl = referer.PathAndQuery + referer.Fragment;
                if (Url.IsLocalUrl(localUrl))
                {
                    return localUrl;
                }
            }

            return Url.Action("Index", "Home") ?? "/";
        }

        [HttpGet]
        public IActionResult ThrowError()
        {
            throw new InvalidOperationException("Test exception for global error handler.");
        }
    }
}
