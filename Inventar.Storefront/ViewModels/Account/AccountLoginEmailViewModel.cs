namespace Inventar.Storefront.ViewModels.Account;

public class AccountLoginEmailViewModel
{
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
}
