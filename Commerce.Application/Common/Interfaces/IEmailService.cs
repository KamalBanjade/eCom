using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Entities.Sales;

namespace Commerce.Application.Common.Interfaces;

/// <summary>
/// Email service contract for sending emails
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a password reset email with a reset link
    /// </summary>
    Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default);

    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);

    /// <summary>
    /// Sends a welcome email with password setup link for newly created admin users
    /// </summary>
    Task SendWelcomeWithPasswordSetupAsync(
        string email, 
        string role, 
        string resetLink,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a forgot password email with reset link for existing users
    /// </summary>
    Task SendForgotPasswordEmailAsync(
        string email,
        string resetLink,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification email to the warehouse with order details
    /// </summary>
    Task SendOrderNotificationToWarehouseAsync(Order order, string warehouseEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification email to support with return request details
    /// </summary>
    Task SendReturnNotificationToSupportAsync(ReturnRequest returnRequest, string supportEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a confirmation email to the customer after a successful refund
    /// </summary>
    Task SendRefundConfirmationEmailAsync(ReturnRequest returnRequest, string customerEmail, CancellationToken cancellationToken = default);
}
