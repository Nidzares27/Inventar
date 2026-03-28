namespace Inventar.Storefront.ViewModels.Checkout;

public class OrderConfirmationViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerFirstName { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public int TotalItems { get; set; }
}
