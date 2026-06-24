namespace Inventar.Storefront.ViewModels.Catalog;

public class CategoryGroupViewModel
{
    public string BroaderCategory { get; set; } = string.Empty;
    public IReadOnlyList<string> NarrowerCategories { get; set; } = Array.Empty<string>();
}
