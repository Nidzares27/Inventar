using Inventar.Utils;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Inventar.Controllers
{
    public class LanguageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ChangeLanguage(string? culture)
        {
            var cultureName = LocalizationSettings.GetSupportedCultureOrDefault(culture);
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

            var referer = Request.Headers["Referer"].ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri) &&
                Uri.TryCreate($"{Request.Scheme}://{Request.Host}", UriKind.Absolute, out var currentUri) &&
                string.Equals(refererUri.Host, currentUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(refererUri.PathAndQuery + refererUri.Fragment);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
