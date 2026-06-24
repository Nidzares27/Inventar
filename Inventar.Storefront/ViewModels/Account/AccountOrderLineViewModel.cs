namespace Inventar.Storefront.ViewModels.Account;

public class AccountOrderLineViewModel
{
    public string ShortDescription { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
