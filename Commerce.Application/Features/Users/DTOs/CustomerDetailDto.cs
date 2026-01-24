using Commerce.Application.Features.Orders.DTOs;
using Commerce.Application.Features.Returns.DTOs;

namespace Commerce.Application.Features.Users.DTOs;

public class CustomerDetailDto
{
    public AdminUserDto User { get; set; } = null!;
    public UserProfileDto Profile { get; set; } = null!;
    public List<OrderDto> Orders { get; set; } = new();
    public List<ReturnRequestDto> Returns { get; set; } = new();
}
