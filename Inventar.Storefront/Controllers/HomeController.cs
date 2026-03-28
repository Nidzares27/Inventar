using Inventar.Storefront.Models;
using Inventar.Storefront.Data;
using Inventar.Storefront.Services;
using Inventar.Storefront.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Inventar.Storefront.Controllers;

public class HomeController : Controller
{
    private readonly StorefrontDbContext _dbContext;
    private readonly StorefrontSettings _settings;

    public HomeController(StorefrontDbContext dbContext, IOptions<StorefrontSettings> settings)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
    }

    public async Task<IActionResult> Index()
    {
        var publishedProductsQuery = _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsPublished && !product.Disabled && product.Slug != null);

        var featuredProducts = await publishedProductsQuery
            .Include(product => product.ProductImages)
            .Where(product => product.Quantity > product.ReservedQuantity)
            .OrderByDescending(product => product.Id)
            .Take(8)
            .ToListAsync();

        var collections = await publishedProductsQuery
            .Select(product => product.Name)
            .Where(name => name != null && name != string.Empty)
            .Distinct()
            .OrderBy(name => name)
            .Take(6)
            .ToListAsync();

        var viewModel = new HomeIndexViewModel
        {
            BrandName = _settings.BrandName,
            Collections = collections,
            FeaturedProducts = featuredProducts.Select(StorefrontViewModelMapper.ToProductCard).ToList(),
            TotalPublishedProducts = await publishedProductsQuery.CountAsync()
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
