using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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
}
