using Inventar.Helpers;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Inventar.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly SendGridSettings _settings;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IOptions<SendGridSettings> settings, ILogger<EmailSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            ValidateConfiguration();

            var client = new SendGridClient(_settings.ApiKey);
            var from = new EmailAddress(_settings.SenderEmail, _settings.SenderDisplayName);
            var to = new EmailAddress(email);
            var mailMessage = MailHelper.CreateSingleEmail(from, to, subject, message, message);

            try
            {
                await client.SendEmailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin email to {RecipientEmail}.", email);
                throw;
            }
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
                string.IsNullOrWhiteSpace(_settings.SenderEmail))
            {
                throw new InvalidOperationException(
                    "SendGrid settings are incomplete. Configure SendGrid:ApiKey and SendGrid:SenderEmail before sending emails.");
            }
        }
    }
}
