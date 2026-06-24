using Inventar.Storefront.ViewModels.Catalog;

namespace Inventar.Storefront.Services;

public interface ICategoryNavigationService
{
    Task<IReadOnlyList<CategoryGroupViewModel>> GetCategoryGroupsAsync(CancellationToken cancellationToken = default);
}
