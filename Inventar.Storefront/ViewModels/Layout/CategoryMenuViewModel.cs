using Inventar.Storefront.ViewModels.Catalog;

namespace Inventar.Storefront.ViewModels.Layout;

public class CategoryMenuViewModel
{
    public bool IsMobile { get; set; }
    public IReadOnlyList<CategoryGroupViewModel> CategoryGroups { get; set; } = Array.Empty<CategoryGroupViewModel>();
}
