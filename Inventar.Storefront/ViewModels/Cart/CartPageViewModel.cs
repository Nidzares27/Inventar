namespace Inventar.Storefront.ViewModels.Cart;

public class CartPageViewModel
{
    public IReadOnlyList<CartLineViewModel> Lines { get; set; } = Array.Empty<CartLineViewModel>();
    public decimal Subtotal { get; set; }
    public int TotalItems { get; set; }

    public bool HasAvailabilityIssues => Lines.Any(line => line.HasAvailabilityIssue);
}
