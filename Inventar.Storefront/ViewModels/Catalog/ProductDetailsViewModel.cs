namespace Inventar.Storefront.ViewModels.Catalog;

public class ProductDetailsViewModel
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string SizeLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? PricePerSquareMeter { get; set; }
    public int AvailableQuantity { get; set; }
    public int MaxOrderQuantity { get; set; }
    public bool IsSoldOut { get; set; }
    public bool CanAddToCart { get; set; }
    public string AvailabilityStatusMessage { get; set; } = string.Empty;
    public bool PerM2 { get; set; }
    public bool PoMjeri { get; set; }
    public string SeoTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public string SelectedColor { get; set; } = string.Empty;
    public string SelectedSizeLabel { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int? CustomWidth { get; set; }
    public int? CustomLength { get; set; }
    public bool HasColorOptions { get; set; }
    public bool HasSizeOptions { get; set; }
    public IReadOnlyList<string> AvailableColors { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableSizes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ProductVariantOptionViewModel> Variants { get; set; } = Array.Empty<ProductVariantOptionViewModel>();
    public IReadOnlyList<ProductGalleryImageViewModel> GalleryImages { get; set; } = Array.Empty<ProductGalleryImageViewModel>();
    public IReadOnlyList<ProductCardViewModel> RelatedProducts { get; set; } = Array.Empty<ProductCardViewModel>();
}
