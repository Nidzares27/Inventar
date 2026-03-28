using Inventar.Storefront.ViewModels.Catalog;

namespace Inventar.Storefront.ViewModels.Home;

public class HomeIndexViewModel
{
    public string BrandName { get; set; } = string.Empty;
    public IReadOnlyList<string> Collections { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ProductCardViewModel> FeaturedProducts { get; set; } = Array.Empty<ProductCardViewModel>();
    public int TotalPublishedProducts { get; set; }
}
