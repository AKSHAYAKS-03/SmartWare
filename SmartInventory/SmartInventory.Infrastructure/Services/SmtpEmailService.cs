using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            var host = _configuration["EmailSettings:SmtpHost"];
            var portString = _configuration["EmailSettings:SmtpPort"];
            var user = _configuration["EmailSettings:SmtpUser"];
            var pass = _configuration["EmailSettings:AppPassword"];
            var fromName = _configuration["EmailSettings:FromName"] ?? "SmartInventory WMS";

            var fromAddress =
                _configuration["EmailSettings:FromAddress"]
                ?? user
                ?? throw new InvalidOperationException(
                    "EmailSettings:FromAddress is not configured.");

            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                throw new InvalidOperationException(
                    "Email sender address is empty.");
}


            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                _logger.LogWarning("Email settings are not fully configured. Simulating email send to {To}.", to);
                return;
            }

            int.TryParse(portString, out int port);
            if (port == 0) port = 587; // Default to TLS port

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromAddress));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = isHtml ? body : null,
                TextBody = isHtml ? null : body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // Accept all SSL certificates (in case the server supports STARTTLS but has an invalid cert)
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Successfully sent email to {To} with subject: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}. Subject: {Subject}", to, subject);
            // Throwing might break background processors if not caught, but here we just log it.
        }
    }
}
