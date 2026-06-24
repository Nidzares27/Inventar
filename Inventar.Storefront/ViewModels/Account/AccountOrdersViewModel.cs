namespace Inventar.Storefront.ViewModels.Account;

public class AccountOrdersViewModel
{
    public IReadOnlyList<AccountOrderListItemViewModel> Orders { get; set; } = Array.Empty<AccountOrderListItemViewModel>();
}
