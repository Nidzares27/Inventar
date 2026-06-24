namespace Inventar.Storefront.ViewModels.Checkout;

public class OrderConfirmationViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerNote { get; set; }
    public string ShippingAddressLine1 { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingCountry { get; set; } = string.Empty;
    public decimal ItemsTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public int TotalItems { get; set; }
    public bool ConfirmationEmailSent { get; set; } = true;
    public string? ConfirmationEmailStatusMessage { get; set; }
    public IReadOnlyList<OrderConfirmationLineViewModel> Lines { get; set; } = Array.Empty<OrderConfirmationLineViewModel>();
}
