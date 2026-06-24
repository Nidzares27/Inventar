using System.Diagnostics;
using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Inventar.Storefront.Services;
using Inventar.Storefront.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventar.Storefront.Controllers;

public class HomeController : Controller
{
    private readonly StorefrontDbContext _dbContext;
    private readonly StorefrontSettings _settings;
    private readonly StorefrontPoMjeriInventoryService _poMjeriInventoryService;

    public HomeController(
        StorefrontDbContext dbContext,
        IOptions<StorefrontSettings> settings,
        StorefrontPoMjeriInventoryService poMjeriInventoryService)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
        _poMjeriInventoryService = poMjeriInventoryService;
    }

    public async Task<IActionResult> Index()
    {
        var publishedProductsQuery = _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsPublished && !product.Disabled && product.Slug != null);

        var featuredVariants = await publishedProductsQuery
            .Include(product => product.ProductImages)
            .ToListAsync();

        var availabilitySnapshot = await _poMjeriInventoryService.LoadSnapshotAsync(featuredVariants, cancellationToken: HttpContext.RequestAborted);
        var featuredProducts = StorefrontProductGrouping.SortGroups(
                StorefrontProductGrouping.GroupVariants(
                    featuredVariants,
                    featuredVariants.ToDictionary(product => product.Id, availabilitySnapshot.GetEffectiveAvailability)),
                "featured")
            .Take(8)
            .Select(StorefrontViewModelMapper.ToProductCard)
            .ToList();

        var collections = await publishedProductsQuery
            .Where(product =>
                product.BroaderCategory != null &&
                product.BroaderCategory != string.Empty &&
                product.BroaderCategory != StorefrontCategoryHelper.PlaceholderCategory)
            .Select(product => product.BroaderCategory)
            .Where(name => name != null && name != string.Empty)
            .Distinct()
            .OrderBy(name => name)
            .Take(6)
            .ToListAsync();

        var totalPublishedProducts = await publishedProductsQuery
            .Select(product => new { product.Name, product.Model })
            .Distinct()
            .CountAsync();

        var viewModel = new HomeIndexViewModel
        {
            BrandName = _settings.BrandName,
            Collections = collections,
            FeaturedProducts = featuredProducts,
            TotalPublishedProducts = totalPublishedProducts
        };

        return View(viewModel);
    }

    [HttpGet("akcije")]
    public async Task<IActionResult> Promotions()
    {
        var discountedVariants = await _dbContext.Products
            .AsNoTracking()
            .Where(product =>
                product.IsPublished &&
                !product.Disabled &&
                product.Slug != null &&
                product.OnlinePrice.HasValue &&
                product.OnlinePrice.Value < product.Price)
            .Include(product => product.ProductImages)
            .ToListAsync();

        var availabilitySnapshot = await _poMjeriInventoryService.LoadSnapshotAsync(discountedVariants, cancellationToken: HttpContext.RequestAborted);
        var discountedProducts = StorefrontProductGrouping.SortGroups(
                StorefrontProductGrouping.GroupVariants(
                    discountedVariants,
                    discountedVariants.ToDictionary(product => product.Id, availabilitySnapshot.GetEffectiveAvailability)),
                "featured")
            .Select(StorefrontViewModelMapper.ToProductCard)
            .ToList();

        var viewModel = new HomeIndexViewModel
        {
            BrandName = _settings.BrandName,
            FeaturedProducts = discountedProducts,
            TotalPublishedProducts = discountedProducts.Count
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
