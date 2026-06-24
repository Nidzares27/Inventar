using Inventar.Storefront.Data;
using Inventar.Storefront.ViewModels.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Storefront.Services;

public class CategoryNavigationService : ICategoryNavigationService
{
    private readonly StorefrontDbContext _dbContext;
    private readonly StorefrontPoMjeriInventoryService _poMjeriInventoryService;

    public CategoryNavigationService(
        StorefrontDbContext dbContext,
        StorefrontPoMjeriInventoryService poMjeriInventoryService)
    {
        _dbContext = dbContext;
        _poMjeriInventoryService = poMjeriInventoryService;
    }

    public async Task<IReadOnlyList<CategoryGroupViewModel>> GetCategoryGroupsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(product =>
                product.IsPublished &&
                !product.Disabled &&
                product.Slug != null &&
                product.BroaderCategory != null &&
                product.BroaderCategory != string.Empty &&
                product.BroaderCategory != StorefrontCategoryHelper.PlaceholderCategory)
            .ToListAsync(cancellationToken);

        var availabilitySnapshot = await _poMjeriInventoryService.LoadSnapshotAsync(products, cancellationToken: cancellationToken);

        var categories = products
            .Where(product => availabilitySnapshot.GetEffectiveAvailability(product) > 0)
            .Select(product => new
            {
                product.BroaderCategory,
                product.NarrowerCategory
            })
            .Distinct()
            .ToList();

        return categories
            .GroupBy(
                item => StorefrontCategoryHelper.Normalize(item.BroaderCategory),
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => new CategoryGroupViewModel
            {
                BroaderCategory = group.Key,
                NarrowerCategories = group
                    .Select(item => item.NarrowerCategory)
                    .Where(StorefrontCategoryHelper.IsMeaningful)
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList()
            })
            .ToList();
    }
}
