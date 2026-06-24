namespace Inventar.Storefront.ViewModels.Catalog;

public class ProductVariantOptionViewModel
{
    public int ProductId { get; set; }
    public string Color { get; set; } = string.Empty;
    public string SizeLabel { get; set; } = string.Empty;
    public bool PoMjeri { get; set; }
    public int? OriginalWidth { get; set; }
    public int? OriginalLength { get; set; }
    public int? RemainingLength { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? PricePerSquareMeter { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsSoldOut { get; set; }
    public string AvailabilityStatusMessage { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
}
