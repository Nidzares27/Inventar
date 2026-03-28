namespace Inventar.Storefront.ViewModels.Catalog;

public class CatalogIndexViewModel
{
    public IReadOnlyList<ProductCardViewModel> Products { get; set; } = Array.Empty<ProductCardViewModel>();
    public IReadOnlyList<string> Collections { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Colors { get; set; } = Array.Empty<string>();
    public string? Query { get; set; }
    public string? Collection { get; set; }
    public string? Color { get; set; }
    public string Sort { get; set; } = "featured";
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}
