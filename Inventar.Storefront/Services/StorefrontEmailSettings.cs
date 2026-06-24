namespace Inventar.Storefront.Services;

public class StorefrontEmailSettings
{
    public const string SectionName = "StorefrontEmail";

    public string SenderEmail { get; set; } = "npetricevic05@gmail.com";
    public string SenderDisplayName { get; set; } = "Kašmir Home";
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = "npetricevic05@gmail.com";
    public string SmtpPassword { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public int VerificationCodeLifetimeMinutes { get; set; } = 15;

    public static bool UsesPlaceholder(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Contains("example.com", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("<required", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("<optional", StringComparison.OrdinalIgnoreCase));
    }
}
