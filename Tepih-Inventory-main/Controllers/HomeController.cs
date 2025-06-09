using Inventar.Data;
using Inventar.Models;
using Inventar.ViewModels.Login_Register;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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

        public HomeController(ILogger<HomeController> logger, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ApplicationDbContext context, IEmailSender emailSender)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailSender = emailSender;
        }
        [Authorize]
        public async Task<IActionResult> AllAccounts()
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
            IEnumerable<UserWithRoleViewModel> vvv = userList;

            return View(vvv);
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(LoginViewModel LoginViewModel)
        {
            if (!ModelState.IsValid) return View(LoginViewModel);

            var user = await _userManager.FindByEmailAsync(LoginViewModel.EmailAddress);

            if (user != null)
            {
                var passwordCheck = await _userManager.CheckPasswordAsync(user, LoginViewModel.Password);
                if (passwordCheck)
                {
                    var result = await _signInManager.PasswordSignInAsync(user, LoginViewModel.Password, false, false);
                    if (result.Succeeded)
                    {
                        ViewBag.FullName = $"{user.FirstName} {user.LastName}";
                        return RedirectToAction("Index", "InventoryItem");
                    }
                }
                TempData["Error"] = "Wrong credentials. Please try again";
                return View(LoginViewModel);
            }
            TempData["Error"] = "Wrong credentials. Please try again";
            return View(LoginViewModel);
        }
        [Authorize]
        public IActionResult Register()
        {
            ViewData["ActivePage"] = "Register";
            var response = new RegisterViewModel();
            return View(response);
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel registerViewModel)
        {
            if (!ModelState.IsValid) return View(registerViewModel);

            var user = await _userManager.FindByEmailAsync(registerViewModel.EmailAddress);

            if (user != null)
            {
                TempData["Error"] = "This email address is already in use";
                return View(registerViewModel);
            }

            var newUser = new AppUser()
            {
                FirstName = registerViewModel.FirstName,
                LastName = registerViewModel.LastName,
                Email = registerViewModel.EmailAddress,
                UserName = registerViewModel.FirstName + registerViewModel.LastName,
            };

            var newUserResponse = await _userManager.CreateAsync(newUser, registerViewModel.Password);

            if (newUserResponse.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, registerViewModel.UserRole);
                //await _userManager.AddToRoleAsync(newUser, UserRoles.Admin);
            }
            else
            {
                foreach (var error in newUserResponse.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View();
            }
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
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
        public IActionResult ResetPassword(string token, string userId)
        {
            if (token == null || userId == null)
                return BadRequest("Invalid password reset token.");

            var model = new ResetPasswordViewModel { Token = token, UserId = userId };
            return View(model);
        }

        [HttpPost]
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
        public async Task<IActionResult> ChangePassword(string email, string newPassword)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound();
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
            }

            return RedirectToAction(nameof(AllAccounts));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("AllAccounts");
            }

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User successfully deleted.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete user.";
            }

            return RedirectToAction("AllAccounts");
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult ChangeLanguage (string lang)
        {
            if (!string.IsNullOrEmpty(lang))
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(lang);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);
            }
            else
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en");
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");
            }
            Response.Cookies.Append("Language",lang);
            return Redirect(Request.GetTypedHeaders().Referer.ToString());
        }
    }
}