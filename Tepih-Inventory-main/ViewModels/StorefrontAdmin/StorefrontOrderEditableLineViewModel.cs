namespace Inventar.ViewModels.StorefrontAdmin;

public class StorefrontOrderEditableLineViewModel
{
    public int? ExistingItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Color { get; set; }
    public int? Width { get; set; }
    public int? Length { get; set; }
    public bool PerM2 { get; set; }
    public bool PoMjeri { get; set; }
    public int Quantity { get; set; }
    public int MaxQuantity { get; set; }
    public string MaxQuantityMessage { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? PrimaryImageUrl { get; set; }
}
