using Commerce.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using MailKit.Security;

namespace Commerce.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

   public async Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default)
{
    try
    {
        var smtpSettings = _configuration.GetSection("Smtp");
        var host = smtpSettings["Host"];
        var portValue = smtpSettings["Port"];
        var port = int.TryParse(portValue, out var p) ? p : 587;
        var useSsl = bool.TryParse(smtpSettings["UseSsl"], out var ssl) && ssl;
        var username = smtpSettings["Username"];
        var password = smtpSettings["Password"];
        var fromEmail = smtpSettings["FromEmail"] ?? username;
        var fromName = smtpSettings["FromName"] ?? "Your Commerce App";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("SMTP configuration incomplete. Skipping email send.");
            return;
        }

        var resetLink = $"https://localhost:7213/reset-password?token={resetToken}"; // Change for prod

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress("", email));
        message.Subject = "Password Reset Request";

        message.Body = new TextPart(TextFormat.Plain)
        {
            Text = $@"
Hello,

You requested a password reset for your account.

Click the link below to set a new password:
{resetLink}

This link will expire in 15 minutes.

If you did not request this, please ignore this email.

Thank you,
Your Commerce Team
"
        };

        // Log in development
        if (_configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
        {
            _logger.LogInformation("DEV: Password reset link for {Email}: {Link}", email, resetLink);
        }

        using var client = new SmtpClient();
        var socketOptions = useSsl ? MailKit.Security.SecureSocketOptions.Auto : MailKit.Security.SecureSocketOptions.StartTls;

        await client.ConnectAsync(host, port, socketOptions, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Password reset email sent successfully to {Email}", email);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
        // Do NOT throw — email failure shouldn't break the reset flow
    }
}
}
