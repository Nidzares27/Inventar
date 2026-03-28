using System.Text.Json;
using Inventar.Storefront.Data;
using Inventar.Storefront.Services;
using Inventar.Storefront.ViewModels.Cart;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Storefront.Controllers;

public class CartController : Controller
{
    private readonly StorefrontDbContext _dbContext;
    private readonly ICartService _cartService;

    public CartController(StorefrontDbContext dbContext, ICartService cartService)
    {
        _dbContext = dbContext;
        _cartService = cartService;
    }

    [HttpGet("korpa")]
    public async Task<IActionResult> Index()
    {
        return View(await BuildCartPageViewModelAsync());
    }

    [HttpPost("korpa/dodaj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1, string? returnUrl = null)
    {
        var existingQuantity = _cartService.GetCart()
            .FirstOrDefault(line => line.ProductId == productId)?
            .Quantity ?? 0;

        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(item => item.ProductImages)
            .FirstOrDefaultAsync(item => item.Id == productId && item.IsPublished && !item.Disabled);

        if (product == null || product.AvailableQuantity <= 0)
        {
            TempData["CartErrorMessage"] = "Odabrani proizvod trenutno nije dostupan.";
            return Redirect(ResolveReturnUrl(returnUrl));
        }

        _cartService.AddOrIncrement(product.Id, quantity, product.AvailableQuantity);

        var updatedQuantity = _cartService.GetCart()
            .FirstOrDefault(line => line.ProductId == productId)?
            .Quantity ?? 0;

        var quantityAdded = Math.Max(updatedQuantity - existingQuantity, 0);
        if (quantityAdded <= 0)
        {
            TempData["CartErrorMessage"] = "Proizvod je vec dodat u maksimalnoj dostupnoj kolicini.";
            return Redirect(ResolveReturnUrl(returnUrl));
        }

        var notification = StorefrontViewModelMapper.ToCartAddedNotification(product, quantityAdded);
        TempData["CartAddedNotification"] = JsonSerializer.Serialize(notification);

        return Redirect(ResolveReturnUrl(returnUrl));
    }

    [HttpPost("korpa/azuriraj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int productId, int quantity)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == productId && item.IsPublished && !item.Disabled);

        if (product == null)
        {
            _cartService.Remove(productId);
            TempData["CartErrorMessage"] = "Proizvod više nije u ponudi.";
            return RedirectToAction(nameof(Index));
        }

        if (quantity <= 0)
        {
            _cartService.Remove(productId);
        }
        else
        {
            _cartService.SetQuantity(productId, quantity, product.AvailableQuantity);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("korpa/ukloni")]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int productId)
    {
        _cartService.Remove(productId);
        return RedirectToAction(nameof(Index));
    }

    private async Task<CartPageViewModel> BuildCartPageViewModelAsync()
    {
        var cartItems = _cartService.GetCart();
        if (cartItems.Count == 0)
        {
            return new CartPageViewModel();
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

        return new CartPageViewModel
        {
            Lines = lines,
            Subtotal = lines.Sum(line => line.LineTotal),
            TotalItems = lines.Sum(line => line.Quantity)
        };
    }

    private string ResolveReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return Url.Action(nameof(Index)) ?? "/korpa";
    }
}
