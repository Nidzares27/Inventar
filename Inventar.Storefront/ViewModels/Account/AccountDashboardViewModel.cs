namespace Inventar.Storefront.ViewModels.Account;

public class AccountDashboardViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsProfileComplete { get; set; }
    public IReadOnlyList<AccountOrderListItemViewModel> RecentOrders { get; set; } = Array.Empty<AccountOrderListItemViewModel>();
}
