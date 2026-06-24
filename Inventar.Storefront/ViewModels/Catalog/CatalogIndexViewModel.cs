namespace Inventar.Storefront.ViewModels.Catalog;

public class CatalogIndexViewModel
{
    public IReadOnlyList<ProductCardViewModel> Products { get; set; } = Array.Empty<ProductCardViewModel>();
    public IReadOnlyList<CategoryGroupViewModel> CategoryGroups { get; set; } = Array.Empty<CategoryGroupViewModel>();
    public IReadOnlyList<string> Colors { get; set; } = Array.Empty<string>();
    public string? Query { get; set; }
    public string? BroaderCategory { get; set; }
    public string? NarrowerCategory { get; set; }
    public string? Color { get; set; }
    public string Sort { get; set; } = "newest-desc";
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}
