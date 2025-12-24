using Commerce.Application.Features.Orders;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Enums;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly CommerceDbContext _context;

    public OrderService(CommerceDbContext context)
    {
        _context = context;
    }

    public async Task<OrderDto> CreateOrderAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Placeholder logic - normally would convert Cart to Order
        throw new NotImplementedException("Order creation logic to be implemented in Checkout Phase");
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .AsQueryable();

        if (!isAdmin)
        {
            // Security check: Users can only see their own orders linked via CustomerProfile
            // Note: This requires resolving CustomerProfileId from UserId, assuming simplified link here
            var userProfile = await _context.CustomerProfiles
                .FirstOrDefaultAsync(p => p.ApplicationUserId == userId.ToString(), cancellationToken);
                
            if (userProfile == null) return null;
            
            query = query.Where(o => o.CustomerProfileId == userProfile.Id);
        }

        var order = await query.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order == null) return null;

        return MapToDto(order);
    }

    public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userProfile = await _context.CustomerProfiles
            .FirstOrDefaultAsync(p => p.ApplicationUserId == userId.ToString(), cancellationToken);
            
        if (userProfile == null) return Enumerable.Empty<OrderDto>();

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerProfileId == userProfile.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDto);
    }

    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDto);
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null) throw new KeyNotFoundException("Order not found");

        order.OrderStatus = status;
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(order);
    }

    private static OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductVariantId = i.ProductVariantId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                ProductName = "Product Info Placeholder" // would need join to get name
            }).ToList()
        };
    }
}
