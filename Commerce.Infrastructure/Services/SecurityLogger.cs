using Commerce.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Commerce.Infrastructure.Services;

/// <summary>
/// Structured security event logging for audit trails
/// </summary>
public class SecurityLogger : ISecurityLogger
{
    private readonly ILogger<SecurityLogger> _logger;

    public SecurityLogger(ILogger<SecurityLogger> logger)
    {
        _logger = logger;
    }

    public void LogLoginSuccess(string email, string userId, string? ipAddress = null)
    {
        _logger.LogInformation(
            "SECURITY: Login successful - Email: {Email}, UserId: {UserId}, IP: {IpAddress}, Time: {Time}",
            email, userId, ipAddress ?? "Unknown", DateTime.UtcNow);
    }

    public void LogLoginFailure(string email, string reason, string? ipAddress = null)
    {
        _logger.LogWarning(
            "SECURITY: Login failed - Email: {Email}, Reason: {Reason}, IP: {IpAddress}, Time: {Time}",
            email, reason, ipAddress ?? "Unknown", DateTime.UtcNow);
    }

    public void LogPasswordResetRequested(string email, string? ipAddress = null)
    {
        _logger.LogInformation(
            "SECURITY: Password reset requested - Email: {Email}, IP: {IpAddress}, Time: {Time}",
            email, ipAddress ?? "Unknown", DateTime.UtcNow);
    }

    public void LogPasswordResetCompleted(string email, string? ipAddress = null)
    {
        _logger.LogInformation(
            "SECURITY: Password reset completed - Email: {Email}, IP: {IpAddress}, Time: {Time}",
            email, ipAddress ?? "Unknown", DateTime.UtcNow);
    }

    public void LogTokenRefreshed(string userId, string? ipAddress = null)
    {
        _logger.LogInformation(
            "SECURITY: Token refreshed - UserId: {UserId}, IP: {IpAddress}, Time: {Time}",
            userId, ipAddress ?? "Unknown", DateTime.UtcNow);
    }

    public void LogTokenRevoked(string userId, string? ipAddress = null)
    {
        _logger.LogInformation(
            "SECURITY: Token revoked - UserId: {UserId}, IP: {IpAddress}, Time: {Time}",
            userId, ipAddress ?? "Unknown", DateTime.UtcNow);
    }

    public void LogMfaEnabled(string userId, string? ipAddress = null)
    {
        _logger.LogInformation(
            "SECURITY: MFA enabled - UserId: {UserId}, IP: {IpAddress}, Time: {Time}",
            userId, ipAddress ?? "Unknown", DateTime.UtcNow);
    }

    public void LogMfaDisabled(string userId, string? ipAddress = null)
    {
        _logger.LogWarning(
            "SECURITY: MFA disabled - UserId: {UserId}, IP: {IpAddress}, Time: {Time}",
            userId, ipAddress ?? "Unknown", DateTime.UtcNow);
    }

    public void LogMfaVerified(string userId, bool success, string? ipAddress = null)
    {
        if (success)
        {
            _logger.LogInformation(
                "SECURITY: MFA verification successful - UserId: {UserId}, IP: {IpAddress}, Time: {Time}",
                userId, ipAddress ?? "Unknown", DateTime.UtcNow);
        }
        else
        {
            _logger.LogWarning(
                "SECURITY: MFA verification failed - UserId: {UserId}, IP: {IpAddress}, Time: {Time}",
                userId, ipAddress ?? "Unknown", DateTime.UtcNow);
        }
    }
}
