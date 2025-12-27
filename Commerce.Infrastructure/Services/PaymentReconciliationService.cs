using Commerce.Application.Common.Interfaces;
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

        // Find stuck payments (initiated more than 5 minutes ago)
        var cutoffTime = DateTime.UtcNow.AddMinutes(-5);
        var pendingOrders = await context.Orders
            .Where(o => o.PaymentMethod == PaymentMethod.Khalti
                     && o.PaymentStatus == PaymentStatus.Initiated
                     && o.CreatedAt < cutoffTime
                     && !string.IsNullOrEmpty(o.Pidx))
            .ToListAsync(cancellationToken);

        if (!pendingOrders.Any())
        {
            _logger.LogInformation("No pending payments to reconcile");
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
                if (lookupResponse.Status == "Completed")
                {
                    // Verify amount
                    decimal paidAmount = lookupResponse.TotalAmount / 100m;
                    if (Math.Abs(paidAmount - order.TotalAmount) <= 0.01m)
                    {
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

                        _logger.LogInformation("Payment reconciled successfully for Order {OrderNumber}", order.OrderNumber);
                    }
                    else
                    {
                        _logger.LogWarning("Amount mismatch for Order {OrderNumber}. Expected: {Expected}, Paid: {Paid}", 
                            order.OrderNumber, order.TotalAmount, paidAmount);
                        order.PaymentStatus = PaymentStatus.Failed;
                    }
                }
                else if (lookupResponse.Status == "Expired" || lookupResponse.Status == "User canceled")
                {
                    order.PaymentStatus = PaymentStatus.Failed;
                    _logger.LogInformation("Payment marked as failed for Order {OrderNumber}. Status: {Status}", 
                        order.OrderNumber, lookupResponse.Status);
                }
                else
                {
                    _logger.LogInformation("Payment still pending for Order {OrderNumber}. Status: {Status}", 
                        order.OrderNumber, lookupResponse.Status);
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
