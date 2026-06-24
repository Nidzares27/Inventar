using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Inventar.Storefront.ViewModels.Account;
using Inventar.Storefront.ViewModels.Checkout;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Inventar.Storefront.Services;

public class SmtpStorefrontEmailService : IStorefrontEmailService
{
    private readonly StorefrontEmailSettings _settings;
    private readonly StorefrontSettings _storefrontSettings;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<SmtpStorefrontEmailService> _logger;

    public SmtpStorefrontEmailService(
        IOptions<StorefrontEmailSettings> settings,
        IOptions<StorefrontSettings> storefrontSettings,
        IWebHostEnvironment webHostEnvironment,
        ILogger<SmtpStorefrontEmailService> logger)
    {
        _settings = settings.Value;
        _storefrontSettings = storefrontSettings.Value;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
    }

    public Task SendAccountLoginCodeAsync(
        AccountLoginEmailViewModel model,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Kod za prijavu - {_storefrontSettings.BrandName}";
        var plainBody = string.Join(Environment.NewLine, new[]
        {
            "Zdravo,",
            string.Empty,
            "Za prijavu na svoj nalog unesite sljedeći verifikacioni kod:",
            model.VerificationCode,
            string.Empty,
            $"Kod važi narednih {model.ExpiresInMinutes} minuta.",
            "Ako niste pokušali prijavu, slobodno zanemarite ovu poruku."
        });

        var htmlBody = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;line-height:1.6;color:#1f2933;">
                <p>Zdravo,</p>
                <p>Za prijavu na svoj nalog unesite sljedeći verifikacioni kod:</p>
                <div style="display:inline-block;padding:14px 18px;border-radius:12px;background:#f4ede4;border:1px solid #d6c0a8;font-size:28px;font-weight:700;letter-spacing:6px;">
                    {Html(model.VerificationCode)}
                </div>
                <p style="margin-top:20px;">Kod važi narednih <strong>{model.ExpiresInMinutes}</strong> minuta.</p>
                <p>Ako niste pokušali prijavu, slobodno zanemarite ovu poruku.</p>
            </div>
            """;

        return SendEmailAsync(model.Email, subject, plainBody, htmlBody, cancellationToken);
    }

    public Task SendCheckoutVerificationCodeAsync(
        CheckoutVerificationEmailModel model,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Verifikacioni kod za narudžbinu - {_storefrontSettings.BrandName}";
        var plainBody = string.Join(Environment.NewLine, new[]
        {
            $"Zdravo {model.CustomerFirstName},",
            string.Empty,
            "Za nastavak narudžbine unesite sljedeći verifikacioni kod:",
            model.VerificationCode,
            string.Empty,
            $"Kod važi narednih {model.ExpiresInMinutes} minuta.",
            "Ako niste pokrenuli narudžbinu, zanemarite ovu poruku."
        });

        var htmlBody = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;line-height:1.6;color:#1f2933;">
                <p>Zdravo {Html(model.CustomerFirstName)},</p>
                <p>Za nastavak narudžbine unesite sljedeći verifikacioni kod:</p>
                <div style="display:inline-block;padding:14px 18px;border-radius:12px;background:#f4ede4;border:1px solid #d6c0a8;font-size:28px;font-weight:700;letter-spacing:6px;">
                    {Html(model.VerificationCode)}
                </div>
                <p style="margin-top:20px;">Kod važi narednih <strong>{model.ExpiresInMinutes}</strong> minuta.</p>
                <p>Ako niste pokrenuli narudžbinu, slobodno zanemarite ovu poruku.</p>
            </div>
            """;

        return SendEmailAsync(model.Email, subject, plainBody, htmlBody, cancellationToken);
    }

    public Task SendOrderConfirmationAsync(
        OrderConfirmationViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.CustomerEmail))
        {
            throw new InvalidOperationException("Customer email is required for order confirmation emails.");
        }

        var subject = $"Potvrda narudzbine {model.OrderNumber} - {_storefrontSettings.BrandName}";
        var address = BuildAddress(model);
        var logoResource = CreateInlineLogoResource();
        var logoHtml = logoResource is null
            ? string.Empty
            : """
                <div style="text-align:center;margin-top:24px;">
                    <img src="cid:company-logo" alt="Kasmir Home" style="display:inline-block;max-width:140px;width:100%;height:auto;" />
                </div>
                """;

        var plainLines = model.Lines
            .Select(line => $"- {line.Title} x {line.Quantity}: {line.LineTotal.ToString("0.00")} €")
            .ToArray();

        var plainBodyBuilder = new StringBuilder()
            .AppendLine($"Zdravo {model.CustomerFirstName},")
            .AppendLine()
            .AppendLine($"Vaša narudžbina je uspješno evidentirana pod brojem {model.OrderNumber}.")
            .AppendLine("Plaćanje: pouzećem")
            .AppendLine()
            .AppendLine("Artikli:");

        foreach (var line in plainLines)
        {
            plainBodyBuilder.AppendLine(line);
        }

        plainBodyBuilder
            .AppendLine()
            .AppendLine($"Artikli ukupno: {model.ItemsTotal.ToString("0.00")} €")
            .AppendLine($"Dostava: {model.ShippingTotal.ToString("0.00")} €")
            .AppendLine($"Ukupno za plaćanje: {model.GrandTotal.ToString("0.00")} €")
            .AppendLine()
            .AppendLine("Adresa za dostavu:")
            .AppendLine(address);

        if (!string.IsNullOrWhiteSpace(model.CustomerPhone))
        {
            plainBodyBuilder.AppendLine($"Telefon: {model.CustomerPhone}");
        }

        if (!string.IsNullOrWhiteSpace(model.CustomerNote))
        {
            plainBodyBuilder.AppendLine($"Napomena: {model.CustomerNote}");
        }

        plainBodyBuilder
            .AppendLine()
            .AppendLine("Hvala vam na povjerenju.")
            .AppendLine()
            .AppendLine("Za pitanja o proizvodima, narudžbini i dostavi, kontaktirajte nas putem:")
            .AppendLine("- Telefon: +382 63 494 456")
            .AppendLine("- E-mail: kasmirhome@gmail.com")
            .AppendLine("- Instagram: kasmirhome.outlet");

        var htmlRows = string.Join(string.Empty, model.Lines.Select(line => $"""
            <tr>
                <td style="padding:10px 0;border-bottom:1px solid #ebe3d7;">
                    <table role="presentation" style="width:100%;border-collapse:collapse;">
                        <tr>
                            <td style="width:60px;padding:0 10px 0 0;vertical-align:top;">
                                {(string.IsNullOrWhiteSpace(line.ImageUrl)
                                    ? "<div style=\"width:52px;height:52px;border-radius:12px;background:#f4ede4;border:1px solid #e6dccd;\"></div>"
                                    : $"<img class=\"order-item-image\" src=\"{Html(line.ImageUrl)}\" alt=\"{Html(line.Title)}\" width=\"52\" height=\"52\" style=\"display:block;width:52px;height:52px;border-radius:12px;object-fit:cover;border:1px solid #e6dccd;\" />")}
                            </td>
                            <td style="vertical-align:top;">
                                <strong class="order-item-title" style="display:block;font-size:14px;line-height:1.4;">{Html(line.Title)}</strong>
                                {(string.IsNullOrWhiteSpace(line.Meta) ? string.Empty : $"<div class=\"order-item-meta\" style=\"color:#6b7280;font-size:13px;line-height:1.4;\">{Html(line.Meta)}</div>")}
                            </td>
                        </tr>
                    </table>
                </td>
                <td class="order-items-table__cell" style="padding:10px 0;border-bottom:1px solid #ebe3d7;text-align:center;">{line.Quantity}</td>
                <td class="order-items-table__cell" style="padding:10px 0;border-bottom:1px solid #ebe3d7;text-align:right;">{line.UnitPrice.ToString("0.00")} €</td>
                <td class="order-items-table__cell" style="padding:10px 0;border-bottom:1px solid #ebe3d7;text-align:right;"><strong>{line.LineTotal.ToString("0.00")} €</strong></td>
            </tr>
            """));

        var htmlBody = $$"""
            <style>
                @media only screen and (max-width: 600px) {
                    .order-items-table th,
                    .order-items-table td,
                    .order-items-table__cell {
                        font-size: 12px !important;
                    }

                    .order-item-title {
                        font-size: 13px !important;
                    }

                    .order-item-meta {
                        font-size: 11px !important;
                    }

                    .order-item-image {
                        width: 44px !important;
                        height: 44px !important;
                    }
                }
            </style>
            <div style="font-family:Segoe UI,Arial,sans-serif;line-height:1.6;color:#1f2933;">
                <p>Zdravo {{Html(model.CustomerFirstName)}},</p>
                <p>Vaša narudžbina je uspješno evidentirana pod brojem <strong>{{Html(model.OrderNumber)}}</strong>.</p>
                <p>Izdvojili smo najvažnije detalje ispod.</p>

                <table class="order-items-table" style="width:100%;border-collapse:collapse;margin:20px 0 8px;">
                    <thead>
                        <tr style="text-align:left;color:#6b7280;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;">
                            <th style="padding-bottom:10px;">Artikal</th>
                            <th style="padding-bottom:10px;text-align:center;">Kol.</th>
                            <th style="padding-bottom:10px;text-align:right;">Cijena</th>
                            <th style="padding-bottom:10px;text-align:right;">Ukupno</th>
                        </tr>
                    </thead>
                    <tbody>
                        {{htmlRows}}
                    </tbody>
                </table>

                <div style="margin-top:20px;padding:18px;border-radius:16px;background:#f9f6f1;border:1px solid #ebe3d7;">
                    <p style="margin:0 0 8px;"><strong>Artikli ukupno:</strong> {{model.ItemsTotal.ToString("0.00")}} €</p>
                    <p style="margin:0 0 8px;"><strong>Dostava:</strong> {{model.ShippingTotal.ToString("0.00")}} €</p>
                    <p style="margin:0;"><strong>Ukupno za plaćanje:</strong> {{model.GrandTotal.ToString("0.00")}} €</p>
                </div>

                <div style="margin-top:20px;">
                    <p style="margin:0 0 6px;"><strong>Adresa za dostavu</strong></p>
                    <p style="margin:0;">{{Html(address).Replace(Environment.NewLine, "<br />")}}</p>
                    {{(string.IsNullOrWhiteSpace(model.CustomerPhone) ? string.Empty : $"<p style=\"margin:8px 0 0;\"><strong>Telefon:</strong> {Html(model.CustomerPhone)}</p>")}}
                    {{(string.IsNullOrWhiteSpace(model.CustomerNote) ? string.Empty : $"<p style=\"margin:8px 0 0;\"><strong>Napomena:</strong> {Html(model.CustomerNote)}</p>")}}
                </div>

                <p style="margin-top:20px;">Plaćanje se vrši po prijemu pošiljke.</p>
                <p>Hvala vam na povjerenju.</p>

                <div style="margin-top:28px;padding-top:22px;border-top:1px solid #ebe3d7;text-align:center;">
                    {{logoHtml}}
                    <p style="margin:18px 0 10px;">Za pitanja o proizvodima, narudžbini i dostavi, kontaktirajte nas putem:</p>
                    <p style="margin:0 0 6px;">Telefon: <a href="tel:+38263494456" style="color:#b9643d;text-decoration:none;">+382 63 494 456</a></p>
                    <p style="margin:0 0 6px;">E-mail: <a href="mailto:kasmirhome@gmail.com" style="color:#b9643d;text-decoration:none;">kasmirhome@gmail.com</a></p>
                    <p style="margin:0;">Instagram: <a href="https://www.instagram.com/kasmirhome.outlet/" style="color:#b9643d;text-decoration:none;">kasmirhome.outlet</a></p>
                </div>
            </div>
            """;

        return SendEmailAsync(
            model.CustomerEmail,
            subject,
            plainBodyBuilder.ToString(),
            htmlBody,
            cancellationToken,
            logoResource is null ? null : new[] { logoResource });
    }

    private async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string plainBody,
        string htmlBody,
        CancellationToken cancellationToken,
        IEnumerable<LinkedResource>? linkedResources = null)
    {
        var senderEmail = _settings.SenderEmail.Trim();
        var senderDisplayName = _settings.SenderDisplayName.Trim();
        var smtpHost = _settings.SmtpHost.Trim();
        var smtpUsername = _settings.SmtpUsername.Trim();
        var smtpPassword = NormalizeSmtpPassword(smtpHost, _settings.SmtpPassword);

        ValidateConfiguration(senderEmail, senderDisplayName, smtpHost, smtpUsername, smtpPassword);

        using var message = new MailMessage
        {
            From = new MailAddress(senderEmail, senderDisplayName),
            Subject = subject,
            Body = plainBody,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        message.To.Add(recipientEmail);

        var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html);
        if (linkedResources is not null)
        {
            foreach (var linkedResource in linkedResources)
            {
                htmlView.LinkedResources.Add(linkedResource);
            }
        }

        message.AlternateViews.Add(htmlView);

        using var client = new SmtpClient(smtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException ex) when (IsAuthenticationFailure(ex))
        {
            _logger.LogError(ex, "SMTP authentication failed for storefront email sender {SenderEmail}.", senderEmail);
            throw new InvalidOperationException(
                "SMTP authentication failed. For Gmail you must enable 2-Step Verification, create an App Password, and store that App Password in StorefrontEmail:SmtpPassword.",
                ex);
        }
        catch (SmtpException ex) when (IsDnsResolutionFailure(ex))
        {
            _logger.LogError(ex, "SMTP host resolution failed for storefront email host {SmtpHost}.", smtpHost);
            throw new InvalidOperationException(
                $"SMTP host '{smtpHost}' could not be resolved. Check StorefrontEmail:SmtpHost in configuration.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send storefront email to {RecipientEmail}.", recipientEmail);
            throw;
        }
    }

    private static void ValidateConfiguration(
        string senderEmail,
        string senderDisplayName,
        string smtpHost,
        string smtpUsername,
        string smtpPassword)
    {
        if (string.IsNullOrWhiteSpace(senderEmail) ||
            string.IsNullOrWhiteSpace(senderDisplayName) ||
            string.IsNullOrWhiteSpace(smtpHost) ||
            string.IsNullOrWhiteSpace(smtpUsername) ||
            string.IsNullOrWhiteSpace(smtpPassword))
        {
            throw new InvalidOperationException(
                "Storefront email settings are incomplete. Configure SMTP before sending emails.");
        }

        if (StorefrontEmailSettings.UsesPlaceholder(senderEmail) ||
            StorefrontEmailSettings.UsesPlaceholder(smtpHost) ||
            StorefrontEmailSettings.UsesPlaceholder(smtpUsername) ||
            StorefrontEmailSettings.UsesPlaceholder(smtpPassword))
        {
            throw new InvalidOperationException(
                "Storefront email settings still contain placeholder values. Update StorefrontEmail in appsettings.Staging.local.json before sending emails.");
        }
    }

    private LinkedResource? CreateInlineLogoResource()
    {
        var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "slike", "KasmirHomeLogo.png");
        if (!File.Exists(logoPath))
        {
            return null;
        }

        var logoResource = new LinkedResource(logoPath, MediaTypeNames.Image.Jpeg)
        {
            ContentId = "company-logo",
            TransferEncoding = TransferEncoding.Base64
        };

        if (string.Equals(Path.GetExtension(logoPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            logoResource.ContentType = new ContentType("image/png");
        }

        return logoResource;
    }

    private static string BuildAddress(OrderConfirmationViewModel model)
    {
        var segments = new[]
        {
            model.ShippingAddressLine1,
            model.ShippingCity,
            model.ShippingCountry
        };

        return string.Join(Environment.NewLine, segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private static string Html(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static bool IsAuthenticationFailure(SmtpException ex)
    {
        return ex.Message.Contains("Authentication Required", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("client was not authenticated", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("requires a secure connection", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDnsResolutionFailure(SmtpException ex)
    {
        return ex.InnerException is System.Net.Sockets.SocketException socketException &&
               socketException.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound;
    }

    private static string NormalizeSmtpPassword(string smtpHost, string? smtpPassword)
    {
        var password = (smtpPassword ?? string.Empty).Trim();
        if (!smtpHost.EndsWith("gmail.com", StringComparison.OrdinalIgnoreCase) ||
            !password.Any(char.IsWhiteSpace))
        {
            return password;
        }

        var compactPassword = new string(password.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        if (compactPassword.Length == 16 && compactPassword.All(char.IsLetterOrDigit))
        {
            return compactPassword;
        }

        return password;
    }
}
