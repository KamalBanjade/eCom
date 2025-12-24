using System.ComponentModel.DataAnnotations;

namespace Commerce.Application.Features.Auth.DTOs;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
