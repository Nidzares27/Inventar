using Inventar.Storefront.Data;
using Inventar.Storefront.Models;
using Inventar.Storefront.Services;
using Inventar.Storefront.Utils;
using Inventar.Storefront.ViewModels.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Storefront.Controllers;

public class CatalogController : Controller
{
    private const int PageSize = 12;
    private const int PredictiveSearchProductLimit = 6;
    private const int PredictiveSearchSuggestionLimit = 6;

    private readonly StorefrontDbContext _dbContext;
    private readonly ICategoryNavigationService _categoryNavigationService;
    private readonly StorefrontPoMjeriInventoryService _poMjeriInventoryService;
    private readonly ICartService _cartService;

    public CatalogController(
        StorefrontDbContext dbContext,
        ICategoryNavigationService categoryNavigationService,
        StorefrontPoMjeriInventoryService poMjeriInventoryService,
        ICartService cartService)
    {
        _dbContext = dbContext;
        _categoryNavigationService = categoryNavigationService;
        _poMjeriInventoryService = poMjeriInventoryService;
        _cartService = cartService;
    }

    [HttpGet("proizvodi")]
    public async Task<IActionResult> Index(string? q, string? broaderCategory, string? narrowerCategory, string? color, string sort = "newest-desc", int page = 1)
    {
        page = Math.Max(page, 1);

        q = NormalizeSearchQuery(q);
        broaderCategory = string.IsNullOrWhiteSpace(broaderCategory) ? null : broaderCategory.Trim();
        narrowerCategory = string.IsNullOrWhiteSpace(narrowerCategory) ? null : narrowerCategory.Trim();
        color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();

        var visibleProductsQuery = BuildVisibleProductsQuery();

        var filteredVariantsQuery = visibleProductsQuery
            .Include(product => product.ProductImages)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            filteredVariantsQuery = ApplySearchFilter(filteredVariantsQuery, q);
        }

        if (!string.IsNullOrWhiteSpace(broaderCategory))
        {
            filteredVariantsQuery = filteredVariantsQuery.Where(product => product.BroaderCategory == broaderCategory);
        }

        if (!string.IsNullOrWhiteSpace(narrowerCategory))
        {
            filteredVariantsQuery = filteredVariantsQuery.Where(product => product.NarrowerCategory == narrowerCategory);
        }

        if (!string.IsNullOrWhiteSpace(color))
        {
            filteredVariantsQuery = filteredVariantsQuery.Where(product => product.Color == color);
        }

        var filteredVariants = await filteredVariantsQuery.ToListAsync();
        var availabilitySnapshot = await _poMjeriInventoryService.LoadSnapshotAsync(
            filteredVariants,
            _cartService.GetCart(),
            cancellationToken: HttpContext.RequestAborted);
        var availabilityLookup = filteredVariants.ToDictionary(product => product.Id, availabilitySnapshot.GetEffectiveAvailability);

        var groupedProducts = StorefrontProductGrouping.SortGroups(
            StorefrontProductGrouping.GroupVariants(filteredVariants, availabilityLookup),
            sort);

        var totalCount = groupedProducts.Count;
        var products = groupedProducts
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(StorefrontViewModelMapper.ToProductCard)
            .ToList();

        var colorsQuery = visibleProductsQuery;
        if (!string.IsNullOrWhiteSpace(broaderCategory))
        {
            colorsQuery = colorsQuery.Where(product => product.BroaderCategory == broaderCategory);
        }

        if (!string.IsNullOrWhiteSpace(narrowerCategory))
        {
            colorsQuery = colorsQuery.Where(product => product.NarrowerCategory == narrowerCategory);
        }

        var colors = await colorsQuery
            .Select(product => product.Color)
            .Where(colorValue => colorValue != null && colorValue != string.Empty)
            .Distinct()
            .OrderBy(colorValue => colorValue)
            .ToListAsync();

        var viewModel = new CatalogIndexViewModel
        {
            Products = products,
            CategoryGroups = await _categoryNavigationService.GetCategoryGroupsAsync(),
            Colors = colors,
            Query = q,
            BroaderCategory = broaderCategory,
            NarrowerCategory = narrowerCategory,
            Color = color,
            Sort = sort,
            CurrentPage = page,
            TotalPages = Math.Max((int)Math.Ceiling(totalCount / (double)PageSize), 1),
            TotalCount = totalCount
        };

        return View(viewModel);
    }

    [HttpGet("proizvodi/pretraga/predlozi")]
    public async Task<IActionResult> PredictiveSearch(string? q, CancellationToken cancellationToken)
    {
        q = NormalizeSearchQuery(q);

        var suggestionItems = await BuildCategorySuggestionsAsync(q, cancellationToken);

        var predictiveProductsQuery = BuildVisibleProductsQuery()
            .Include(product => product.ProductImages)
            .OrderByDescending(product => product.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            predictiveProductsQuery = ApplySearchFilter(predictiveProductsQuery, q);
        }
        else
        {
            predictiveProductsQuery = predictiveProductsQuery.Take(60);
        }

        var predictiveVariants = await predictiveProductsQuery.ToListAsync(cancellationToken);
        var predictiveAvailability = await _poMjeriInventoryService.LoadSnapshotAsync(
            predictiveVariants,
            _cartService.GetCart(),
            cancellationToken: cancellationToken);
        var predictiveAvailabilityLookup = predictiveVariants.ToDictionary(
            product => product.Id,
            predictiveAvailability.GetEffectiveAvailability);

        var predictiveProducts = StorefrontProductGrouping.SortGroups(
                StorefrontProductGrouping.GroupVariants(predictiveVariants, predictiveAvailabilityLookup),
                "newest-desc")
            .Take(PredictiveSearchProductLimit)
            .Select(group =>
            {
                var productCard = StorefrontViewModelMapper.ToProductCard(group);

                return new PredictiveSearchProductViewModel
                {
                    Url = Url.Action("Details", "Catalog", new { slug = productCard.Slug }) ?? "/proizvodi",
                    ImageUrl = productCard.ImageUrl,
                    ShortDescription = productCard.Description,
                    Price = productCard.CurrentPrice.ToString("0.00") + " €"
                };
            })
            .ToList();

        var response = new PredictiveSearchResponseViewModel
        {
            Query = q ?? string.Empty,
            Suggestions = suggestionItems,
            Products = predictiveProducts,
            ResultsUrl = string.IsNullOrWhiteSpace(q)
                ? Url.Action("Index", "Catalog") ?? "/proizvodi"
                : Url.Action("Index", "Catalog", new { q }) ?? "/proizvodi"
        };

        return Json(response);
    }

    [HttpGet("proizvodi/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var currentProduct = await _dbContext.Products
            .AsNoTracking()
            .Include(product => product.ProductImages)
            .FirstOrDefaultAsync(product => product.Slug == slug && product.IsPublished && !product.Disabled);

        if (currentProduct == null)
        {
            return NotFound();
        }

        var variants = await LoadGroupVariantsAsync(currentProduct);
        if (variants.Count == 0)
        {
            return NotFound();
        }

        var cartItems = _cartService.GetCart();
        var availabilitySnapshot = await _poMjeriInventoryService.LoadSnapshotAsync(
            variants,
            cartItems,
            cancellationToken: HttpContext.RequestAborted);
        var availabilityLookup = variants.ToDictionary(product => product.Id, availabilitySnapshot.GetEffectiveAvailability);

        var selectedVariant = variants.FirstOrDefault(product => product.Id == currentProduct.Id) ?? variants[0];
        var broaderCategoryMatch = StorefrontCategoryHelper.IsMeaningful(selectedVariant.BroaderCategory)
            ? selectedVariant.BroaderCategory.Trim()
            : null;
        var narrowerCategoryMatch = StorefrontCategoryHelper.IsMeaningful(selectedVariant.NarrowerCategory)
            ? selectedVariant.NarrowerCategory.Trim()
            : null;
        var selectedName = selectedVariant.Name;
        var selectedModel = selectedVariant.Model;
        var selectedProductNumber = selectedVariant.ProductNumber;

        var relatedCandidatesQuery = _dbContext.Products
            .AsNoTracking()
            .Include(product => product.ProductImages)
            .Where(product =>
                product.IsPublished &&
                !product.Disabled &&
                product.Slug != null);

        if (selectedVariant.PoMjeri)
        {
            relatedCandidatesQuery = relatedCandidatesQuery.Where(product =>
                !(product.PoMjeri &&
                  product.Name == selectedName &&
                  product.ProductNumber == selectedProductNumber &&
                  product.Model == selectedModel));
        }
        else
        {
            relatedCandidatesQuery = relatedCandidatesQuery.Where(product =>
                !(product.Name == selectedName &&
                  product.Model == selectedModel));
        }

        var relatedCandidateVariants = await relatedCandidatesQuery.ToListAsync();

        if (narrowerCategoryMatch != null || broaderCategoryMatch != null)
        {
            relatedCandidateVariants = relatedCandidateVariants
                .Where(product =>
                    (narrowerCategoryMatch != null && product.NarrowerCategory == narrowerCategoryMatch) ||
                    (broaderCategoryMatch != null && product.BroaderCategory == broaderCategoryMatch) ||
                    product.Name == selectedVariant.Name)
                .ToList();
        }
        else
        {
            relatedCandidateVariants = relatedCandidateVariants
                .Where(product => product.Name == selectedVariant.Name)
                .ToList();
        }

        var relatedAvailability = await _poMjeriInventoryService.LoadSnapshotAsync(
            relatedCandidateVariants,
            cancellationToken: HttpContext.RequestAborted);
        var relatedLookup = relatedCandidateVariants.ToDictionary(product => product.Id, relatedAvailability.GetEffectiveAvailability);

        var relatedProducts = StorefrontProductGrouping.GroupVariants(relatedCandidateVariants, relatedLookup)
            .OrderByDescending(group => narrowerCategoryMatch != null && group.Variants.Any(product => product.NarrowerCategory == narrowerCategoryMatch))
            .ThenByDescending(group => broaderCategoryMatch != null && group.Variants.Any(product => product.BroaderCategory == broaderCategoryMatch))
            .ThenByDescending(group => group.Variants.Any(product => product.Name == selectedVariant.Name))
            .ThenBy(group => group.TotalAvailableQuantity <= 0)
            .ThenByDescending(group => group.LatestProductId)
            .Take(4)
            .Select(StorefrontViewModelMapper.ToProductCard)
            .ToList();

        var orderedVariants = variants
            .OrderByDescending(product => product.Id == selectedVariant.Id)
            .ThenBy(product => product.Id)
            .ToList();

        var colorOptions = variants
            .Select(product => product.Color)
            .Where(colorValue => !string.IsNullOrWhiteSpace(colorValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(colorValue => colorValue)
            .ToList();

        var variantOptions = BuildVariantOptions(variants, availabilitySnapshot);
        var selectedSizeLabel = selectedVariant.PoMjeri
            ? BuildPoMjeriWidthLabel(selectedVariant.Width)
            : StorefrontViewModelMapper.BuildSizeLabel(selectedVariant.Width, selectedVariant.Length);

        var selectedVariantOption = variantOptions.FirstOrDefault(option =>
                string.Equals(option.Color, selectedVariant.Color, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(option.SizeLabel, selectedSizeLabel, StringComparison.OrdinalIgnoreCase))
            ?? variantOptions.FirstOrDefault(option =>
                string.Equals(option.Color, selectedVariant.Color, StringComparison.OrdinalIgnoreCase))
            ?? variantOptions[0];

        var sizeOptions = selectedVariant.PoMjeri
            ? variantOptions
                .Where(option => option.PoMjeri)
                .OrderBy(option => option.OriginalWidth ?? int.MaxValue)
                .Select(option => option.SizeLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : variants
                .Select(product => StorefrontViewModelMapper.BuildSizeLabel(product.Width, product.Length))
                .Where(sizeLabel => !string.IsNullOrWhiteSpace(sizeLabel))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(sizeLabel => sizeLabel)
                .ToList();

        var selectedAvailability = selectedVariant.PoMjeri
            ? selectedVariantOption.AvailableQuantity
            : availabilityLookup.GetValueOrDefault(selectedVariant.Id, selectedVariant.AvailableQuantity);
        var selectedPricing = selectedVariant.PoMjeri
            ? StorefrontPricingHelper.BuildPoMjeriPricing(selectedVariant.EffectivePrice, selectedVariant.Price, null)
            : StorefrontPricingHelper.BuildPricing(selectedVariant, selectedVariant.Width, selectedVariant.Length);

        var viewModel = new ProductDetailsViewModel
        {
            Id = selectedVariantOption.ProductId,
            Slug = selectedVariant.Slug ?? selectedVariant.Id.ToString(),
            Name = TextEncodingHelper.Decode(selectedVariant.Name) ?? selectedVariant.Name,
            Model = TextEncodingHelper.Decode(selectedVariant.Model) ?? selectedVariant.Model,
            ProductNumber = TextEncodingHelper.Decode(selectedVariant.ProductNumber) ?? selectedVariant.ProductNumber,
            CollectionName = StorefrontViewModelMapper.BuildCollectionName(selectedVariant),
            ShortDescription = StorefrontViewModelMapper.BuildShortDescriptionText(selectedVariant),
            Color = TextEncodingHelper.Decode(selectedVariant.Color) ?? selectedVariant.Color,
            SizeLabel = StorefrontViewModelMapper.BuildSizeLabel(selectedVariant.Width, selectedVariant.Length),
            Description = StorefrontViewModelMapper.BuildDescription(selectedVariant),
            CurrentPrice = selectedPricing.UnitPrice,
            CompareAtPrice = selectedPricing.CompareAtUnitPrice,
            PricePerSquareMeter = selectedPricing.PricePerSquareMeter,
            AvailableQuantity = selectedAvailability,
            MaxOrderQuantity = StorefrontStockRules.GetMaxOrderQuantity(selectedAvailability),
            IsSoldOut = selectedAvailability <= 0,
            CanAddToCart = selectedAvailability > 0,
            AvailabilityStatusMessage = selectedVariant.PoMjeri
                ? (selectedAvailability > 0 ? string.Empty : StorefrontStockRules.SoldOutStatusText)
                : StorefrontStockRules.BuildAvailabilityStatusMessage(selectedAvailability),
            PerM2 = selectedVariant.PerM2,
            PoMjeri = selectedVariant.PoMjeri,
            SeoTitle = string.IsNullOrWhiteSpace(selectedVariant.SeoTitle)
                ? $"{StorefrontViewModelMapper.BuildProductTitle(selectedVariant)} | Kašmir Home"
                : TextEncodingHelper.Decode(selectedVariant.SeoTitle) ?? selectedVariant.SeoTitle,
            SeoDescription = string.IsNullOrWhiteSpace(selectedVariant.SeoDescription)
                ? StorefrontViewModelMapper.BuildShortDescription(selectedVariant)
                : TextEncodingHelper.Decode(selectedVariant.SeoDescription) ?? selectedVariant.SeoDescription,
            SelectedColor = TextEncodingHelper.Decode(selectedVariant.Color) ?? selectedVariant.Color,
            SelectedSizeLabel = selectedVariantOption.SizeLabel,
            Quantity = 1,
            CustomWidth = selectedVariant.PoMjeri ? selectedVariant.Width : null,
            HasColorOptions = colorOptions.Count > 1,
            HasSizeOptions = selectedVariant.PoMjeri ? sizeOptions.Count > 0 : sizeOptions.Count > 1,
            AvailableColors = colorOptions,
            AvailableSizes = sizeOptions,
            Variants = variantOptions,
            GalleryImages = StorefrontProductGrouping.BuildDistinctGalleryImages(orderedVariants),
            RelatedProducts = relatedProducts
        };

        return View(viewModel);
    }

    [HttpGet("proizvodi/po-mjeri-preview")]
    public async Task<IActionResult> PreviewPoMjeriSelection(
        int productId,
        string? color,
        int? customWidth,
        int? customLength,
        int quantity = 1,
        string? lineId = null)
    {
        var currentProduct = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.Id == productId && product.IsPublished && !product.Disabled);

        if (currentProduct == null || !currentProduct.PoMjeri)
        {
            return Json(new
            {
                success = false,
                message = "Po mjeri proizvod nije pronađen."
            });
        }

        var variants = await LoadGroupVariantsAsync(currentProduct);
        var snapshot = await _poMjeriInventoryService.LoadSnapshotAsync(
            variants,
            _cartService.GetCart(),
            lineId,
            HttpContext.RequestAborted);

        var evaluation = StorefrontPoMjeriPlanner.Evaluate(
            variants,
            snapshot,
            color,
            customWidth ?? 0,
            customLength ?? 0,
            quantity);

        var selectedColor = string.IsNullOrWhiteSpace(color)
            ? currentProduct.Color
            : color.Trim();

        var sourceLookup = variants.ToDictionary(product => product.Id);
        var selectedColorProduct = variants
            .Where(product => string.Equals(product.Color, selectedColor, StringComparison.OrdinalIgnoreCase))
            .OrderBy(product => snapshot.GetAvailableRemainingLength(product.Id) > 0 ? 0 : 1)
            .ThenBy(product => product.Id)
            .FirstOrDefault() ?? currentProduct;
        var pricingProduct = evaluation.BestPlan != null
            ? StorefrontPricingHelper.SelectPoMjeriPricingProduct(sourceLookup, evaluation.BestPlan.Slices)
            : StorefrontPricingHelper.SelectPoMjeriPricingProduct(
                variants,
                snapshot,
                selectedColor,
                customWidth,
                customLength);
        pricingProduct ??= selectedColorProduct;
        var pricing = evaluation.BestPlan != null
            ? StorefrontPricingHelper.BuildPoMjeriPricing(sourceLookup, evaluation.BestPlan.Slices, customLength)
            : StorefrontPricingHelper.BuildPoMjeriPricing(pricingProduct.EffectivePrice, pricingProduct.Price, customLength);

        return Json(new
        {
            success = evaluation.IsValid,
            message = evaluation.Message,
            selectedProductId = pricingProduct.Id,
            maxAvailableQuantity = evaluation.MaxAvailableQuantity,
            canAddToCart = evaluation.IsValid,
            planSourceCount = evaluation.BestPlan?.UsedCandidateCount ?? 0,
            currentPrice = pricing.UnitPrice,
            compareAtPrice = pricing.CompareAtUnitPrice,
            pricePerSquareMeter = pricing.PricePerSquareMeter
        });
    }

    private async Task<List<StorefrontProduct>> LoadGroupVariantsAsync(StorefrontProduct currentProduct)
    {
        IQueryable<StorefrontProduct> query = BuildVisibleProductsQuery()
            .Include(product => product.ProductImages);

        if (currentProduct.PoMjeri)
        {
            query = query.Where(product =>
                product.Name == currentProduct.Name &&
                product.ProductNumber == currentProduct.ProductNumber &&
                product.Model == currentProduct.Model &&
                product.PoMjeri);
        }
        else
        {
            query = query.Where(product =>
                product.Name == currentProduct.Name &&
                product.Model == currentProduct.Model);
        }

        return await query
            .OrderBy(product => product.Id)
            .ToListAsync();
    }

    private IQueryable<StorefrontProduct> BuildVisibleProductsQuery()
    {
        return _dbContext.Products
            .AsNoTracking()
            .Where(product =>
                product.IsPublished &&
                !product.Disabled &&
                product.Slug != null);
    }

    private static string? NormalizeSearchQuery(string? query)
    {
        return string.IsNullOrWhiteSpace(query) ? null : query.Trim();
    }

    private static IQueryable<StorefrontProduct> ApplySearchFilter(IQueryable<StorefrontProduct> query, string search)
    {
        return query.Where(product =>
            product.Name.Contains(search) ||
            product.Model.Contains(search) ||
            product.ProductNumber.Contains(search) ||
            product.BroaderCategory.Contains(search) ||
            product.NarrowerCategory.Contains(search) ||
            (product.ShortDescription != null && product.ShortDescription.Contains(search)) ||
            (product.Description != null && product.Description.Contains(search)) ||
            product.Color.Contains(search));
    }

    private async Task<List<PredictiveSearchSuggestionViewModel>> BuildCategorySuggestionsAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        var categoryGroups = await _categoryNavigationService.GetCategoryGroupsAsync(cancellationToken);
        var normalizedQuery = query?.Trim();

        var suggestions = new List<PredictiveSearchSuggestionViewModel>();

        foreach (var group in categoryGroups)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuery) || group.BroaderCategory.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new PredictiveSearchSuggestionViewModel
                {
                    Label = group.BroaderCategory,
                    Url = Url.Action("Index", "Catalog", new { broaderCategory = group.BroaderCategory }) ?? "/proizvodi"
                });
            }

            foreach (var narrowerCategory in group.NarrowerCategories)
            {
                var label = $"{group.BroaderCategory} / {narrowerCategory}";
                if (!string.IsNullOrWhiteSpace(normalizedQuery) &&
                    !label.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                suggestions.Add(new PredictiveSearchSuggestionViewModel
                {
                    Label = label,
                    Url = Url.Action(
                        "Index",
                        "Catalog",
                        new
                        {
                            broaderCategory = group.BroaderCategory,
                            narrowerCategory
                        }) ?? "/proizvodi"
                });
            }
        }

        return suggestions
            .DistinctBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .Take(PredictiveSearchSuggestionLimit)
            .ToList();
    }

    private static List<ProductVariantOptionViewModel> BuildVariantOptions(
        IReadOnlyCollection<StorefrontProduct> variants,
        PoMjeriInventorySnapshot availabilitySnapshot)
    {
        if (variants.Any(product => product.PoMjeri))
        {
            return variants
                .Where(product => product.Width.HasValue)
                .GroupBy(
                    product => new
                    {
                        Color = TextEncodingHelper.Decode(product.Color) ?? product.Color,
                        Width = product.Width ?? 0
                    })
                .Select(group =>
                {
                    var representative = group
                        .OrderBy(product => availabilitySnapshot.GetAvailableRemainingLength(product.Id) > 0 ? 0 : 1)
                        .ThenBy(product => product.Id)
                        .First();
                    var pricing = StorefrontPricingHelper.BuildPoMjeriPricing(
                        representative.EffectivePrice,
                        representative.Price,
                        null);

                    return new ProductVariantOptionViewModel
                    {
                        ProductId = representative.Id,
                        Color = group.Key.Color,
                        SizeLabel = BuildPoMjeriWidthLabel(group.Key.Width),
                        PoMjeri = true,
                        OriginalWidth = group.Key.Width,
                        OriginalLength = representative.Length,
                        RemainingLength = group.Max(product => availabilitySnapshot.GetAvailableRemainingLength(product.Id)),
                        CurrentPrice = pricing.UnitPrice,
                        CompareAtPrice = pricing.CompareAtUnitPrice,
                        PricePerSquareMeter = pricing.PricePerSquareMeter,
                        AvailableQuantity = group.Sum(product => availabilitySnapshot.GetEffectiveAvailability(product)),
                        IsSoldOut = group.All(product => availabilitySnapshot.GetAvailableRemainingLength(product.Id) <= 0),
                        AvailabilityStatusMessage = group.All(product => availabilitySnapshot.GetAvailableRemainingLength(product.Id) <= 0)
                            ? StorefrontStockRules.SoldOutStatusText
                            : string.Empty,
                        PrimaryImageUrl = StorefrontViewModelMapper.BuildPrimaryGalleryImageUrl(representative)
                    };
                })
                .OrderBy(option => option.Color)
                .ThenBy(option => option.OriginalWidth ?? int.MaxValue)
                .ToList();
        }

        return variants
            .Select(product =>
            {
                var pricing = StorefrontPricingHelper.BuildPricing(product, product.Width, product.Length);

                return new ProductVariantOptionViewModel
                {
                    ProductId = product.Id,
                    Color = TextEncodingHelper.Decode(product.Color) ?? product.Color,
                    SizeLabel = StorefrontViewModelMapper.BuildSizeLabel(product.Width, product.Length),
                    CurrentPrice = pricing.UnitPrice,
                    CompareAtPrice = pricing.CompareAtUnitPrice,
                    PricePerSquareMeter = pricing.PricePerSquareMeter,
                    AvailableQuantity = availabilitySnapshot.GetEffectiveAvailability(product),
                    IsSoldOut = availabilitySnapshot.GetEffectiveAvailability(product) <= 0,
                    AvailabilityStatusMessage = StorefrontStockRules.BuildAvailabilityStatusMessage(availabilitySnapshot.GetEffectiveAvailability(product)),
                    PrimaryImageUrl = StorefrontViewModelMapper.BuildPrimaryGalleryImageUrl(product)
                };
            })
            .ToList();
    }

    private static string BuildPoMjeriWidthLabel(int? width)
    {
        return width.HasValue && width.Value > 0
            ? $"{width.Value} cm"
            : string.Empty;
    }
}
