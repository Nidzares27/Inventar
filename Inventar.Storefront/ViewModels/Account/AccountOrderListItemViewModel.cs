namespace Inventar.Storefront.ViewModels.Account;

public class AccountOrderListItemViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string FulfillmentStatus { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public int TotalItems { get; set; }
    public IReadOnlyList<string> PreviewImageUrls { get; set; } = Array.Empty<string>();
}
