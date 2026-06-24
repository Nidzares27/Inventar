namespace Inventar.ViewModels.StorefrontAdmin;

public class WebCustomerAdminEmailGroupViewModel
{
    public string CustomerEmail { get; set; } = string.Empty;
    public IReadOnlyList<WebCustomerAdminListItemViewModel> Customers { get; set; } = Array.Empty<WebCustomerAdminListItemViewModel>();
}
