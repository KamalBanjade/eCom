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
}
