namespace Commerce.Application.Common.Interfaces;

/// <summary>
/// Security audit logging interface for tracking authentication events
/// </summary>
public interface ISecurityLogger
{
    void LogLoginSuccess(string email, string userId, string? ipAddress = null);
    void LogLoginFailure(string email, string reason, string? ipAddress = null);
    void LogPasswordResetRequested(string email, string? ipAddress = null);
    void LogPasswordResetCompleted(string email, string? ipAddress = null);
    void LogTokenRefreshed(string userId, string? ipAddress = null);
    void LogTokenRevoked(string userId, string? ipAddress = null);
    void LogMfaEnabled(string userId, string? ipAddress = null);
    void LogMfaDisabled(string userId, string? ipAddress = null);
    void LogMfaVerified(string userId, bool success, string? ipAddress = null);
}
