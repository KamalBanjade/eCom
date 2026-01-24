using Commerce.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using MailKit.Security;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Entities.Sales;
using System.Text;

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
            var fromName = smtpSettings["FromName"] ?? "eCommerce Admin";

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("SMTP configuration incomplete. Email will be logged only.");
                _logger.LogInformation("Email would be sent to {To}: {Subject}", to, subject);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;
            message.Body = new TextPart(isHtml ? TextFormat.Html : TextFormat.Plain) { Text = body };

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
            var fromName = smtpSettings["FromName"] ?? "eCommerce Admin";

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
                Text = $"Hello,\n\nYou requested a password reset for your account.\n\nClick the link below to set a new password:\n{resetLink}\n\nThis link will expire in 15 minutes.\n\nIf you did not request this, please ignore this email.\n\nThank you,\nYour Commerce Team"
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

    public async Task SendWelcomeWithPasswordSetupAsync(string email, string role, string resetLink, CancellationToken cancellationToken = default)
    {
        var body = BuildWelcomeEmailHtml(email, role, resetLink);
        await SendHtmlEmailAsync(email, "Welcome to eCommerce Admin - Set Your Password", body, cancellationToken);
    }

    public async Task SendForgotPasswordEmailAsync(string email, string resetLink, CancellationToken cancellationToken = default)
    {
        var body = BuildForgotPasswordEmailHtml(email, resetLink);
        await SendHtmlEmailAsync(email, "Reset Your Password - eCommerce Admin", body, cancellationToken);
    }

    public async Task SendOrderNotificationToWarehouseAsync(Order order, string warehouseEmail, CancellationToken cancellationToken = default)
    {
        var subject = $"New Order #{order.OrderNumber} - Ready for Packing";
        var body = BuildOrderNotificationHtml(order);
        await SendHtmlEmailAsync(warehouseEmail, subject, body, cancellationToken, "Warehouse Staff");
    }

    public async Task SendReturnNotificationToSupportAsync(ReturnRequest returnRequest, string supportEmail, CancellationToken cancellationToken = default)
    {
        var order = returnRequest.Order;
        var orderNumber = order?.OrderNumber ?? "N/A";
        var subject = $"New Return Request #{returnRequest.Id} - Order #{orderNumber} - Ready for Review";
        var body = BuildReturnNotificationHtml(returnRequest);
        await SendHtmlEmailAsync(supportEmail, subject, body, cancellationToken, "Support Team");
    }

    public async Task SendRefundConfirmationEmailAsync(ReturnRequest returnRequest, string customerEmail, CancellationToken cancellationToken = default)
    {
        var order = returnRequest.Order;
        var orderNumber = order?.OrderNumber ?? "N/A";
        var subject = $"Refund Processed: Order #{orderNumber}";
        var body = BuildRefundConfirmationHtml(returnRequest);
        
        var toName = order?.CustomerProfile?.FullName ?? "";
        await SendHtmlEmailAsync(customerEmail, subject, body, cancellationToken, toName);
    }

    private async Task SendHtmlEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default, string toName = "")
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
                _logger.LogInformation("Email would be sent to {Email}: {Subject}", to, subject);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(toName, to));
            message.Subject = subject;
            message.Body = new TextPart(TextFormat.Html) { Text = body };

            if (_configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                _logger.LogInformation("DEV: Sending email to {Email}: {Subject}", to, subject);
            }

            using var client = new SmtpClient();
            var socketOptions = useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.StartTls;
            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
        }
    }

    private string BuildWelcomeEmailHtml(string email, string role, string resetLink)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;background-color:#f5f5f5'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f5f5f5;padding:40px 20px'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color:#fff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1)'>
                    <tr>
                        <td style='background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);padding:40px;border-radius:8px 8px 0 0'>
                            <h1 style='margin:0;color:#fff;font-size:28px;font-weight:600;text-align:center'>Welcome to eCommerce Admin!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:40px'>
                            <p style='margin:0 0 20px;color:#333;font-size:16px'>Your administrator account has been successfully created.</p>
                            <div style='background-color:#f8f9fa;border-left:4px solid #667eea;padding:20px;margin:25px 0;border-radius:4px'>
                                <p style='margin:5px 0'><strong style='color:#495057'>Email:</strong> <span style='color:#212529'>{email}</span></p>
                                <p style='margin:5px 0'><strong style='color:#495057'>Role:</strong> <span style='color:#212529'>{role}</span></p>
                            </div>
                            <h2 style='margin:30px 0 15px;color:#333;font-size:20px'>Next Step: Set Your Password</h2>
                            <p style='margin:0 0 25px;color:#555;font-size:15px'>To activate your account, please create your password by clicking the button below:</p>
                            <table width='100%' cellpadding='0' cellspacing='0'>
                                <tr>
                                    <td align='center' style='padding:20px 0'>
                                        <a href='{resetLink}' style='display:inline-block;padding:16px 40px;background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:#fff;text-decoration:none;border-radius:6px;font-size:16px;font-weight:600'>Set My Password</a>
                                    </td>
                                </tr>
                            </table>
                            <div style='background-color:#fff3cd;border:1px solid #ffc107;border-radius:4px;padding:15px;margin:25px 0'>
                                <p style='margin:0;color:#856404;font-size:13px'><strong>Security Notice:</strong> This link will expire in 24 hours.</p>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style='background-color:#f8f9fa;padding:30px 40px;border-radius:0 0 8px 8px;border-top:1px solid #e9ecef'>
                            <p style='margin:0;color:#6c757d;font-size:13px'>Best regards,<br><strong>The eCommerce Team</strong></p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string BuildForgotPasswordEmailHtml(string email, string resetLink)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;background-color:#f5f5f5'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f5f5f5;padding:40px 20px'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color:#fff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1)'>
                    <tr>
                        <td style='background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);padding:40px;border-radius:8px 8px 0 0'>
                            <h1 style='margin:0;color:#fff;font-size:28px;font-weight:600;text-align:center'>Password Reset Request</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:40px'>
                            <p style='margin:0 0 20px;color:#333;font-size:16px'>Hello,</p>
                            <p style='margin:0 0 20px;color:#333;font-size:16px'>We received a request to reset the password for your account <strong>{email}</strong>.</p>
                            <table width='100%' cellpadding='0' cellspacing='0'>
                                <tr>
                                    <td align='center' style='padding:20px 0'>
                                        <a href='{resetLink}' style='display:inline-block;padding:16px 40px;background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:#fff;text-decoration:none;border-radius:6px;font-size:16px;font-weight:600'>Reset My Password</a>
                                    </td>
                                </tr>
                            </table>
                            <div style='background-color:#fff3cd;border:1px solid #ffc107;border-radius:4px;padding:15px;margin:25px 0'>
                                <p style='margin:0;color:#856404;font-size:13px'><strong>Security Notice:</strong> This link will expire in 24 hours.</p>
                            </div>
                            <div style='background-color:#f8d7da;border:1px solid #f5c6cb;border-radius:4px;padding:15px;margin:25px 0'>
                                <p style='margin:0;color:#721c24;font-size:13px'><strong>Didn't request this?</strong> Ignore this email.</p>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style='background-color:#f8f9fa;padding:30px 40px;border-radius:0 0 8px 8px;border-top:1px solid #e9ecef'>
                            <p style='margin:0;color:#6c757d;font-size:13px'>Best regards,<br><strong>The eCommerce Team</strong></p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string BuildOrderNotificationHtml(Order order)
    {
        var sb = new StringBuilder();
        if (order.Items != null)
        {
            foreach (var item in order.Items)
            {
                sb.Append($"<li style='margin-bottom:8px'><strong>{item.ProductName}</strong> - {item.VariantName ?? "Standard"}, Qty: {item.Quantity} x Rs. {item.UnitPrice:N2}</li>");
            }
        }
        var itemsList = sb.ToString();
        var paymentInfo = order.PaymentMethod.ToString();
        if (order.PaymentMethod == Domain.Enums.PaymentMethod.CashOnDelivery)
            paymentInfo += $" - <strong>Collect Rs. {order.TotalAmount:N2}</strong>";
        else
            paymentInfo += " (Paid via Khalti)";

        var customerName = order.CustomerProfile?.FullName ?? "Guest";
        var customerPhone = order.CustomerProfile?.PhoneNumber ?? "N/A";
        var address = $"{order.ShippingAddress?.Street}, {order.ShippingAddress?.City}, {order.ShippingAddress?.State ?? ""} {order.ShippingAddress?.PostalCode ?? ""}";
        var adminUrl = _configuration["AdminAppUrl"] ?? "https://localhost:7213";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body style='margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;background-color:#f7fafc'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f7fafc;padding:40px 20px'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color:#fff;border-radius:12px;box-shadow:0 10px 25px rgba(0,0,0,0.08)'>
                    <tr>
                        <td style='background:linear-gradient(135deg,#6366f1 0%,#4f46e5 100%);padding:32px 24px;border-radius:12px 12px 0 0'>
                            <h1 style='margin:0;color:#fff;font-size:28px;font-weight:600;text-align:center'>New Order #{order.OrderNumber}</h1>
                            <p style='margin:8px 0 0;color:#fff;font-size:16px;text-align:center;opacity:0.9'>Ready for Picking &amp; Packing</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:28px 24px'>
                            <div style='margin-bottom:32px'>
                                <h3 style='color:#4f46e5;font-size:18px;margin:0 0 16px;padding-bottom:8px;border-bottom:2px solid #e2e8f0'>Customer Information</h3>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Name:</span><span style='color:#2d3748'>{customerName}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Phone:</span><span style='color:#2d3748'>{customerPhone}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Address:</span><span style='color:#2d3748'>{address}</span></p>
                            </div>
                            <div style='margin-bottom:32px'>
                                <h3 style='color:#4f46e5;font-size:18px;margin:0 0 16px;padding-bottom:8px;border-bottom:2px solid #e2e8f0'>Order Items</h3>
                                <ul style='list-style:none;padding:0;margin:0'>{itemsList}</ul>
                            </div>
                            <div style='margin-bottom:32px'>
                                <h3 style='color:#4f46e5;font-size:18px;margin:0 0 16px;padding-bottom:8px;border-bottom:2px solid #e2e8f0'>Payment Summary</h3>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Method:</span><span style='color:#2d3748'>{paymentInfo}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Total:</span><span style='color:#e53e3e;font-weight:700;font-size:1.2em'>Rs. {order.TotalAmount:N2}</span></p>
                            </div>
                            <div style='text-align:center;margin:36px 0 20px'>
                                <p style='margin-bottom:16px;color:#4a5568;font-size:15px'>Time to prepare this order!</p>
                                <a href='{adminUrl}/admin/orders/{order.Id}' style='display:inline-block;padding:14px 32px;background:#4f46e5;color:#fff;text-decoration:none;font-weight:600;border-radius:8px;font-size:16px'>View &amp; Manage Order</a>
                            </div>
                            <div style='text-align:center;color:#718096;font-size:14px;margin-top:20px'>Thank you for keeping orders moving smoothly!</div>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string BuildReturnNotificationHtml(ReturnRequest returnRequest)
    {
        var order = returnRequest.Order;
        var orderNumber = order?.OrderNumber ?? "N/A";
        var sb = new StringBuilder();
        if (order?.Items != null)
        {
            foreach (var item in order.Items)
            {
                sb.Append($"<tr><td style='padding:12px 8px;border-bottom:1px solid #e2e8f0'>{item.ProductName} - {item.VariantName ?? "Standard"}</td><td style='padding:12px 8px;text-align:center;border-bottom:1px solid #e2e8f0'>{item.Quantity}</td><td style='padding:12px 8px;text-align:right;border-bottom:1px solid #e2e8f0;font-weight:600'>Rs. {item.UnitPrice * item.Quantity:N2}</td></tr>");
            }
        }
        var itemsTable = $"<table style='width:100%;border-collapse:collapse;margin:12px 0'><thead><tr style='background:#f1f5f9'><th style='padding:12px 8px;text-align:left;border-bottom:1px solid #e2e8f0;font-weight:600;color:#4a5568'>Item</th><th style='padding:12px 8px;text-align:center;border-bottom:1px solid #e2e8f0;font-weight:600;color:#4a5568;width:60px'>Qty</th><th style='padding:12px 8px;text-align:right;border-bottom:1px solid #e2e8f0;font-weight:600;color:#4a5568;width:90px'>Price</th></tr></thead><tbody>{sb}</tbody></table>";

        var refundInfo = returnRequest.TotalRefundAmount > 0 ? $"Rs. {returnRequest.TotalRefundAmount:N2}" : (order?.TotalAmount.ToString("N2") ?? "TBD");
        var customerName = order?.CustomerProfile?.FullName ?? "Guest";
        var customerPhone = order?.CustomerProfile?.PhoneNumber ?? "N/A";
        var address = $"{order?.ShippingAddress?.Street}, {order?.ShippingAddress?.City}, {order?.ShippingAddress?.State ?? ""} {order?.ShippingAddress?.PostalCode ?? ""}";
        var adminUrl = _configuration["AdminAppUrl"] ?? "https://localhost:7213";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body style='margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;background-color:#f7fafc'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f7fafc;padding:40px 20px'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color:#fff;border-radius:12px;box-shadow:0 10px 25px rgba(0,0,0,0.08)'>
                    <tr>
                        <td style='background:linear-gradient(135deg,#f59e0b 0%,#d97706 100%);padding:32px 24px;border-radius:12px 12px 0 0'>
                            <h1 style='margin:0;color:#fff;font-size:28px;font-weight:600;text-align:center'>New Return Request #{returnRequest.Id}</h1>
                            <p style='margin:8px 0 0;color:#fff;font-size:16px;text-align:center;opacity:0.9'>Order #{orderNumber} - Ready for Review</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:28px 24px'>
                            <div style='margin-bottom:32px'>
                                <h3 style='color:#f59e0b;font-size:18px;margin:0 0 16px;padding-bottom:8px;border-bottom:2px solid #e2e8f0'>Customer Information</h3>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Name:</span><span style='color:#2d3748'>{customerName}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Phone:</span><span style='color:#2d3748'>{customerPhone}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Address:</span><span style='color:#2d3748'>{address}</span></p>
                            </div>
                            <div style='margin-bottom:32px'>
                                <h3 style='color:#f59e0b;font-size:18px;margin:0 0 16px;padding-bottom:8px;border-bottom:2px solid #e2e8f0'>Return Details</h3>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Return ID:</span><span style='color:#2d3748'>{returnRequest.Id}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Reason:</span><span style='color:#2d3748'>{string.Join(", ", returnRequest.Items?.Select(i => i.Reason) ?? Enumerable.Empty<string>())}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Requested:</span><span style='color:#2d3748'>{returnRequest.RequestedAt:yyyy-MM-dd HH:mm}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Status:</span><span style='color:#2d3748'>{returnRequest.ReturnStatus}</span></p>
                            </div>
                            <div style='margin-bottom:32px'>
                                <h3 style='color:#f59e0b;font-size:18px;margin:0 0 16px;padding-bottom:8px;border-bottom:2px solid #e2e8f0'>Order Items</h3>
                                {itemsTable}
                            </div>
                            <div style='margin-bottom:32px'>
                                <h3 style='color:#f59e0b;font-size:18px;margin:0 0 16px;padding-bottom:8px;border-bottom:2px solid #e2e8f0'>Refund Summary</h3>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Amount:</span><span style='color:#e53e3e;font-weight:700;font-size:1.2em'>Rs. {refundInfo}</span></p>
                                <p style='margin:8px 0'><span style='display:inline-block;width:110px;font-weight:600;color:#4a5568'>Payment:</span><span style='color:#2d3748'>{order?.PaymentMethod.ToString() ?? "N/A"}</span></p>
                            </div>
                            <div style='text-align:center;margin:36px 0 20px'>
                                <p style='margin-bottom:16px;color:#4a5568;font-size:15px'>Please review this return request and take action.</p>
                                <a href='{adminUrl}/admin/returns/{returnRequest.Id}' style='display:inline-block;padding:14px 32px;background:#f59e0b;color:#fff;text-decoration:none;font-weight:600;border-radius:8px;font-size:16px'>Review &amp; Process Return</a>
                            </div>
                            <div style='text-align:center;color:#718096;font-size:14px;margin-top:20px'>Thank you for providing excellent customer support!</div>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string BuildRefundConfirmationHtml(ReturnRequest returnRequest)
    {
        var order = returnRequest.Order;
        var orderNumber = order?.OrderNumber ?? "N/A";
        var customerName = order?.CustomerProfile?.FullName ?? "Customer";
        var refundAmount = returnRequest.TotalRefundAmount.ToString("N2");
        var refundMethod = returnRequest.RefundMethod?.ToString() ?? "Original Payment Method";
        
        var sb = new StringBuilder();
        if (returnRequest.Items != null)
        {
            foreach (var item in returnRequest.Items)
            {
                sb.Append($"<li style='margin-bottom:8px'><strong>{item.OrderItem.ProductName}</strong> - {item.OrderItem.VariantName ?? "Standard"}, Qty: {item.Quantity}</li>");
            }
        }
        var itemsList = sb.ToString();

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body style='margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;background-color:#f9fafb'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f9fafb;padding:40px 20px'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background-color:#fff;border-radius:12px;box-shadow:0 4px 12px rgba(0,0,0,0.05)'>
                    <tr>
                        <td style='background:linear-gradient(135deg,#10b981 0%,#059669 100%);padding:32px 24px;border-radius:12px 12px 0 0;text-align:center'>
                            <h1 style='margin:0;color:#fff;font-size:24px;font-weight:700'>Refund Successful</h1>
                            <p style='margin:8px 0 0;color:#fff;font-size:16px;opacity:0.9'>Order #{orderNumber}</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:32px 24px'>
                            <p style='margin:0 0 20px;color:#374151;font-size:16px'>Dear {customerName},</p>
                            <p style='margin:0 0 16px;color:#4b5563;line-height:1.6'>We are writing to confirm that your refund for <strong>Order #{orderNumber}</strong> has been successfully processed. We've credited the amount back to your original payment method.</p>
                            <p style='margin:0 0 24px;color:#4b5563;line-height:1.6'>We sincerely apologize for any inconvenience caused by this return. Providing a seamless shopping experience is our priority, and we hope to serve you better in your future purchases.</p>
                            
                            <div style='background-color:#f0fdf4;border:1px solid #bcf0da;border-radius:12px;padding:24px;margin-bottom:32px'>
                                <h3 style='margin:0 0 16px;color:#065f46;font-size:13px;text-transform:uppercase;letter-spacing:0.05em;font-weight:700'>Refund Summary</h3>
                                <table width='100%' cellpadding='0' cellspacing='0'>
                                    <tr>
                                        <td style='padding:4px 0;color:#374151;font-weight:600;width:120px'>Refund Amount:</td>
                                        <td style='padding:4px 0;color:#059669;font-weight:800;font-size:1.1em'>Rs. {refundAmount}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding:4px 0;color:#374151;font-weight:600'>Method:</td>
                                        <td style='padding:4px 0;color:#374151'>{refundMethod}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding:4px 0;color:#374151;font-weight:600'>Processed On:</td>
                                        <td style='padding:4px 0;color:#374151'>{DateTime.UtcNow:MMMM dd, yyyy}</td>
                                    </tr>
                                </table>
                            </div>

                            <h3 style='margin:0 0 12px;color:#111827;font-size:14px;text-transform:uppercase;letter-spacing:0.05em;font-weight:700'>Items Refunded</h3>
                            <ul style='margin:0;padding:0;list-style:none;color:#4b5563;border-top:1px solid #f3f4f6'>
                                {itemsList.Replace("<li ", "<li style='padding:12px 0;border-bottom:1px solid #f3f4f6;display:flex;align-items:center' ")}
                            </ul>

                            <div style='text-align:center;margin:40px 0 20px'>
                                <p style='margin-bottom:20px;color:#6b7280;font-size:15px'>Ready to find something new?</p>
                                <a href='{_configuration["ClientUrl"] ?? "http://localhost:3000"}' style='display:inline-block;padding:16px 40px;background:linear-gradient(135deg,#10b981 0%,#059669 100%);color:#fff;text-decoration:none;border-radius:8px;font-size:16px;font-weight:600;box-shadow:0 4px 6px rgba(16, 185, 129, 0.2)'>Continue Shopping</a>
                            </div>

                            <hr style='border:0;border-top:1px solid #e5e7eb;margin:40px 0'>

                            <p style='margin:0;color:#6b7280;font-size:14px;text-align:center;line-height:1.5'>Questions? Our support team is here to help.<br>Simply reply to this email or visit our <a href='#' style='color:#059669;text-decoration:none;font-weight:600'>Help Center</a>.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='background-color:#f9fafb;padding:24px;border-radius:0 0 12px 12px;text-align:center;border-top:1px solid #e5e7eb'>
                            <p style='margin:0;color:#9ca3af;font-size:12px'>&copy; {DateTime.UtcNow.Year} Commerce Team. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
