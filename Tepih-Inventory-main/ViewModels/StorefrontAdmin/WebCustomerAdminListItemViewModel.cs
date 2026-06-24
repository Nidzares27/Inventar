namespace Inventar.ViewModels.StorefrontAdmin;

public class WebCustomerAdminListItemViewModel
{
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public int OrderCount { get; set; }
    public int TotalItemsOrdered { get; set; }
    public decimal TotalMoneySpent { get; set; }
}
