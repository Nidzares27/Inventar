using Microsoft.AspNetCore.Mvc;

namespace Inventar.Storefront.Controllers;

public class InfoController : Controller
{
    [HttpGet("kontakt")]
    public IActionResult Contact()
    {
        return View();
    }

    [HttpGet("politika-privatnosti")]
    public IActionResult PrivacyPolicy()
    {
        return View();
    }

    [HttpGet("dostava")]
    public IActionResult Shipping()
    {
        return View();
    }
}
