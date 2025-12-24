namespace Commerce.Application.Features.Auth.DTOs;

/// <summary>
/// Response containing QR code and manual setup key for MFA
/// </summary>
public class EnableMfaResponse
{
    /// <summary>
    /// QR code data URL (can be displayed as image in frontend)
    /// </summary>
    public string QrCodeDataUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Manual entry key for users who can't scan QR code
    /// </summary>
    public string ManualEntryKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Instructions for the user
    /// </summary>
    public string Instructions { get; set; } = "Scan the QR code with your authenticator app (Google Authenticator, Microsoft Authenticator, etc.) or enter the manual key. Then verify with a code to complete setup.";
}
