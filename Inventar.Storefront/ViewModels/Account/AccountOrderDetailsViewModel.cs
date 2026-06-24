namespace Inventar.Storefront.ViewModels.Account;

public class AccountOrderDetailsViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string FulfillmentStatus { get; set; } = string.Empty;
    public decimal ItemsTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string? CustomerNote { get; set; }
    public IReadOnlyList<AccountOrderLineViewModel> Lines { get; set; } = Array.Empty<AccountOrderLineViewModel>();
}
