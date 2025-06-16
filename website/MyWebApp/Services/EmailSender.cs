using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MyWebApp.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string message);
    }

    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;
        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }
        public Task SendEmailAsync(string email, string subject, string message)
        {
            _logger.LogInformation("Sending email to {Email} - {Subject}\n{Message}", email, subject, message);
            return Task.CompletedTask;
        }
    }

    public class SmtpEmailSender : IEmailSender
    {
        private readonly ILogger<SmtpEmailSender> _logger;
        private readonly MyWebApp.Options.SmtpOptions _options;
        public SmtpEmailSender(ILogger<SmtpEmailSender> logger, Microsoft.Extensions.Options.IOptions<MyWebApp.Options.SmtpOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            try
            {
                using var client = new System.Net.Mail.SmtpClient(_options.Host, _options.Port)
                {
                    EnableSsl = _options.UseSsl,
                    Credentials = new System.Net.NetworkCredential(_options.Username, _options.Password)
                };
                var mail = new System.Net.Mail.MailMessage(_options.From, email, subject, message) { IsBodyHtml = true };
                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP send failed");
            }
        }
    }
}
