using Commerce.Application.Features.Orders;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Application.Features.Carts;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Payments.DTOs;
using Commerce.Domain.Entities.Orders;
using Commerce.Domain.Entities.Payments;
using Commerce.Domain.Enums;
using Commerce.Domain.ValueObjects;
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Commerce.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Commerce.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly CommerceDbContext _context;
    private readonly ICartService _cartService;
    private readonly ICouponService _couponService;
    private readonly IKhaltiPaymentService _khaltiPaymentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly KhaltiSettings _khaltiSettings;

    public OrderService(
        CommerceDbContext context, 
        ICartService cartService,
        ICouponService couponService,
        IKhaltiPaymentService khaltiPaymentService,
        UserManager<ApplicationUser> userManager,
        IOptions<KhaltiSettings> khaltiSettings)
    {
        _context = context;
        _cartService = cartService;
        _couponService = couponService;
        _khaltiPaymentService = khaltiPaymentService;
        _userManager = userManager;
        _khaltiSettings = khaltiSettings.Value;
    }

    public async Task<ApiResponse<OrderDto>> PlaceOrderAsync(
        Guid applicationUserId, 
        PlaceOrderRequest request, 
        CancellationToken cancellationToken = default)
    {
        // 1. Validate user and get customer profile
        var user = await _userManager.FindByIdAsync(applicationUserId.ToString());
        if (user?.CustomerProfileId == null)
            return ApiResponse<OrderDto>.ErrorResponse("User profile not found");

       var customerProfile = await _context.CustomerProfiles
            // Addresses are JSON columns, auto-loaded. No Include needed.
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.Id == user.CustomerProfileId.Value, cancellationToken);

        if (customerProfile == null)
            return ApiResponse<OrderDto>.ErrorResponse("Customer profile not found");
            
        // Now check the counts properly
        if (request.ShippingAddressIndex < 0 || request.ShippingAddressIndex >= customerProfile.ShippingAddresses.Count)
            return ApiResponse<OrderDto>.ErrorResponse("Invalid shipping address index");

        var shippingAddress = customerProfile.ShippingAddresses[request.ShippingAddressIndex];

        // 3. Fetch billing address (or use shipping if not provided)
        Address billingAddress;
        if (request.BillingAddressIndex.HasValue)
        {
            if (request.BillingAddressIndex.Value < 0 || request.BillingAddressIndex.Value >= customerProfile.BillingAddresses.Count)
                return ApiResponse<OrderDto>.ErrorResponse("Invalid billing address index");
            
            billingAddress = customerProfile.BillingAddresses[request.BillingAddressIndex.Value];
        }
        else
        {
            billingAddress = shippingAddress;
        }

        // 4. Fetch cart snapshot
        var cartResponse = await _cartService.GetCartAsync(applicationUserId, null, cancellationToken);
        if (!cartResponse.Success || cartResponse.Data == null || !cartResponse.Data.Items.Any())
            return ApiResponse<OrderDto>.ErrorResponse("Cart is empty or invalid");

        var cart = cartResponse.Data;

        // 5. Fetch product details for snapshots
        var variantIds = cart.Items.Select(i => i.ProductVariantId).ToList();
        var variants = await _context.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v, cancellationToken);

        // 6. Begin database transaction for atomicity
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // 7. Create Order with immutable snapshot
            var order = new Order
            {
                CustomerProfileId = customerProfile.Id,
                PaymentMethod = request.PaymentMethod,
                CreatedAt = DateTime.UtcNow,
                
                // Pricing from cart (already calculated with coupon)
                SubTotal = cart.Subtotal,
                DiscountAmount = cart.DiscountAmount,
                TaxAmount = 0m, // TODO: Implement tax calculation
                ShippingAmount = 0m, // TODO: Implement shipping calculation
                TotalAmount = cart.Total,
                
                // Coupon snapshot
                AppliedCouponCode = cart.AppliedCoupon,
                
                // Address snapshots
                ShippingAddress = shippingAddress,
                BillingAddress = billingAddress,
                
                // Order items with product snapshots
                Items = cart.Items.Select(ci =>
                {
                    var variant = variants.GetValueOrDefault(ci.ProductVariantId);
                    return new OrderItem
                    {
                        ProductVariantId = ci.ProductVariantId,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.UnitPrice,
                        ProductName = variant?.Product?.Name ?? ci.ProductName,
                        VariantName = ci.VariantName,
                        ProductSnapshot = new Dictionary<string, string>
                        {
                            ["ProductId"] = variant?.ProductId.ToString() ?? "",
                            ["SKU"] = variant?.SKU ?? "",
                            ["ImageUrl"] = ci.ImageUrl ?? ""
                        }
                    };
                }).ToList()
            };

            // 8. Generate Order Number
            order.OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            // 9. BRANCH BY PAYMENT METHOD
            if (request.PaymentMethod == PaymentMethod.CashOnDelivery)
            {
                // COD FLOW: Simple and immediate
                order.OrderStatus = OrderStatus.Confirmed;
                order.PaymentStatus = PaymentStatus.NotRequired;
                
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(cancellationToken);

                // Lock coupon usage
                if (!string.IsNullOrEmpty(cart.AppliedCoupon))
                {
                    var coupon = await _context.Set<Domain.Entities.Sales.Coupon>()
                        .FirstOrDefaultAsync(c => c.Code == cart.AppliedCoupon, cancellationToken);
                    
                    if (coupon != null)
                    {
                        coupon.CurrentUses++;
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }

                await transaction.CommitAsync(cancellationToken);

                // Clear cart immediately for COD
                await _cartService.ClearCartAsync(applicationUserId, null, cancellationToken);

                return ApiResponse<OrderDto>.SuccessResponse(MapToDto(order), "Order placed successfully");
            }
            else if (request.PaymentMethod == PaymentMethod.Khalti)
            {
                // KHALTI FLOW: Initiate payment, wait for confirmation
                order.OrderStatus = OrderStatus.PendingPayment;
                order.PaymentStatus = PaymentStatus.Initiated;
                
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(cancellationToken);

                // Initiate Khalti payment
                var khaltiRequest = new KhaltiInitiateRequest
                {
                    Amount = (long)(order.TotalAmount * 100), // Convert NPR to paisa
                    PurchaseOrderId = order.OrderNumber,
                    PurchaseOrderName = "Order Payment",
                    ReturnUrl = _khaltiSettings.ReturnUrl,
                    WebsiteUrl = _khaltiSettings.WebsiteUrl,
                    CustomerInfo = new KhaltiCustomerInfo
                    {
                        Name = customerProfile.FullName,
                        Email = customerProfile.Email,
                        Phone = customerProfile.PhoneNumber ?? ""
                    }
                };

                var khaltiResponse = await _khaltiPaymentService.InitiatePaymentAsync(khaltiRequest, cancellationToken);

                // Store Khalti details
                order.Pidx = khaltiResponse.Pidx;
                order.PaymentUrl = khaltiResponse.PaymentUrl;
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                // DO NOT clear cart - wait for payment confirmation
                // DO NOT lock coupon - wait for payment confirmation

                var orderDto = MapToDto(order);
                orderDto.PaymentUrl = khaltiResponse.PaymentUrl;  // Include for frontend redirect

                return ApiResponse<OrderDto>.SuccessResponse(orderDto, "Order created. Please complete payment.");
            }
            else
            {
                return ApiResponse<OrderDto>.ErrorResponse("Invalid payment method");
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ApiResponse<OrderDto>.ErrorResponse($"Failed to place order: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDto>> ConfirmPaymentAsync(
        string pidx, 
        CancellationToken cancellationToken = default)
    {
        // 1. Find order by Pidx
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.CustomerProfile)
            .FirstOrDefaultAsync(o => o.Pidx == pidx, cancellationToken);

        if (order == null)
            return ApiResponse<OrderDto>.ErrorResponse("Order not found");

        // Idempotency: Already paid
        if (order.PaymentStatus == PaymentStatus.Completed)
            return ApiResponse<OrderDto>.SuccessResponse(MapToDto(order), "Payment already confirmed");

        // 2. Call Khalti Lookup API
        KhaltiLookupResponse lookupResponse;
        try 
        {
            lookupResponse = await _khaltiPaymentService.LookupPaymentAsync(pidx, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // If Khalti fails validation (e.g., user cancellation sometimes causes issues or invalid PIDX)
            // We treat this as a payment failure rather than crashing
            order.PaymentStatus = PaymentStatus.Failed;
            
             // Log the failure in audit log roughly if possible, or just skip
            var failedLog = new PaymentAuditLog
            {
                OrderId = order.Id,
                Pidx = pidx,
                Status = "LookupFailed",
                RawResponse = $"Exception: {ex.Message}",
                CheckedAt = DateTime.UtcNow
            };
            _context.Set<PaymentAuditLog>().Add(failedLog);
            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<OrderDto>.ErrorResponse($"Payment validation failed: {ex.Message}");
        }

        // 3. Log audit trail
        var auditLog = new PaymentAuditLog
        {
            OrderId = order.Id,
            Pidx = pidx,
            Status = lookupResponse.Status,
            RawResponse = JsonSerializer.Serialize(lookupResponse),
            CheckedAt = DateTime.UtcNow
        };
        _context.Set<PaymentAuditLog>().Add(auditLog);

        // 4. Verify status
        if (lookupResponse.Status != "Completed")
        {
            order.PaymentStatus = PaymentStatus.Failed;
            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<OrderDto>.ErrorResponse($"Payment not completed. Status: {lookupResponse.Status}");
        }

        // 5. Verify amount (convert paisa to NPR)
        decimal paidAmount = lookupResponse.TotalAmount / 100m;
        if (Math.Abs(paidAmount - order.TotalAmount) > 0.01m)
        {
            // Log security alert
            order.PaymentStatus = PaymentStatus.Failed;
            await _context.SaveChangesAsync(cancellationToken);
            return ApiResponse<OrderDto>.ErrorResponse($"Amount mismatch. Expected: {order.TotalAmount}, Paid: {paidAmount}");
        }

        // 6. Update order
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            order.OrderStatus = OrderStatus.Confirmed;
            order.PaymentStatus = PaymentStatus.Completed;
            order.PaidAt = DateTime.UtcNow;

            // Lock coupon usage (if not already locked)
            if (!string.IsNullOrEmpty(order.AppliedCouponCode))
            {
                var coupon = await _context.Set<Domain.Entities.Sales.Coupon>()
                    .FirstOrDefaultAsync(c => c.Code == order.AppliedCouponCode, cancellationToken);

                if (coupon != null)
                {
                    coupon.CurrentUses++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // 7. Clear cart
            var user = await _userManager.FindByIdAsync(order.CustomerProfile.ApplicationUserId);
            if (user != null)
            {
                await _cartService.ClearCartAsync(Guid.Parse(user.Id), null, cancellationToken);
            }

            return ApiResponse<OrderDto>.SuccessResponse(MapToDto(order), "Payment confirmed successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ApiResponse<OrderDto>.ErrorResponse($"Failed to confirm payment: {ex.Message}");
        }
    }

    public async Task<OrderDto?> GetOrderByIdAsync(
        Guid orderId, 
        Guid applicationUserId, 
        bool isAdmin, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .AsQueryable();

        if (!isAdmin)
        {
            var userProfile = await _context.CustomerProfiles
                .FirstOrDefaultAsync(p => p.ApplicationUserId == applicationUserId.ToString(), cancellationToken);
                
            if (userProfile == null) return null;
            
            query = query.Where(o => o.CustomerProfileId == userProfile.Id);
        }

        var order = await query.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order == null) return null;

        return MapToDto(order);
    }

    public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(
        Guid applicationUserId, 
        CancellationToken cancellationToken = default)
    {
        var userProfile = await _context.CustomerProfiles
            .FirstOrDefaultAsync(p => p.ApplicationUserId == applicationUserId.ToString(), cancellationToken);
            
        if (userProfile == null) return Enumerable.Empty<OrderDto>();

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerProfileId == userProfile.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDto);
    }

    public async Task<PagedResult<OrderDto>> GetOrdersAsync(
        OrderFilterRequest filter, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .AsQueryable();

        // Apply filters
        if (filter.Status.HasValue)
            query = query.Where(o => o.OrderStatus == filter.Status.Value);

        if (filter.PaymentMethod.HasValue)
            query = query.Where(o => o.PaymentMethod == filter.PaymentMethod.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(o => o.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(o => o.CreatedAt <= filter.ToDate.Value);

        if (filter.UserId.HasValue)
        {
            var userProfile = await _context.CustomerProfiles
                .FirstOrDefaultAsync(p => p.ApplicationUserId == filter.UserId.Value.ToString(), cancellationToken);
            
            if (userProfile != null)
                query = query.Where(o => o.CustomerProfileId == userProfile.Id);
        }

        if (!string.IsNullOrEmpty(filter.OrderNumber))
            query = query.Where(o => o.OrderNumber.Contains(filter.OrderNumber));

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderDto>
        {
            Items = orders.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(
        Guid orderId, 
        OrderStatus newStatus, 
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            return ApiResponse<OrderDto>.ErrorResponse("Order not found");

        // Validate state transition
        var validationResult = ValidateStatusTransition(order.OrderStatus, newStatus);
        if (!validationResult.IsValid)
            return ApiResponse<OrderDto>.ErrorResponse(validationResult.ErrorMessage!);

        // Update status
        order.OrderStatus = newStatus;

        // Update timestamp fields
        switch (newStatus)
        {
            case OrderStatus.Confirmed:
                order.ConfirmedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Shipped:
                order.ShippedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Delivered:
                order.DeliveredAt = DateTime.UtcNow;
                if (order.PaymentMethod == PaymentMethod.CashOnDelivery)
                {
                    order.PaymentStatus = PaymentStatus.Completed; // Mark COD as completed on delivery
                    order.PaidAt = DateTime.UtcNow;
                }
                break;
            case OrderStatus.Cancelled:
                order.CancelledAt = DateTime.UtcNow;
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<OrderDto>.SuccessResponse(MapToDto(order), "Order status updated successfully");
    }

    private static (bool IsValid, string? ErrorMessage) ValidateStatusTransition(
        OrderStatus currentStatus, 
        OrderStatus newStatus)
    {
        // Cannot modify delivered or cancelled orders
        if (currentStatus == OrderStatus.Delivered)
            return (false, "Cannot modify delivered orders");

        if (currentStatus == OrderStatus.Cancelled && newStatus != OrderStatus.Cancelled)
            return (false, "Cannot reactivate cancelled orders");

        // PendingPayment can only go to Confirmed (via payment) or Cancelled
        if (currentStatus == OrderStatus.PendingPayment && newStatus != OrderStatus.Confirmed && newStatus != OrderStatus.Cancelled)
            return (false, "Pending payment orders must be paid before processing");

        // Valid transitions
        var validTransitions = new Dictionary<OrderStatus, List<OrderStatus>>
        {
            [OrderStatus.PendingPayment] = new() { OrderStatus.Confirmed, OrderStatus.Cancelled },
            [OrderStatus.Confirmed] = new() { OrderStatus.Processing, OrderStatus.Cancelled },
            [OrderStatus.Processing] = new() { OrderStatus.Shipped, OrderStatus.Cancelled },
            [OrderStatus.Shipped] = new() { OrderStatus.Delivered, OrderStatus.Cancelled },
            [OrderStatus.Delivered] = new() { }, // No transitions allowed
            [OrderStatus.Cancelled] = new() { }, // No transitions allowed
        };

        if (!validTransitions.ContainsKey(currentStatus))
            return (false, $"Invalid current status: {currentStatus}");

        if (!validTransitions[currentStatus].Contains(newStatus))
            return (false, $"Invalid transition from {currentStatus} to {newStatus}");

        return (true, null);
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
            PaymentMethod = order.PaymentMethod.ToString(),
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            TaxAmount = order.TaxAmount,
            ShippingAmount = order.ShippingAmount,
            TotalAmount = order.TotalAmount,
            AppliedCouponCode = order.AppliedCouponCode,
            ShippingAddress = order.ShippingAddress,
            BillingAddress = order.BillingAddress,
            PaymentUrl = order.PaymentUrl,  // Include for Khalti redirect
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductVariantId = i.ProductVariantId,
                ProductName = i.ProductName,
                VariantName = i.VariantName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                SubTotal = i.SubTotal
            }).ToList(),
            ConfirmedAt = order.ConfirmedAt,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            CancelledAt = order.CancelledAt
        };
    }
}
