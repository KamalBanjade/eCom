// Commerce.Application/Features/Users/DTOs/UserProfileDto.cs
using Commerce.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace Commerce.Application.Features.Users.DTOs;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public List<Address> ShippingAddresses { get; set; } = new();
    public List<Address> BillingAddresses { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateProfileRequest
{
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Phone]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
}

public class AddAddressRequest
{
    [Required]
    public Address Address { get; set; } = null!;
    
    public bool IsShipping { get; set; }
    public bool IsBilling { get; set; }
}

public class UpdateAddressRequest
{
    [Required]
    public AddressType Type { get; set; }
    
    [Required]
    public int Index { get; set; }
    
    [Required]
    public Address Address { get; set; } = null!;
}

public class RemoveAddressRequest
{
    [Required]
    public AddressType Type { get; set; }
    
    [Required]
    public int Index { get; set; }
}

public enum AddressType
{
    Shipping = 1,
    Billing = 2
}