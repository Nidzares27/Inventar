using Inventar.Storefront.Models;
using Inventar.Storefront.ViewModels.Checkout;

namespace Inventar.Storefront.Services;

public class PendingCheckoutSessionModel
{
    public CheckoutFormViewModel Form { get; set; } = new();
    public List<CartItem> CartItems { get; set; } = new();
    public string VerificationCodeHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public DateTime LastSentUtc { get; set; }
}
