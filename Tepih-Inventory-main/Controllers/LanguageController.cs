using Microsoft.AspNetCore.Mvc;

namespace Inventar.Controllers
{
    public class LanguageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ChangeLanguage(string culture)
        {
            // Save the selected culture to the session
            HttpContext.Session.SetString("culture", culture);

            // Redirect back to the previous page or home
            return Redirect(Request.Headers["Referer"].ToString() ?? "/");
        }
    }
}
