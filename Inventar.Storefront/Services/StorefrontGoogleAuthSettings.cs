namespace Inventar.Storefront.Services;

public class StorefrontGoogleAuthSettings
{
    public const string SectionName = "StorefrontGoogleAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-google-storefront";

    public static bool UsesPlaceholder(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Contains("<optional", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("<required", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("your_", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("example", StringComparison.OrdinalIgnoreCase));
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !UsesPlaceholder(ClientId) &&
        !UsesPlaceholder(ClientSecret);
}
