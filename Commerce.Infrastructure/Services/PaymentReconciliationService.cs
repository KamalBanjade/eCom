using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Inventory;
using Commerce.Domain.Entities.Payments;
using Commerce.Domain.Enums;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Commerce.Infrastructure.Services;

public class PaymentReconciliationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentReconciliationService> _logger;
    private readonly TimeSpan _reconciliationInterval = TimeSpan.FromMinutes(3);
    //  private readonly TimeSpan _reconciliationInterval = TimeSpan.FromSeconds(30);

    public PaymentReconciliationService(
        IServiceProvider serviceProvider,
        ILogger<PaymentReconciliationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment Reconciliation Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcilePaymentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in payment reconciliation cycle");
            }

            await Task.Delay(_reconciliationInterval, stoppingToken);
        }

        _logger.LogInformation("Payment Reconciliation Service stopped");
    }

    private async Task ReconcilePaymentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var khaltiService = scope.ServiceProvider.GetRequiredService<IKhaltiPaymentService>();
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        // Find stuck payments (initiated more than 3 minutes ago)
        var cutoffTime = DateTime.UtcNow.AddMinutes(-3);
        
        Console.WriteLine("==========================================================");
        Console.WriteLine($"[RECONCILIATION] Cutoff Time: {cutoffTime:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"[RECONCILIATION] Current Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        
        // TARGETED DEBUGGING FOR SPECIFIC ORDER
        var debugOrder = await context.Orders
            .FirstOrDefaultAsync(o => o.OrderNumber == "ORD-20260123-FD7C4CE3", cancellationToken);
        
        if (debugOrder != null)
        {
            Console.WriteLine($"[TARGET DEBUG] Found Order ORD-20260123-FD7C4CE3");
            Console.WriteLine($"  PaymentStatus: {debugOrder.PaymentStatus} (Int: {(int)debugOrder.PaymentStatus})");
            Console.WriteLine($"  PaymentMethod: {debugOrder.PaymentMethod}");
            Console.WriteLine($"  Pidx: '{debugOrder.Pidx}'");
            Console.WriteLine($"  CreatedAt: {debugOrder.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  Is Khalti? {debugOrder.PaymentMethod == PaymentMethod.Khalti}");
            Console.WriteLine($"  Is Initiated? {debugOrder.PaymentStatus == PaymentStatus.Initiated}");
            Console.WriteLine($"  Is Older than cutoff? {debugOrder.CreatedAt < cutoffTime}");
            Console.WriteLine($"  Has Pidx? {!string.IsNullOrEmpty(debugOrder.Pidx)}");
        }
        else
        {
            Console.WriteLine($"[TARGET DEBUG] Order ORD-20260123-FD7C4CE3 NOT FOUND in DB!");
        }
        Console.WriteLine("==========================================================");
        
        var pendingOrders = await context.Orders
            .Include(o => o.Items) // Needed for Stock Confirmation
            .Where(o => o.PaymentMethod == PaymentMethod.Khalti
                     && o.PaymentStatus == PaymentStatus.Initiated
                     && o.CreatedAt < cutoffTime
                     && !string.IsNullOrEmpty(o.Pidx))
            .ToListAsync(cancellationToken);

        if (!pendingOrders.Any())
        {
            _logger.LogInformation("No pending payments to reconcile");
            
            // DIAGNOSTIC: Show why orders might not be picked up
            var allKhaltiOrders = await context.Orders
                .Where(o => o.PaymentMethod == PaymentMethod.Khalti 
                         && o.PaymentStatus == PaymentStatus.Initiated)
                .Select(o => new { o.OrderNumber, o.Pidx, o.CreatedAt, o.PaymentStatus })
                .ToListAsync(cancellationToken);
            
            Console.WriteLine($"[RECONCILIATION DEBUG] Found {allKhaltiOrders.Count} Khalti orders with Initiated status");
            foreach (var order in allKhaltiOrders)
            {
                var age = DateTime.UtcNow - order.CreatedAt;
                Console.WriteLine($"  Order: {order.OrderNumber}, Pidx: {order.Pidx}, Age: {age.TotalMinutes:F1} min, Status: {order.PaymentStatus}");
            }
            
            return;
        }

        _logger.LogInformation("Reconciling {Count} pending payments", pendingOrders.Count);

        foreach (var order in pendingOrders)
        {
            try
            {
                _logger.LogInformation("Reconciling payment for Order {OrderNumber}, Pidx: {Pidx}", 
                    order.OrderNumber, order.Pidx);

                // Call Khalti Lookup API
                var lookupResponse = await khaltiService.LookupPaymentAsync(order.Pidx!, cancellationToken);

                // Log audit trail
                context.Set<PaymentAuditLog>().Add(new PaymentAuditLog
                {
                    OrderId = order.Id,
                    Pidx = order.Pidx,
                    Status = lookupResponse.Status,
                    RawResponse = JsonSerializer.Serialize(lookupResponse),
                    CheckedAt = DateTime.UtcNow
                });

                // Update order based on status
                // Update order based on status
                if (lookupResponse.Status == "Completed")
                {
                    // Verify amount
                    decimal paidAmount = lookupResponse.TotalAmount / 100m;
                    if (Math.Abs(paidAmount - order.TotalAmount) <= 0.01m)
                    {
                        // CRITICAL FIX: Confirm Stock before marking as Completed
                        // Using the new resilient ConfirmStockAsync (handles expired reservations)
                        try 
                        {
                            foreach (var item in order.Items)
                            {
                                // Using CustomerProfileId or falling back to "SYSTEM" (reconciliation)
                                // We use order.CustomerProfileId if available
                                var userId = order.CustomerProfileId?.ToString() ?? "SYSTEM_RECONCILIATION";
                                
                                await inventoryService.ConfirmStockAsync(
                                    item.ProductVariantId, 
                                    item.Quantity, 
                                    userId,
                                    cancellationToken);
                            }

                            order.OrderStatus = OrderStatus.Confirmed;
                            order.PaymentStatus = PaymentStatus.Completed;
                            order.PaidAt = DateTime.UtcNow;

                            // Lock coupon if applicable
                            if (!string.IsNullOrEmpty(order.AppliedCouponCode))
                            {
                                var coupon = await context.Set<Domain.Entities.Sales.Coupon>()
                                    .FirstOrDefaultAsync(c => c.Code == order.AppliedCouponCode, cancellationToken);

                                if (coupon != null)
                                {
                                    coupon.CurrentUses++;
                                }
                            }

                            _logger.LogInformation("Payment reconciled and stock confirmed for Order {OrderNumber}", order.OrderNumber);
                        }
                        catch (Exception ex)
                        {
                            // Stock failed (e.g. True Out Of Stock)
                            // We cannot mark as Completed effectively.
                            // Options: 
                            // 1. Mark PaymentStatus=Failed? (User paid!)
                            // 2. Mark PaymentStatus=Completed, OrderStatus=Cancelled/RefundRequired?
                            // For now, we Log Error and do NOT update status, keeping it "Initiated" so Admin sees it needs attention.
                            // Or, we could mark it as a special status like "Processing" but with a specialized flag.
                            // Let's stick to Logging Error so it remains "Initiated" (Orphaned) for manual intervention.
                            
                             _logger.LogError(ex, "Payment verified manually but Stock Confirmation Failed for Order {OrderNumber}. Manual Refund Required.", order.OrderNumber);
                             
                             // Optional: Add an audit log about this failure
                             context.Set<PaymentAuditLog>().Add(new PaymentAuditLog
                             {
                                 OrderId = order.Id,
                                 Pidx = order.Pidx,
                                 Status = "StockFailed",
                                 RawResponse = $"Payment Success, Stock Fail: {ex.Message}",
                                 CheckedAt = DateTime.UtcNow
                             });
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Amount mismatch for Order {OrderNumber}. Expected: {Expected}, Paid: {Paid}", 
                            order.OrderNumber, order.TotalAmount, paidAmount);
                        order.PaymentStatus = PaymentStatus.Failed;
                        order.OrderStatus = OrderStatus.Cancelled;
                    }
                }
                else if (lookupResponse.Status == "Expired" || lookupResponse.Status == "User canceled")
                {
                    Console.WriteLine("==========================================================");
                    Console.WriteLine($"[PAYMENT CANCELLED/EXPIRED]");
                    Console.WriteLine($"Order: {order.OrderNumber}");
                    Console.WriteLine($"Pidx: {order.Pidx}");
                    Console.WriteLine($"Status from Khalti: {lookupResponse.Status}");
                    Console.WriteLine($"Marking PaymentStatus as Failed");
                    Console.WriteLine("==========================================================");
                    
                    order.PaymentStatus = PaymentStatus.Failed;
                    order.OrderStatus = OrderStatus.Cancelled;
                    _logger.LogInformation("Payment marked as failed for Order {OrderNumber}. Status: {Status}", 
                        order.OrderNumber, lookupResponse.Status);
                }
                else
                {
                    // If it's still Pending but it was created longer ago than our cutoff, 
                    // we consider it abandoned and cancel it to free up stock.
                    Console.WriteLine("==========================================================");
                    Console.WriteLine($"[PAYMENT ABANDONED]");
                    Console.WriteLine($"Order: {order.OrderNumber}");
                    Console.WriteLine($"Pidx: {order.Pidx}");
                    Console.WriteLine($"Status from Khalti: {lookupResponse.Status}");
                    Console.WriteLine($"Order Age: {(DateTime.UtcNow - order.CreatedAt).TotalMinutes:F1} minutes");
                    Console.WriteLine($"Marking as Failed/Cancelled due to inactivity");
                    Console.WriteLine("==========================================================");

                    order.PaymentStatus = PaymentStatus.Failed;
                    order.OrderStatus = OrderStatus.Cancelled;
                    
                    _logger.LogInformation("Payment abandoned for Order {OrderNumber}. Marking as Cancelled.", 
                        order.OrderNumber);
                }

                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconcile payment for Order {OrderNumber}", order.OrderNumber);
            }
        }

        _logger.LogInformation("Payment reconciliation cycle completed");
    }
}
