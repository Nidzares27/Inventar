namespace Inventar.Storefront.ViewModels.Checkout;

public class CheckoutVerificationEmailModel
{
    public string CustomerFirstName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
}
