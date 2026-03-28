namespace Inventar.Storefront.ViewModels.Cart;

public class CartAddedNotificationViewModel
{
    public string Name { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string SizeLabel { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int QuantityAdded { get; set; }
    public decimal UnitPrice { get; set; }
}
