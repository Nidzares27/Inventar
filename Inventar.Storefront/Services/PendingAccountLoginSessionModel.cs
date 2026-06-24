namespace Inventar.Storefront.Services;

public class PendingAccountLoginSessionModel
{
    public string Email { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
    public DateTime ExpiresUtc { get; set; }
}
