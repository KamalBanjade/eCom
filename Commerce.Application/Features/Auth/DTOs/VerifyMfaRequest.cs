using System.ComponentModel.DataAnnotations;

namespace Commerce.Application.Features.Auth.DTOs;

/// <summary>
/// Request to verify MFA code
/// </summary>
public class VerifyMfaRequest
{
    [Required]
    [StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "MFA code must be 6 digits")]
    public string MfaCode { get; set; } = string.Empty;
}
