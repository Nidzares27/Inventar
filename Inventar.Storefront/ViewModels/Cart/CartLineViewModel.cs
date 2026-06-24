namespace Inventar.Storefront.ViewModels.Cart;

public class CartLineViewModel
{
    public string LineId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string SizeLabel { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int MaxOrderQuantity { get; set; }
    public bool PoMjeri { get; set; }
    public bool PerM2 { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal? PricePerSquareMeter { get; set; }

    public bool HasAvailabilityIssue => MaxOrderQuantity < Quantity;
}
