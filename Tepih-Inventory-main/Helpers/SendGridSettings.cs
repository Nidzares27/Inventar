namespace Inventar.Helpers;

public class SendGridSettings
{
    public const string SectionName = "SendGrid";

    public string ApiKey { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderDisplayName { get; set; } = "Inventar";
}
