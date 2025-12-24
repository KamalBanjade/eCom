using Commerce.Application.Features.Orders.DTOs;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Enums;
using Commerce.Application.Common.DTOs;

namespace Commerce.Application.Features.Orders;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderDto>> GetAllOrdersAsync(CancellationToken cancellationToken = default);
    Task<OrderDto> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken cancellationToken = default);
}
