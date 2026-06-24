using System.Text.Json;
using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Inventar.Storefront.Services;
using Inventar.Storefront.ViewModels.Cart;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Storefront.Controllers;

public class CartController : Controller
{
    private readonly StorefrontDbContext _dbContext;
    private readonly ICartService _cartService;
    private readonly StorefrontPoMjeriInventoryService _poMjeriInventoryService;

    public CartController(
        StorefrontDbContext dbContext,
        ICartService cartService,
        StorefrontPoMjeriInventoryService poMjeriInventoryService)
    {
        _dbContext = dbContext;
        _cartService = cartService;
        _poMjeriInventoryService = poMjeriInventoryService;
    }

    [HttpGet("korpa")]
    public async Task<IActionResult> Index()
    {
        return View(await BuildCartPageViewModelAsync());
    }

    [HttpPost("korpa/dodaj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        int productId,
        int quantity = 1,
        string? color = null,
        int? customWidth = null,
        int? customLength = null,
        string? returnUrl = null)
    {
        var cart = _cartService.GetCart().ToList();
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(item => item.ProductImages)
            .FirstOrDefaultAsync(item => item.Id == productId && item.IsPublished && !item.Disabled);

        if (product == null)
        {
            TempData["CartErrorMessage"] = "Odabrani proizvod trenutno nije dostupan.";
            return Redirect(ResolveReturnUrl(returnUrl));
        }

        if (product.PoMjeri)
        {
            var normalizedWidth = customWidth ?? 0;
            var normalizedLength = customLength ?? 0;

            var variants = await LoadGroupVariantsAsync(product);
            var evaluation = await EvaluatePoMjeriAsync(
                variants,
                color ?? product.Color,
                normalizedWidth,
                normalizedLength,
                quantity,
                cart);

            if (!evaluation.IsValid || evaluation.BestPlan == null)
            {
                TempData["CartErrorMessage"] = evaluation.Message;
                return Redirect(ResolveReturnUrl(returnUrl));
            }

            var sourceLookup = variants.ToDictionary(item => item.Id);
            var pricingProduct = StorefrontPricingHelper.SelectPoMjeriPricingProduct(sourceLookup, evaluation.BestPlan.Slices)
                ?? StorefrontPricingHelper.SelectPoMjeriPricingProduct(variants, color ?? product.Color, normalizedWidth)
                ?? product;
            var pricing = StorefrontPricingHelper.BuildPoMjeriPricing(sourceLookup, evaluation.BestPlan.Slices, normalizedLength);

            var normalizedPoMjeriQuantity = Math.Min(Math.Max(quantity, 1), evaluation.MaxAvailableQuantity);

            cart.Add(new CartItem
            {
                LineId = Guid.NewGuid().ToString("N"),
                ProductId = pricingProduct.Id,
                Quantity = normalizedPoMjeriQuantity,
                PoMjeri = true,
                CustomWidth = normalizedWidth,
                CustomLength = normalizedLength,
                SelectedColor = pricingProduct.Color,
                Allocations = evaluation.BestPlan.Slices
                    .Select(slice => new CartItemAllocation
                    {
                        SourceProductId = slice.ProductId,
                        Quantity = slice.Quantity,
                        ConsumedLengthPerUnit = slice.ConsumedLengthPerUnit
                    })
                    .ToList()
            });

            _cartService.Store(cart);
            TempData["CartAddedNotification"] = JsonSerializer.Serialize(
                StorefrontViewModelMapper.ToCartAddedNotification(
                    pricingProduct,
                    normalizedPoMjeriQuantity,
                    normalizedWidth,
                    normalizedLength,
                    pricingProduct.Color,
                    pricing));

            return Redirect(ResolveReturnUrl(returnUrl));
        }

        if (product.AvailableQuantity <= 0)
        {
            TempData["CartErrorMessage"] = "Odabrani proizvod trenutno nije dostupan.";
            return Redirect(ResolveReturnUrl(returnUrl));
        }

        var existingLine = cart.FirstOrDefault(line => !line.PoMjeri && line.ProductId == productId);
        var existingQuantity = existingLine?.Quantity ?? 0;
        var normalizedQuantity = Math.Min(
            Math.Max(quantity, 1),
            StorefrontStockRules.GetMaxOrderQuantity(product.AvailableQuantity));

        if (existingLine == null)
        {
            cart.Add(new CartItem
            {
                LineId = Guid.NewGuid().ToString("N"),
                ProductId = product.Id,
                Quantity = normalizedQuantity
            });
        }
        else
        {
            existingLine.Quantity = Math.Min(
                existingLine.Quantity + Math.Max(quantity, 1),
                StorefrontStockRules.GetMaxOrderQuantity(product.AvailableQuantity));
        }

        _cartService.Store(cart);

        var updatedQuantity = cart
            .First(line => !line.PoMjeri && line.ProductId == productId)
            .Quantity;

        var quantityAdded = Math.Max(updatedQuantity - existingQuantity, 0);
        if (quantityAdded <= 0)
        {
            TempData["CartErrorMessage"] = StorefrontStockRules.BuildQuantityLimitMessage(existingQuantity + quantity, product.AvailableQuantity);
            return Redirect(ResolveReturnUrl(returnUrl));
        }

        TempData["CartAddedNotification"] = JsonSerializer.Serialize(
            StorefrontViewModelMapper.ToCartAddedNotification(
                product,
                quantityAdded,
                product.Width,
                product.Length));

        return Redirect(ResolveReturnUrl(returnUrl));
    }

    [HttpPost("korpa/azuriraj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string lineId, int quantity)
    {
        var cart = _cartService.GetCart().ToList();
        var cartLine = cart.FirstOrDefault(item => string.Equals(item.LineId, lineId, StringComparison.Ordinal));
        if (cartLine == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(item => item.ProductImages)
            .FirstOrDefaultAsync(item => item.Id == cartLine.ProductId && item.IsPublished && !item.Disabled);

        if (product == null)
        {
            cart.Remove(cartLine);
            _cartService.Store(cart);
            var removedResponse = await BuildCartUpdateResponseAsync(lineId, "Proizvod vise nije u ponudi.", removed: true);
            if (IsAjaxRequest())
            {
                return Json(removedResponse);
            }

            TempData["CartErrorMessage"] = removedResponse.Message;
            return RedirectToAction(nameof(Index));
        }

        if (cartLine.PoMjeri)
        {
            var variants = await LoadGroupVariantsAsync(product);
            var evaluation = await EvaluatePoMjeriAsync(
                variants,
                cartLine.SelectedColor ?? product.Color,
                cartLine.CustomWidth ?? 0,
                cartLine.CustomLength ?? 0,
                quantity,
                cart,
                cartLine.LineId);

            var maxQuantity = evaluation.MaxAvailableQuantity;
            if (maxQuantity <= 0)
            {
                cart.Remove(cartLine);
                _cartService.Store(cart);
                var unavailableResponse = await BuildCartUpdateResponseAsync(lineId, "Odabrani proizvod trenutno nije dostupan.", removed: true);
                if (IsAjaxRequest())
                {
                    return Json(unavailableResponse);
                }

                TempData["CartErrorMessage"] = unavailableResponse.Message;
                return RedirectToAction(nameof(Index));
            }

            var normalizedQuantity = Math.Min(Math.Max(quantity, 1), maxQuantity);
            var normalizedEvaluation = normalizedQuantity == quantity
                ? evaluation
                : await EvaluatePoMjeriAsync(
                    variants,
                    cartLine.SelectedColor ?? product.Color,
                    cartLine.CustomWidth ?? 0,
                    cartLine.CustomLength ?? 0,
                    normalizedQuantity,
                    cart,
                    cartLine.LineId);

            if (!normalizedEvaluation.IsValid || normalizedEvaluation.BestPlan == null)
            {
                cart.Remove(cartLine);
                _cartService.Store(cart);
                var invalidResponse = await BuildCartUpdateResponseAsync(lineId, normalizedEvaluation.Message, removed: true);
                if (IsAjaxRequest())
                {
                    return Json(invalidResponse);
                }

                TempData["CartErrorMessage"] = invalidResponse.Message;
                return RedirectToAction(nameof(Index));
            }

            cartLine.Quantity = normalizedQuantity;
            cartLine.Allocations = normalizedEvaluation.BestPlan.Slices
                .Select(slice => new CartItemAllocation
                {
                    SourceProductId = slice.ProductId,
                    Quantity = slice.Quantity,
                    ConsumedLengthPerUnit = slice.ConsumedLengthPerUnit
                })
                .ToList();
            _cartService.Store(cart);

            var message = normalizedQuantity != quantity
                ? $"Moguće je naručiti najviše {maxQuantity} komada za dati proizvod."
                : string.Empty;

            if (IsAjaxRequest())
            {
                return Json(await BuildCartUpdateResponseAsync(lineId, message));
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                TempData["CartErrorMessage"] = message;
            }

            return RedirectToAction(nameof(Index));
        }

        var requestedQuantity = Math.Max(quantity, 1);
        var maxOrderQuantity = StorefrontStockRules.GetMaxOrderQuantity(product.AvailableQuantity);
        if (maxOrderQuantity <= 0)
        {
            cart.Remove(cartLine);
            _cartService.Store(cart);
            var unavailableResponse = await BuildCartUpdateResponseAsync(lineId, "Odabrani proizvod trenutno nije dostupan.", removed: true);
            if (IsAjaxRequest())
            {
                return Json(unavailableResponse);
            }

            TempData["CartErrorMessage"] = unavailableResponse.Message;
            return RedirectToAction(nameof(Index));
        }

        var normalizedRegularQuantity = Math.Min(requestedQuantity, maxOrderQuantity);
        cartLine.Quantity = normalizedRegularQuantity;
        _cartService.Store(cart);

        var regularMessage = normalizedRegularQuantity != requestedQuantity
            ? StorefrontStockRules.BuildQuantityLimitMessage(requestedQuantity, product.AvailableQuantity)
            : string.Empty;

        if (IsAjaxRequest())
        {
            return Json(await BuildCartUpdateResponseAsync(lineId, regularMessage));
        }

        if (!string.IsNullOrWhiteSpace(regularMessage))
        {
            TempData["CartErrorMessage"] = regularMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("korpa/ukloni")]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(string lineId)
    {
        _cartService.Remove(lineId);
        return RedirectToAction(nameof(Index));
    }

    private async Task<CartPageViewModel> BuildCartPageViewModelAsync()
    {
        var cartItems = _cartService.GetCart().ToList();
        if (cartItems.Count == 0)
        {
            return new CartPageViewModel();
        }

        var productIds = cartItems
            .Select(item => item.ProductId)
            .Concat(cartItems.SelectMany(item => item.Allocations.Select(allocation => allocation.SourceProductId)))
            .Distinct()
            .ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Include(product => product.ProductImages)
            .Where(product => productIds.Contains(product.Id) && !product.Disabled && product.IsPublished)
            .ToDictionaryAsync(product => product.Id);

        var lines = new List<CartLineViewModel>();
        var cartChanged = false;

        foreach (var item in cartItems.ToList())
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                cartItems.Remove(item);
                cartChanged = true;
                continue;
            }

            if (item.PoMjeri)
            {
                var variants = await LoadGroupVariantsAsync(product);
                var evaluation = await EvaluatePoMjeriAsync(
                    variants,
                    item.SelectedColor ?? product.Color,
                    item.CustomWidth ?? 0,
                    item.CustomLength ?? 0,
                    item.Quantity,
                    cartItems,
                    item.LineId);

                var maxQuantity = evaluation.MaxAvailableQuantity;
                if (maxQuantity <= 0)
                {
                    cartItems.Remove(item);
                    cartChanged = true;
                    continue;
                }

                var normalizedQuantity = Math.Min(item.Quantity, maxQuantity);
                if (normalizedQuantity != item.Quantity || !SameAllocations(item.Allocations, evaluation.BestPlan?.Slices))
                {
                    var normalizedEvaluation = normalizedQuantity == item.Quantity && evaluation.BestPlan != null
                        ? evaluation
                        : await EvaluatePoMjeriAsync(
                            variants,
                            item.SelectedColor ?? product.Color,
                            item.CustomWidth ?? 0,
                            item.CustomLength ?? 0,
                            normalizedQuantity,
                            cartItems,
                            item.LineId);

                    if (!normalizedEvaluation.IsValid || normalizedEvaluation.BestPlan == null)
                    {
                        cartItems.Remove(item);
                        cartChanged = true;
                        continue;
                    }

                    item.Quantity = normalizedQuantity;
                    item.Allocations = normalizedEvaluation.BestPlan.Slices
                        .Select(slice => new CartItemAllocation
                        {
                            SourceProductId = slice.ProductId,
                            Quantity = slice.Quantity,
                            ConsumedLengthPerUnit = slice.ConsumedLengthPerUnit
                        })
                        .ToList();
                    cartChanged = true;
                }

                var sourceLookup = item.Allocations
                    .Select(allocation => products.GetValueOrDefault(allocation.SourceProductId))
                    .Where(sourceProduct => sourceProduct != null)
                    .ToDictionary(sourceProduct => sourceProduct!.Id, sourceProduct => sourceProduct!);
                var pricing = sourceLookup.Count > 0
                    ? StorefrontPricingHelper.BuildPoMjeriPricing(sourceLookup, item.Allocations, item.CustomLength)
                    : StorefrontPricingHelper.BuildPoMjeriPricing(product.EffectivePrice, product.Price, item.CustomLength);

                lines.Add(StorefrontViewModelMapper.ToCartLine(product, item, maxQuantity, pricing));
                continue;
            }

            var normalizedRegularQuantity = Math.Min(item.Quantity, StorefrontStockRules.GetMaxOrderQuantity(product.AvailableQuantity));
            if (normalizedRegularQuantity <= 0)
            {
                cartItems.Remove(item);
                cartChanged = true;
                continue;
            }

            if (normalizedRegularQuantity != item.Quantity)
            {
                item.Quantity = normalizedRegularQuantity;
                cartChanged = true;
            }

            lines.Add(StorefrontViewModelMapper.ToCartLine(product, item, StorefrontStockRules.GetMaxOrderQuantity(product.AvailableQuantity)));
        }

        if (cartChanged)
        {
            _cartService.Store(cartItems);
        }

        return new CartPageViewModel
        {
            Lines = lines,
            Subtotal = lines.Sum(line => line.LineTotal),
            TotalItems = lines.Sum(line => line.Quantity)
        };
    }

    private async Task<PoMjeriPlanResult> EvaluatePoMjeriAsync(
        IReadOnlyCollection<StorefrontProduct> variants,
        string selectedColor,
        int customWidth,
        int customLength,
        int quantity,
        IReadOnlyCollection<CartItem> cartItems,
        string? excludeLineId = null)
    {
        var snapshot = await _poMjeriInventoryService.LoadSnapshotAsync(
            variants,
            cartItems,
            excludeLineId,
            HttpContext.RequestAborted);

        return StorefrontPoMjeriPlanner.Evaluate(
            variants,
            snapshot,
            selectedColor,
            customWidth,
            customLength,
            quantity);
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

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<CartUpdateResponse> BuildCartUpdateResponseAsync(string lineId, string message, bool removed = false)
    {
        var viewModel = await BuildCartPageViewModelAsync();
        var line = viewModel.Lines.FirstOrDefault(item => string.Equals(item.LineId, lineId, StringComparison.Ordinal));

        return new CartUpdateResponse
        {
            LineId = lineId,
            Removed = removed || line == null,
            CartEmpty = viewModel.Lines.Count == 0,
            Quantity = line?.Quantity ?? 0,
            MaxOrderQuantity = line?.MaxOrderQuantity ?? 0,
            LineTotal = line?.LineTotal ?? 0m,
            LineTotalFormatted = $"{(line?.LineTotal ?? 0m):0.00} €",
            Subtotal = viewModel.Subtotal,
            SubtotalFormatted = $"{viewModel.Subtotal:0.00} €",
            TotalItems = viewModel.TotalItems,
            Message = message
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

    private static bool SameAllocations(
        IReadOnlyList<CartItemAllocation> currentAllocations,
        IReadOnlyList<PoMjeriAllocationSlice>? planSlices)
    {
        if (planSlices == null || currentAllocations.Count != planSlices.Count)
        {
            return false;
        }

        for (var index = 0; index < currentAllocations.Count; index++)
        {
            var left = currentAllocations[index];
            var right = planSlices[index];
            if (left.SourceProductId != right.ProductId ||
                left.Quantity != right.Quantity ||
                left.ConsumedLengthPerUnit != right.ConsumedLengthPerUnit)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class CartUpdateResponse
    {
        public string LineId { get; set; } = string.Empty;
        public bool Removed { get; set; }
        public bool CartEmpty { get; set; }
        public int Quantity { get; set; }
        public int MaxOrderQuantity { get; set; }
        public decimal LineTotal { get; set; }
        public string LineTotalFormatted { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public string SubtotalFormatted { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
