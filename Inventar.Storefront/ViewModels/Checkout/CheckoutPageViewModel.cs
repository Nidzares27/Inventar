using Inventar.Storefront.ViewModels.Cart;

namespace Inventar.Storefront.ViewModels.Checkout;

public class CheckoutPageViewModel
{
    public CheckoutFormViewModel Form { get; set; } = new();
    public IReadOnlyList<CartLineViewModel> Lines { get; set; } = Array.Empty<CartLineViewModel>();
    public bool IsAuthenticatedCustomer { get; set; }
    public string? AuthenticatedCustomerEmail { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public int TotalItems { get; set; }
}
