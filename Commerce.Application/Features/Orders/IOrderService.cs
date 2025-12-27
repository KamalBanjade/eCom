using Commerce.Application.Features.Orders.DTOs;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Enums;
using Commerce.Application.Common.DTOs;

namespace Commerce.Application.Features.Orders;

public interface IOrderService
{
    Task<ApiResponse<OrderDto>> PlaceOrderAsync(Guid applicationUserId, PlaceOrderRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<OrderDto>> ConfirmPaymentAsync(string pidx, CancellationToken cancellationToken = default);
    Task<ApiResponse<OrderDto>> CancelOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid applicationUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(Guid applicationUserId, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> GetOrdersAsync(OrderFilterRequest filter, CancellationToken cancellationToken = default);
    Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken cancellationToken = default);
}
