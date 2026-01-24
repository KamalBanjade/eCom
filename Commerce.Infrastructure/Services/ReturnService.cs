using Commerce.Application.Common.DTOs;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Returns.DTOs;
using Commerce.Domain.Entities.Sales;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Entities.Payments;
using Commerce.Domain.Enums;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Commerce.Infrastructure.Services;

public class ReturnService : IReturnService
{
    private readonly CommerceDbContext _context;
    private readonly ILogger<ReturnService> _logger;
    private readonly Application.Features.Inventory.IInventoryService _inventoryService;
    private readonly IEmailService _emailService;
    private readonly Microsoft.AspNetCore.Identity.UserManager<Commerce.Infrastructure.Identity.ApplicationUser> _userManager;
    private readonly IKhaltiPaymentService _khaltiPaymentService;

    public ReturnService(
        CommerceDbContext context, 
        ILogger<ReturnService> logger,
        Application.Features.Inventory.IInventoryService inventoryService,
        IEmailService emailService,
        Microsoft.AspNetCore.Identity.UserManager<Commerce.Infrastructure.Identity.ApplicationUser> userManager,
        IKhaltiPaymentService khaltiPaymentService)
    {
        _context = context;
        _logger = logger;
        _inventoryService = inventoryService;
        _emailService = emailService;
        _userManager = userManager;
        _khaltiPaymentService = khaltiPaymentService;
    }

    public async Task<ApiResponse<ReturnRequest>> RequestReturnAsync(
        Guid orderId, 
        List<CreateReturnItemDto> items,
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.CustomerProfile)
            .Include(o => o.Items)
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

        // Check for pending returns for this order to calculate available quantity
        var pendingReturns = await _context.Returns
            .Include(r => r.Items)
            .Where(r => r.OrderId == orderId && 
                        r.ReturnStatus != ReturnStatus.Rejected && 
                        r.ReturnStatus != ReturnStatus.Refunded)
            .ToListAsync(cancellationToken);

        // Validations
        foreach (var item in items)
        {
            var orderItem = order.Items.FirstOrDefault(oi => oi.Id == item.OrderItemId);
            if (orderItem == null) 
                 return ApiResponse<ReturnRequest>.ErrorResponse($"Order item {item.OrderItemId} not found");

            int pendingQty = pendingReturns
                .SelectMany(r => r.Items)
                .Where(ri => ri.OrderItemId == item.OrderItemId)
                .Sum(ri => ri.Quantity);

            int availableQty = orderItem.Quantity - orderItem.ReturnedQuantity - pendingQty;

            if (item.Quantity > availableQty)
                return ApiResponse<ReturnRequest>.ErrorResponse($"Cannot return {item.Quantity} of {orderItem.ProductName}. " +
                    $"Purchased: {orderItem.Quantity}, Already Returned: {orderItem.ReturnedQuantity}, " +
                    $"Pending Return: {pendingQty}, Available: {availableQty}");
        }

        // AUTOMATION: Pre-fetch Support user (before creating return)
        var supportUsers = await _userManager.GetUsersInRoleAsync("Support");
        Guid? supportUserIdToAssign = null;
        string? supportEmailToNotify = null;

        if (supportUsers.Any())
        {
            var supportUser = supportUsers.First(); 
            if (Guid.TryParse(supportUser.Id, out var userGuid))
            {
                supportUserIdToAssign = userGuid;
                supportEmailToNotify = supportUser.Email;
            }
        }

        var returnRequest = new ReturnRequest
        {
            OrderId = orderId,
            ReturnStatus = ReturnStatus.Requested, // Overall status
            RequestedAt = DateTime.UtcNow,
            Order = order,
            Items = new List<ReturnItem>()
        };

        foreach (var itemDto in items)
        {
            var orderItem = order.Items.First(oi => oi.Id == itemDto.OrderItemId);
            returnRequest.Items.Add(new ReturnItem
            {
                OrderItemId = itemDto.OrderItemId,
                Quantity = itemDto.Quantity,
                Reason = itemDto.Reason,
                Status = ReturnItemStatus.Requested,
                UnitPrice = orderItem.EffectivePrice ?? orderItem.UnitPrice, // Use effective price if available
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Auto-assign to Support
        if (supportUserIdToAssign.HasValue)
        {
            returnRequest.AssignedToUserId = supportUserIdToAssign.Value;
            returnRequest.AssignedRole = "Support";
            returnRequest.AssignedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Return {ReturnId} auto-assigned to Support user {UserId} ({Email})", 
                returnRequest.Id, supportUserIdToAssign, supportEmailToNotify);
        }

        _context.Returns.Add(returnRequest);
        await _context.SaveChangesAsync(cancellationToken);

        // Fire-and-forget email notification
        if (!string.IsNullOrEmpty(supportEmailToNotify))
        {
            _ = _emailService.SendReturnNotificationToSupportAsync(returnRequest, supportEmailToNotify);
        }

        return ApiResponse<ReturnRequest>.SuccessResponse(returnRequest, "Return requested successfully");
    }

    public async Task<IEnumerable<ReturnRequest>> GetUserReturnsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Returns
            .Include(r => r.Order)
            .Include(r => r.Items)
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

    public async Task<ApiResponse<ReturnRequest>> MarkPickedUpAsync(
        Guid returnId, 
        CancellationToken cancellationToken = default)
    {
        var request = await _context.Returns
            .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);

        if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

        if (request.ReturnStatus != ReturnStatus.Approved)
             return ApiResponse<ReturnRequest>.ErrorResponse($"Cannot mark as Picked Up in status {request.ReturnStatus}. Must be Approved first.");

        try 
        {
            request.ReturnStatus = ReturnStatus.PickedUp;
            request.PickedUpAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<ReturnRequest>.SuccessResponse(request, "Return marked as Picked Up.");
        }
        catch (Exception ex)
        {
            return ApiResponse<ReturnRequest>.ErrorResponse($"Mark Picked Up Failed: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ReturnRequest>> MarkReceivedAsync(
        Guid returnId, 
        CancellationToken cancellationToken = default)
    {
        var request = await _context.Returns
            .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);

        if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

        if (request.ReturnStatus != ReturnStatus.PickedUp)
             return ApiResponse<ReturnRequest>.ErrorResponse($"Cannot mark as Received in status {request.ReturnStatus}. Must be Picked Up first.");

        try 
        {
            request.ReturnStatus = ReturnStatus.Received;
            request.ReceivedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<ReturnRequest>.SuccessResponse(request, "Return marked as Received at Warehouse.");
        }
        catch (Exception ex)
        {
            return ApiResponse<ReturnRequest>.ErrorResponse($"Mark Received Failed: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ReturnRequest>> CompleteInspectionAsync(
        Guid returnId, 
        CancellationToken cancellationToken = default)
    {
        var request = await _context.Returns
            .Include(r => r.Items)
            .Include(r => r.Order)
            .ThenInclude(o => o.CustomerProfile)
            .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);

        if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

        if (request.ReturnStatus != ReturnStatus.Received)
             return ApiResponse<ReturnRequest>.ErrorResponse($"Cannot complete inspection in status {request.ReturnStatus}. Must be Received first.");

        try 
        {
            request.ReturnStatus = ReturnStatus.InspectionCompleted;
            request.InspectionCompletedAt = DateTime.UtcNow;

            // RESTOCK ITEMS
            foreach(var item in request.Items)
            {
                 var orderItem = request.Order.Items.FirstOrDefault(oi => oi.Id == item.OrderItemId);
                 if (orderItem == null) continue;

                 // Generic Adjustment handles restock/locking/auditing
                 await _inventoryService.AdjustStockAsync(
                     orderItem.ProductVariantId,
                     item.Quantity, 
                     $"Return Inspection #{request.Order.OrderNumber}", 
                     request.Order.CustomerProfile?.ApplicationUserId, 
                     cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<ReturnRequest>.SuccessResponse(request, "Inspection completed and items restocked.");
        }
        catch (Exception ex)
        {
            return ApiResponse<ReturnRequest>.ErrorResponse($"Complete Inspection Failed: {ex.Message}");
        }
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
                    .ThenInclude(o => o.CustomerProfile)
                .Include(r => r.Items)
                    .ThenInclude(i => i.OrderItem)
                .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);
                
            if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

            if (request.ReturnStatus != ReturnStatus.InspectionCompleted)
            return ApiResponse<ReturnRequest>.ErrorResponse("Return must be Inspection Completed before refund.");

            // Update Return details
            request.ReturnStatus = ReturnStatus.Refunded;
            request.RefundMethod = method;
            request.TotalRefundAmount = amount;
            request.RefundedAt = DateTime.UtcNow;

            // Increment ReturnedQuantity for each item tracked in this return request
            foreach (var item in request.Items)
            {
                var orderItem = request.Order.Items.FirstOrDefault(oi => oi.Id == item.OrderItemId);
                if (orderItem != null)
                {
                    orderItem.ReturnedQuantity += item.Quantity;
                }
            }

            // Update Order Payment Status
            // Only update order status to Returned if all items are fully returned
            bool allItemsRefunded = request.Order.Items.All(oi => oi.ReturnedQuantity == oi.Quantity);
            if (allItemsRefunded)
            {
                request.Order.OrderStatus = OrderStatus.Returned;
                request.Order.ReturnedAt = DateTime.UtcNow;
            }
            
            request.Order.PaymentStatus = PaymentStatus.Refunded;
            request.Order.RefundedAt = DateTime.UtcNow;

            if (method == RefundMethod.Khalti)
            {
                 // Verify original order used Khalti
                 if (request.Order.PaymentMethod != PaymentMethod.Khalti)
                    throw new InvalidOperationException("Cannot refund via Khalti for non-Khalti order");
                 
                 if (string.IsNullOrEmpty(request.Order.Pidx))
                     throw new InvalidOperationException("Order does not have a valid Khalti Pidx");

                 // Store Pidx for reference
                 request.KhaltiPidx = request.Order.Pidx;

                 // SAFETY LOG: Intent to refund
                 _logger.LogInformation("Attempting Khalti Refund for Return {ReturnId}, Pidx {Pidx}, Amount {Amount}", 
                    request.Id, request.Order.Pidx, amount);

                 // DIAGNOSTIC CONSOLE LOG
                 Console.WriteLine("==========================================================");
                 Console.WriteLine("[KHALTI REFUND DETAILS]");
                 Console.WriteLine($"Return ID: {request.Id}");
                 Console.WriteLine($"Order ID: {request.OrderId}");
                 Console.WriteLine($"Order Number: {request.Order.OrderNumber}");
                 Console.WriteLine($"Order Pidx: {request.Order.Pidx}");
                 Console.WriteLine($"Refund Amount (NPR): {amount}");
                 Console.WriteLine($"Refund Amount (Paisa): {(long)(amount * 100)}");
                 Console.WriteLine("WARNING: Passing 'Pidx' to RefundPaymentAsync - should be TransactionId!");
                 Console.WriteLine("==========================================================");

                 try
                 {
                     // STEP 1: Lookup TransactionId using Pidx
                     Console.WriteLine("[STEP 1: LOOKUP TRANSACTION ID]");
                     Console.WriteLine($"Calling Khalti Lookup API with Pidx: {request.Order.Pidx}");
                     
                     var lookupResponse = await _khaltiPaymentService.LookupPaymentAsync(
                         request.Order.Pidx, 
                         cancellationToken);
                     
                     if (string.IsNullOrEmpty(lookupResponse.TransactionId))
                     {
                         throw new InvalidOperationException($"Khalti lookup did not return TransactionId for Pidx: {request.Order.Pidx}");
                     }
                     
                     Console.WriteLine($"✓ TransactionId retrieved: {lookupResponse.TransactionId}");
                     Console.WriteLine($"Payment Status: {lookupResponse.Status}");
                     Console.WriteLine($"Total Amount: {lookupResponse.TotalAmount} paisa");
                     Console.WriteLine("==========================================================");
                     
                     // STEP 2: CALL KHALTI REFUND API with TransactionId
                     long amountInPaisa = (long)(amount * 100);
                     
                     Console.WriteLine("[STEP 2: CALLING KHALTI REFUND API]");
                     Console.WriteLine($"Using TransactionId (correct): {lookupResponse.TransactionId}");
                     Console.WriteLine($"Amount in paisa: {amountInPaisa}");
                     
                     var khaltiResponse = await _khaltiPaymentService.RefundPaymentAsync(
                         lookupResponse.TransactionId, // ✓ CORRECT: Using TransactionId, not Pidx
                         amountInPaisa, 
                         null, // mobile - for wallet refund
                         cancellationToken);
                     
                     Console.WriteLine("[KHALTI API RESPONSE]");
                     Console.WriteLine($"Success: true");
                     Console.WriteLine($"Message: {khaltiResponse.Message}");
                     Console.WriteLine("==========================================================");
                     
                     // Log success details
                     _context.PaymentAuditLogs.Add(new PaymentAuditLog
                     {
                        OrderId = request.OrderId,
                        Pidx = khaltiResponse.TransactionId ?? request.Order.Pidx, // Use new txn id if available
                        Status = "Refunded (Gateway)",
                        RawResponse = $"Khalti Refund Success: {khaltiResponse.Message}",
                        CheckedAt = DateTime.UtcNow
                     });

                     // SEND NOTIFICATION EMAIL TO CUSTOMER
                     if (request.Order?.CustomerProfile != null && !string.IsNullOrEmpty(request.Order.CustomerProfile.Email))
                     {
                         try
                         {
                             await _emailService.SendRefundConfirmationEmailAsync(request, request.Order.CustomerProfile.Email, cancellationToken);
                             _logger.LogInformation("Refund confirmation email sent for Order {OrderNumber}", request.Order.OrderNumber);
                         }
                         catch (Exception ex)
                         {
                             _logger.LogWarning(ex, "Failed to send refund confirmation email for Order {OrderNumber}. Refund was successful though.", request.Order.OrderNumber);
                         }
                     }
                 }
                 catch (Exception kEx)
                 {
                     _logger.LogError(kEx, "Khalti Refund API Failed for Return {ReturnId}. Rolling back.", request.Id);
                     throw; // Re-throw to trigger transaction rollback
                 }
            }
            else
            {
                // Manual Refund Audit
                _context.PaymentAuditLogs.Add(new PaymentAuditLog
                {
                    OrderId = request.OrderId,
                    Pidx = "MANUAL-REFUND",
                    Status = "Refunded (Manual)",
                    RawResponse = $"Refund via {method}: {amount}",
                    CheckedAt = DateTime.UtcNow
                });
            }

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


    // ==========================================
    // Admin Methods
    // ==========================================

    public async Task<PagedResult<ReturnRequestDto>> GetAllReturnsAsync(ReturnFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Returns
            .Include(r => r.Order)
            .ThenInclude(o => o.CustomerProfile)
            .Include(r => r.Order)
            .ThenInclude(o => o.Items)
            .Include(r => r.Items)
            .AsNoTracking()
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(r => r.ReturnStatus == filter.Status.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.RequestedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(r => r.RequestedAt <= filter.ToDate.Value);

        if (filter.CustomerId.HasValue)
            query = query.Where(r => r.Order.CustomerProfileId == filter.CustomerId.Value);

        if (filter.AssignedToUserId.HasValue)
            query = query.Where(r => r.AssignedToUserId == filter.AssignedToUserId.Value);

        if (!string.IsNullOrEmpty(filter.OrderNumber))
            query = query.Where(r => r.Order.OrderNumber.Contains(filter.OrderNumber));

        if (!string.IsNullOrEmpty(filter.CustomerSearch))
        {
            query = query.Where(r => 
                r.Order.CustomerProfile.FullName.Contains(filter.CustomerSearch) || 
                r.Order.CustomerProfile.Email.Contains(filter.CustomerSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var returns = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        // Pre-fetch assigned users
        var assignedUserIds = returns
            .Where(r => r.AssignedToUserId.HasValue)
            .Select(r => r.AssignedToUserId.Value.ToString())
            .Distinct()
            .ToList();

        var userDict = new Dictionary<string, string>();
        foreach (var uid in assignedUserIds)
        {
            var u = await _userManager.FindByIdAsync(uid);
            if (u?.Email != null) userDict[uid] = u.Email;
        }
        
        var dtos = returns.Select(r => MapToDto(r, userDict)).ToList();
        
        return new PagedResult<ReturnRequestDto>(dtos, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<ReturnRequestDto?> GetReturnByIdAsync(Guid returnId, CancellationToken cancellationToken = default)
    {
        var returnRequest = await _context.Returns
            .Include(r => r.Order)
            .ThenInclude(o => o.CustomerProfile)
            .Include(r => r.Order)
            .ThenInclude(o => o.Items)
            .Include(r => r.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);

        return returnRequest == null ? null : MapToDto(returnRequest);
    }

    public async Task<ApiResponse<ReturnRequest>> AssignReturnAsync(Guid returnId, Guid assignedToUserId, CancellationToken cancellationToken = default)
    {
        var request = await _context.Returns.FindAsync(new object[] { returnId }, cancellationToken);
        if (request == null) return ApiResponse<ReturnRequest>.ErrorResponse("Return request not found");

        request.AssignedToUserId = assignedToUserId;
        request.AssignedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<ReturnRequest>.SuccessResponse(request, "Return assigned successfully");
    }

    private ReturnRequestDto MapToDto(ReturnRequest request, Dictionary<string, string>? userDict = null)
    {
        string? assignedEmail = null;
        if (request.AssignedToUserId.HasValue)
        {
            var uid = request.AssignedToUserId.Value.ToString();
            if (userDict != null && userDict.TryGetValue(uid, out var email))
            {
                assignedEmail = email;
            }
            else
            {
                // Fallback direct lookup
                try 
                {
                    var u = _userManager.FindByIdAsync(uid).Result;
                    assignedEmail = u?.Email;
                }
                catch { /* Ignore */ }
            }
        }

        return new ReturnRequestDto
        {
            Id = request.Id,
            OrderId = request.OrderId,
            OrderNumber = request.Order?.OrderNumber ?? "N/A",
            OrderPaymentMethod = request.Order?.PaymentMethod.ToString() ?? "Unknown",
            OrderTotalAmount = request.Order?.TotalAmount ?? 0,
            OrderDiscountAmount = request.Order?.DiscountAmount ?? 0,
            Reason = string.Join(", ", request.Items?.Select(i => i.Reason) ?? Enumerable.Empty<string>()),
            ReturnStatus = request.ReturnStatus.ToString(),
            RefundAmount = request.TotalRefundAmount,
            RefundMethod = request.RefundMethod?.ToString(),
            KhaltiPidx = request.KhaltiPidx,
            
            AssignedToUserId = request.AssignedToUserId,
            AssignedRole = request.AssignedRole,
            AssignedToUserEmail = assignedEmail,
            AssignedAt = request.AssignedAt,
            
            RequestedAt = request.RequestedAt,
            ApprovedAt = request.ApprovedAt,
            PickedUpAt = request.PickedUpAt,
            ReceivedAt = request.ReceivedAt,
            InspectionCompletedAt = request.InspectionCompletedAt,
            ProcessingAt = request.Order?.ProcessingAt,
            RefundedAt = request.RefundedAt,
            
            CustomerEmail = request.Order?.CustomerProfile?.Email ?? "", 
            CustomerName = request.Order?.CustomerProfile?.FullName ?? "",
            
            Items = (request.Items ?? new List<ReturnItem>()).Select(ri => 
            {
                var originalItem = request.Order?.Items?.FirstOrDefault(oi => oi.Id == ri.OrderItemId);
                return new ReturnItemDto
                {
                    ReturnItemId = ri.Id,
                    OrderItemId = ri.OrderItemId,
                    Status = ri.Status.ToString(),
                    Reason = ri.Reason,
                    Condition = ri.Condition,
                    AdminNotes = ri.AdminNotes,
                    IsRestocked = ri.IsRestocked,
                    ReceivedAt = ri.ReceivedAt,
                    RefundAmount = CalculateItemRefundAmount(ri, originalItem),
                    
                    // Merged Order Item Details
                    Id = originalItem?.Id ?? Guid.Empty, // Keeps compat with FE expecting OrderItemId as Id?
                    ProductVariantId = originalItem?.ProductVariantId ?? Guid.Empty,
                    ProductName = originalItem?.ProductName ?? "Unknown",
                    VariantName = originalItem?.VariantName,
                    Quantity = ri.Quantity, // Return Quantity
                    UnitPrice = ri.UnitPrice,
                    SubTotal = ri.SubTotal,
                    
                    // New fields from base OrderItemDto
                    DiscountAllocated = originalItem?.DiscountAllocated,
                    DiscountPerUnit = originalItem?.DiscountPerUnit,
                    EffectivePrice = originalItem?.EffectivePrice,
                    DiscountDistributionPercentage = originalItem?.DiscountDistributionPercentage,
                    ReturnedQuantity = originalItem?.ReturnedQuantity ?? 0
                };
            }).ToList()
        };
    }

    private decimal CalculateItemRefundAmount(ReturnItem returnItem, OrderItem? orderItem)
    {
        if (orderItem == null)
        {
            _logger.LogWarning("OrderItem not found for ReturnItem {ReturnItemId}", returnItem.Id);
            return returnItem.Quantity * returnItem.UnitPrice;
        }
        
        // If discount was distributed, use effective price
        if (orderItem.EffectivePrice.HasValue)
        {
            var refundAmount = returnItem.Quantity * orderItem.EffectivePrice.Value;
            _logger.LogInformation("Refund calculated with effective price: {RefundAmount} " +
                "(Qty: {Quantity} × EffectivePrice: {EffectivePrice})", 
                refundAmount, returnItem.Quantity, orderItem.EffectivePrice.Value);
            return refundAmount;
        }
        
        // Fallback for old orders without discount distribution
        var fallbackAmount = returnItem.Quantity * returnItem.UnitPrice;
        _logger.LogInformation("Refund calculated with unit price (no discount distribution): {RefundAmount}", 
            fallbackAmount);
        return fallbackAmount;
    }
}

