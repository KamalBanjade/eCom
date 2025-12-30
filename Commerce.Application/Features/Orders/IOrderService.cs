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
    
    // Admin-specific methods
    Task<ApiResponse<OrderDto>> AssignOrderAsync(Guid orderId, Guid assignedToUserId, string assignedRole, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> GetPendingPaymentOrdersAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> GetOrdersWithReturnsAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
}
