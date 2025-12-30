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

    /// <summary>
    /// Send a general email
    /// </summary>
    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
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
                _logger.LogWarning("SMTP configuration incomplete. Email will be logged only.");
                _logger.LogInformation("Email would be sent to {To}: {Subject}\nBody: {Body}", to, subject, body);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;

            message.Body = new TextPart(isHtml ? TextFormat.Html : TextFormat.Plain)
            {
                Text = body
            };

            // Log in development
            if (_configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                _logger.LogInformation("DEV: Sending email to {Email}: {Subject}", to, subject);
            }

            using var client = new SmtpClient();
            var socketOptions = useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.StartTls;

            await client.ConnectAsync(host, port, socketOptions);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Email}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
        }
    }

    /// <summary>
    /// Send password reset email
    /// </summary>
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

            var resetLink = $"https://localhost:7213/reset-password?token={resetToken}";

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

            if (_configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                _logger.LogInformation("DEV: Password reset link for {Email}: {Link}", email, resetLink);
            }

            using var client = new SmtpClient();
            var socketOptions = useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.StartTls;

            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Password reset email sent successfully to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
        }
    }

    /// <summary>
    /// Send welcome email with password setup link for newly created admin users
    /// </summary>
    public async Task SendWelcomeWithPasswordSetupAsync(
        string email, 
        string role, 
        string resetLink,
        CancellationToken cancellationToken = default)
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
            var fromName = smtpSettings["FromName"] ?? "eCommerce Admin";

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("SMTP configuration incomplete. Email will be logged only.");
                _logger.LogInformation("Welcome email would be sent to {Email} with role {Role}", email, role);
                return;
            }

            var subject = "Welcome to eCommerce Admin - Set Your Password";
            
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; background-color: #f5f5f5;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color: #f5f5f5; padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 40px 30px; border-radius: 8px 8px 0 0;'>
                            <h1 style='margin: 0; color: #ffffff; font-size: 28px; font-weight: 600; text-align: center;'>
                                Welcome to eCommerce Admin! 🎉
                            </h1>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            <p style='margin: 0 0 20px; color: #333333; font-size: 16px; line-height: 1.6;'>
                                Your administrator account has been successfully created. You're now part of the team!
                            </p>
                            
                            <div style='background-color: #f8f9fa; border-left: 4px solid #667eea; padding: 20px; margin: 25px 0; border-radius: 4px;'>
                                <table width='100%' cellpadding='0' cellspacing='0'>
                                    <tr>
                                        <td style='padding: 5px 0;'>
                                            <strong style='color: #495057; font-size: 14px;'>Email:</strong>
                                            <span style='color: #212529; font-size: 14px; margin-left: 10px;'>{email}</span>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 5px 0;'>
                                            <strong style='color: #495057; font-size: 14px;'>Role:</strong>
                                            <span style='color: #212529; font-size: 14px; margin-left: 10px;'>{role}</span>
                                        </td>
                                    </tr>
                                </table>
                            </div>
                            
                            <h2 style='margin: 30px 0 15px; color: #333333; font-size: 20px; font-weight: 600;'>
                                🔐 Next Step: Set Your Password
                            </h2>
                            
                            <p style='margin: 0 0 25px; color: #555555; font-size: 15px; line-height: 1.6;'>
                                To activate your account and get started, please create your password by clicking the button below:
                            </p>
                            
                            <table width='100%' cellpadding='0' cellspacing='0'>
                                <tr>
                                    <td align='center' style='padding: 20px 0;'>
                                        <a href='{resetLink}' 
                                           style='display: inline-block; 
                                                  padding: 16px 40px; 
                                                  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                                                  color: #ffffff; 
                                                  text-decoration: none; 
                                                  border-radius: 6px;
                                                  font-size: 16px;
                                                  font-weight: 600;
                                                  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);'>
                                            Set My Password
                                        </a>
                                    </td>
                                </tr>
                            </table>
                            
                            <div style='background-color: #fff3cd; border: 1px solid #ffc107; border-radius: 4px; padding: 15px; margin: 25px 0;'>
                                <p style='margin: 0; color: #856404; font-size: 13px; line-height: 1.5;'>
                                    ⏰ <strong>Security Notice:</strong> This link will expire in 24 hours for your protection.
                                </p>
                            </div>
                            
                            <p style='margin: 25px 0 0; color: #666666; font-size: 14px; line-height: 1.6;'>
                                If you have any questions or need assistance, please contact your system administrator.
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f8f9fa; padding: 30px 40px; border-radius: 0 0 8px 8px; border-top: 1px solid #e9ecef;'>
                            <p style='margin: 0 0 10px; color: #6c757d; font-size: 13px; line-height: 1.5;'>
                                Best regards,<br>
                                <strong style='color: #495057;'>The eCommerce Team</strong>
                            </p>
                            <p style='margin: 15px 0 0; color: #adb5bd; font-size: 12px;'>
                                This is an automated message. Please do not reply to this email.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = subject;

            message.Body = new TextPart(TextFormat.Html)
            {
                Text = body
            };

            if (_configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                _logger.LogInformation("DEV: Sending welcome email to {Email} with role {Role}", email, role);
                _logger.LogInformation("DEV: Password setup link: {Link}", resetLink);
            }

            using var client = new SmtpClient();
            var socketOptions = useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.StartTls;

            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Welcome email with password setup link sent successfully to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
        }
    }
}