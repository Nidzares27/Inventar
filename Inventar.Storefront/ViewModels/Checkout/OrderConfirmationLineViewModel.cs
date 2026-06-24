namespace Inventar.Storefront.ViewModels.Checkout;

public class OrderConfirmationLineViewModel
{
    public string? ImageUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
