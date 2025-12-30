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
}
