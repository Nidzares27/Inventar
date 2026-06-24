namespace Inventar.ViewModels.StorefrontAdmin;

public class WebCustomerAdminIndexViewModel
{
    public IReadOnlyList<WebCustomerAdminEmailGroupViewModel> EmailGroups { get; set; } = Array.Empty<WebCustomerAdminEmailGroupViewModel>();
}
