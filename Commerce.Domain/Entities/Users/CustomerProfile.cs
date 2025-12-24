using Commerce.Domain.Entities.Base;
using Commerce.Domain.ValueObjects;

namespace Commerce.Domain.Entities.Users;

public class CustomerProfile : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    
    // Link to ASP.NET Identity user
    public string ApplicationUserId { get; set; } = string.Empty;
    
    // Addresses stored as JSON
    public List<Address> ShippingAddresses { get; set; } = new();
    public List<Address> BillingAddresses { get; set; } = new();
    
    public string FullName => $"{FirstName} {LastName}";
}
