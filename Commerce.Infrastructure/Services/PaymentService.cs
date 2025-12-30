using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Payments;
using Commerce.Application.Features.Payments.DTOs;
using Commerce.Application.Features.Orders; // Added for IOrderService
using Commerce.Domain.Entities.Payments;
using Commerce.Domain.Enums;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly CommerceDbContext _context;
    private readonly ILogger<PaymentService> _logger;
    private readonly IOrderService _orderService; // To reuse confirmation logic if possible

    public PaymentService(CommerceDbContext context, ILogger<PaymentService> logger, IOrderService orderService)
    {
        _context = context;
        _logger = logger;
        _orderService = orderService;
    }

    public async Task<PagedResult<PaymentDto>> GetAllPaymentsAsync(PaymentFilterRequest filter, CancellationToken cancellationToken = default)
    {
        // Query Orders primarily as they represent the "Payment" status in this system currently
        // Alternatively, query PaymentAuditLogs for history, but Orders gives current state.
        
        var query = _context.Orders
            .Include(o => o.CustomerProfile)
            .AsNoTracking()
            .AsQueryable();

        if (filter.PaymentMethod.HasValue)
            query = query.Where(o => o.PaymentMethod == filter.PaymentMethod.Value);

        if (filter.PaymentStatus.HasValue)
            query = query.Where(o => o.PaymentStatus == filter.PaymentStatus.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(o => o.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(o => o.CreatedAt <= filter.ToDate.Value);

        if (!string.IsNullOrEmpty(filter.SearchTerm))
            query = query.Where(o => o.OrderNumber.Contains(filter.SearchTerm) || o.Pidx != null && o.Pidx.Contains(filter.SearchTerm));

        var totalCount = await query.CountAsync(cancellationToken);
        
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(o => new PaymentDto
        {
            OrderId = o.Id,
            OrderNumber = o.OrderNumber,
            Amount = o.TotalAmount,
            PaymentMethod = o.PaymentMethod.ToString(),
            PaymentStatus = o.PaymentStatus.ToString(),
            TransactionId = o.Pidx,
            PaidAt = o.ConfirmedAt ?? o.CreatedAt, // Approx
            CustomerEmail = o.CustomerProfile?.Email ?? "N/A",
            CustomerName = o.CustomerProfile?.FullName ?? "N/A"
        }).ToList();
        
        return new PagedResult<PaymentDto>(dtos, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<PaymentDto?> GetPaymentByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.CustomerProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            
        if (order == null) return null;

        return new PaymentDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Amount = order.TotalAmount,
            PaymentMethod = order.PaymentMethod.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            TransactionId = order.Pidx,
            PaidAt = order.ConfirmedAt ?? order.CreatedAt,
            CustomerEmail = order.CustomerProfile?.Email ?? "N/A",
            CustomerName = order.CustomerProfile?.FullName ?? "N/A"
        };
    }

    public async Task<ApiResponse<bool>> ManuallyVerifyPaymentAsync(Guid orderId, VerifyPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders.FindAsync(new object[] { orderId }, cancellationToken);
        if (order == null) return ApiResponse<bool>.ErrorResponse("Order not found");

        if (order.PaymentStatus == PaymentStatus.Completed)
             return ApiResponse<bool>.SuccessResponse(true, "Order is already paid");

        // Manual Verification Logic
        // We update status to Completed and log it
        
        order.PaymentStatus = PaymentStatus.Completed;
        order.OrderStatus = OrderStatus.Processing; // Auto-advance? Requirement says "trigger verification".
        order.ConfirmedAt = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(request.TransactionId))
        {
            // Update PIDX if provided manually
            order.Pidx = request.TransactionId; 
        }

        // Log to Audit
        _context.PaymentAuditLogs.Add(new PaymentAuditLog
        {
            OrderId = order.Id,
            Pidx = order.Pidx ?? "MANUAL-VERIFY",
            Status = "Verified-Manual",
            RawResponse = $"Manual verification by Admin. Remarks: {request.Remarks}",
            CheckedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        
        return ApiResponse<bool>.SuccessResponse(true, "Payment verified manually");
    }
}
