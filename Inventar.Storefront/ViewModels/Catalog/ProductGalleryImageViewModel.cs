namespace Inventar.Storefront.ViewModels.Catalog;

public class ProductGalleryImageViewModel
{
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string AltText { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image";
    public bool IsVideo => string.Equals(MediaType, "video", StringComparison.OrdinalIgnoreCase);
}
