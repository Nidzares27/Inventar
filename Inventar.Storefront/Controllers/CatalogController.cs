using Inventar.Storefront.Data;
using Inventar.Storefront.Services;
using Inventar.Storefront.ViewModels.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Storefront.Controllers;

public class CatalogController : Controller
{
    private const int PageSize = 12;

    private readonly StorefrontDbContext _dbContext;

    public CatalogController(StorefrontDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("proizvodi")]
    public async Task<IActionResult> Index(string? q, string? collection, string? color, string sort = "featured", int page = 1)
    {
        page = Math.Max(page, 1);

        var filtersQuery = _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsPublished && !product.Disabled && product.Slug != null);

        var query = _dbContext.Products
            .AsNoTracking()
            .Include(product => product.ProductImages)
            .Where(product => product.IsPublished && !product.Disabled && product.Slug != null && product.Quantity > product.ReservedQuantity);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            query = query.Where(product =>
                product.Name.Contains(search) ||
                product.Model.Contains(search) ||
                product.ProductNumber.Contains(search) ||
                product.Color.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(collection))
        {
            query = query.Where(product => product.Name == collection);
        }

        if (!string.IsNullOrWhiteSpace(color))
        {
            query = query.Where(product => product.Color == color);
        }

        query = sort switch
        {
            "price-asc" => query.OrderBy(product => product.OnlinePrice ?? product.Price).ThenBy(product => product.Name),
            "price-desc" => query.OrderByDescending(product => product.OnlinePrice ?? product.Price).ThenBy(product => product.Name),
            "name" => query.OrderBy(product => product.Name).ThenBy(product => product.Model),
            _ => query.OrderByDescending(product => product.Id)
        };

        var totalCount = await query.CountAsync();
        var products = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var collections = await filtersQuery
            .Select(product => product.Name)
            .Where(name => name != null && name != string.Empty)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var colors = await filtersQuery
            .Select(product => product.Color)
            .Where(colorValue => colorValue != null && colorValue != string.Empty)
            .Distinct()
            .OrderBy(colorValue => colorValue)
            .ToListAsync();

        var viewModel = new CatalogIndexViewModel
        {
            Products = products.Select(StorefrontViewModelMapper.ToProductCard).ToList(),
            Collections = collections,
            Colors = colors,
            Query = q,
            Collection = collection,
            Color = color,
            Sort = sort,
            CurrentPage = page,
            TotalPages = Math.Max((int)Math.Ceiling(totalCount / (double)PageSize), 1),
            TotalCount = totalCount
        };

        return View(viewModel);
    }

    [HttpGet("proizvodi/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(item => item.ProductImages)
            .FirstOrDefaultAsync(item => item.Slug == slug && item.IsPublished && !item.Disabled);

        if (product == null)
        {
            return NotFound();
        }

        var relatedProducts = await _dbContext.Products
            .AsNoTracking()
            .Include(item => item.ProductImages)
            .Where(item =>
                item.Id != product.Id &&
                item.IsPublished &&
                !item.Disabled &&
                item.Quantity > item.ReservedQuantity &&
                item.Name == product.Name &&
                item.Slug != null)
            .OrderByDescending(item => item.Id)
            .Take(4)
            .ToListAsync();

        var viewModel = new ProductDetailsViewModel
        {
            Id = product.Id,
            Slug = product.Slug ?? product.Id.ToString(),
            Name = product.Name,
            //Name = string.IsNullOrWhiteSpace(product.Model) ? product.Name : product.Model,
            Model = product.Model,
            ProductNumber = product.ProductNumber,
            CollectionName = StorefrontViewModelMapper.BuildCollectionName(product),
            Color = product.Color,
            SizeLabel = StorefrontViewModelMapper.BuildSizeLabel(product.Width, product.Length),
            Description = StorefrontViewModelMapper.BuildDescription(product),
            CurrentPrice = product.EffectivePrice,
            CompareAtPrice = product.OnlinePrice.HasValue && product.OnlinePrice.Value < product.Price ? product.Price : null,
            AvailableQuantity = product.AvailableQuantity,
            PerM2 = product.PerM2,
            SeoTitle = string.IsNullOrWhiteSpace(product.SeoTitle) ? $"{product.Name} | Kašmir Home" : product.SeoTitle,
            SeoDescription = string.IsNullOrWhiteSpace(product.SeoDescription) ? StorefrontViewModelMapper.BuildShortDescription(product) : product.SeoDescription,
            GalleryUrls = product.ProductImages
                .Where(image => !image.Disabled)
                .OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder)
                .Select(image => image.Url)
                .ToList(),
            RelatedProducts = relatedProducts.Select(StorefrontViewModelMapper.ToProductCard).ToList()
        };

        return View(viewModel);
    }
}
