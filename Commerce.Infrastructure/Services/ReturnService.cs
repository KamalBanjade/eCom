using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Domain.Entities.Sales;
using Commerce.Domain.Entities.Payments;
using Commerce.Domain.Enums;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Infrastructure.Services;

public class ReturnService : IReturnService
{
    private readonly CommerceDbContext _context;
    private readonly ILogger<ReturnService> _logger;

    public ReturnService(CommerceDbContext context, ILogger<ReturnService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<ReturnRequest>> RequestReturnAsync(
        Guid orderId, 
        string reason, 
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.CustomerProfile)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            return ApiResponse<ReturnRequest>.ErrorResponse("Order not found");

        if (order.CustomerProfile.ApplicationUserId != userId.ToString())
            return ApiResponse<ReturnRequest>.ErrorResponse("Unauthorized to return this order");

        if (order.OrderStatus != OrderStatus.Delivered)
            return ApiResponse<ReturnRequest>.ErrorResponse("Only delivered orders can be returned");

        // Rule: 7-day return window
        if (order.DeliveredAt.HasValue && (DateTime.UtcNow - order.DeliveredAt.Value).TotalDays > 7)
            return ApiResponse<ReturnRequest>.ErrorResponse("Return window (7 days) has expired");

        // Check existing return
        var existingReturn = await _context.Returns
            .FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
        
        if (existingReturn != null)
            return ApiResponse<ReturnRequest>.ErrorResponse("Return already requested for this order");

        var returnRequest = new ReturnRequest
        {
            OrderId = orderId,
            Reason = reason,
            ReturnStatus = ReturnStatus.Requested,
            RequestedAt = DateTime.UtcNow
        };

        _context.Returns.Add(returnRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<ReturnRequest>.SuccessResponse(returnRequest, "Return requested successfully");
    }

    public async Task<IEnumerable<ReturnRequest>> GetUserReturnsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Returns
            .Include(r => r.Order)
            .Where(r => r.Order.CustomerProfile.ApplicationUserId == userId.ToString())
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiResponse<ReturnRequest>> ApproveReturnAsync(
        Guid returnId, 
        CancellationToken cancellationToken = default)
    {
        var request = await _context.Returns.FindAsync(new object[] { returnId }, cancellationToken);
        if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

        if (request.ReturnStatus != ReturnStatus.Requested)
            return ApiResponse<ReturnRequest>.ErrorResponse($"Cannot approve return in status {request.ReturnStatus}");

        request.ReturnStatus = ReturnStatus.Approved;
        request.ApprovedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<ReturnRequest>.SuccessResponse(request, "Return Approved. Waiting for item.");
    }

    public async Task<ApiResponse<ReturnRequest>> RejectReturnAsync(
        Guid returnId, 
        CancellationToken cancellationToken = default)
    {
        var request = await _context.Returns.FindAsync(new object[] { returnId }, cancellationToken);
        if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

        if (request.ReturnStatus != ReturnStatus.Requested)
             return ApiResponse<ReturnRequest>.ErrorResponse($"Cannot reject return in status {request.ReturnStatus}");

        request.ReturnStatus = ReturnStatus.Rejected;
        // No RejectedAt timestamp in entity, assume processed
        
        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<ReturnRequest>.SuccessResponse(request, "Return Rejected");
    }

    public async Task<ApiResponse<ReturnRequest>> MarkReceivedAsync(
        Guid returnId, 
        CancellationToken cancellationToken = default)
    {
        var request = await _context.Returns.FindAsync(new object[] { returnId }, cancellationToken);
        if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

        if (request.ReturnStatus != ReturnStatus.Approved)
             return ApiResponse<ReturnRequest>.ErrorResponse($"Cannot receive item in status {request.ReturnStatus}. Must be Approved first.");

        request.ReturnStatus = ReturnStatus.Received;
        request.ReceivedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<ReturnRequest>.SuccessResponse(request, "Item marked as Received. Ready for Refund.");
    }

    public async Task<ApiResponse<ReturnRequest>> ProcessRefundAsync(
        Guid returnId, 
        RefundMethod method, 
        decimal amount, 
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try 
        {
            var request = await _context.Returns
                .Include(r => r.Order)
                .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);
                
            if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

            if (request.ReturnStatus != ReturnStatus.Received)
                 return ApiResponse<ReturnRequest>.ErrorResponse("Cannot process refund. Item must be Received first.");

            // Update Return details
            request.ReturnStatus = ReturnStatus.Refunded;
            request.RefundMethod = method;
            request.RefundAmount = amount;
            request.RefundedAt = DateTime.UtcNow;

            // Update Order Payment Status
            request.Order.OrderStatus = OrderStatus.Returned;
            request.Order.PaymentStatus = PaymentStatus.Refunded;

            if (method == RefundMethod.Khalti)
            {
                 // Verify original order used Khalti
                 if (request.Order.PaymentMethod != PaymentMethod.Khalti)
                    throw new InvalidOperationException("Cannot refund via Khalti for non-Khalti order");
                 
                 // Store Pidx for reference (usually same as order, or a new refund transaction ID if Khalti API supported it)
                 request.KhaltiPidx = request.Order.Pidx;
            }

            // Log Audit (Using PaymentAuditLog)
            _context.PaymentAuditLogs.Add(new PaymentAuditLog
            {
                OrderId = request.OrderId,
                Pidx = request.KhaltiPidx ?? "MANUAL-REFUND",
                Status = "Refunded",
                RawResponse = $"Refund via {method}: {amount}",
                CheckedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return ApiResponse<ReturnRequest>.SuccessResponse(request, "Refund processed successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Refund failed for Return {ReturnId}", returnId);
            return ApiResponse<ReturnRequest>.ErrorResponse($"Refund failed: {ex.Message}");
        }
    }
}
