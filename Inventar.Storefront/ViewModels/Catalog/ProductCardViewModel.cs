namespace Inventar.Storefront.ViewModels.Catalog;

public class ProductCardViewModel
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string SizeLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int AvailableQuantity { get; set; }
}
