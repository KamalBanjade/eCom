using Commerce.Application.Features.Dashboard;
using Commerce.Application.Features.Dashboard.DTOs;
using Commerce.Application.Features.Auth;
using Commerce.Infrastructure.Data;
using Commerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Commerce.Infrastructure.Identity;

namespace Commerce.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly CommerceDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardService(CommerceDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<DashboardDataDto> GetDashboardDataAsync(int days = 30, CancellationToken cancellationToken = default)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        var previousStartDate = startDate.AddDays(-days);

        // 1. Summary Statistics
        var currentPeriodOrders = await _context.Orders
            .Where(o => o.CreatedAt >= startDate)
            .ToListAsync(cancellationToken);

        var previousPeriodOrders = await _context.Orders
            .Where(o => o.CreatedAt >= previousStartDate && o.CreatedAt < startDate)
            .ToListAsync(cancellationToken);

        var totalRevenue = await _context.Orders
            .Where(o => o.OrderStatus != OrderStatus.Cancelled)
            .SumAsync(o => o.TotalAmount, cancellationToken);

        var totalOrders = await _context.Orders.CountAsync(cancellationToken);
        var totalCustomers = await _context.CustomerProfiles.CountAsync(cancellationToken);
        var activeProducts = await _context.Products.CountAsync(p => p.IsActive, cancellationToken);

        var currentRevenue = currentPeriodOrders.Where(o => o.OrderStatus != OrderStatus.Cancelled).Sum(o => o.TotalAmount);
        var previousRevenue = previousPeriodOrders.Where(o => o.OrderStatus != OrderStatus.Cancelled).Sum(o => o.TotalAmount);

        var revenueChange = previousRevenue == 0 ? 100 : ((currentRevenue - previousRevenue) / previousRevenue) * 100;
        var orderChange = previousPeriodOrders.Count == 0 ? 100 : ((decimal)(currentPeriodOrders.Count - previousPeriodOrders.Count) / previousPeriodOrders.Count) * 100;

        // New Summary Metrics
        var openReturns = await _context.Returns.CountAsync(r => r.ReturnStatus == ReturnStatus.Requested || r.ReturnStatus == ReturnStatus.Approved, cancellationToken);
        var activeCoupons = await _context.Coupons.CountAsync(c => c.IsActive && c.ExpiryDate > DateTime.UtcNow, cancellationToken);
        var outOfStock = await _context.ProductVariants.CountAsync(pv => pv.StockQuantity == 0, cancellationToken);
        
        var staffUsers = await _userManager.GetUsersInRoleAsync(UserRoles.Admin);
        var superStaff = await _userManager.GetUsersInRoleAsync(UserRoles.SuperAdmin);
        var warehouseStaff = await _userManager.GetUsersInRoleAsync(UserRoles.Warehouse);
        var supportStaff = await _userManager.GetUsersInRoleAsync(UserRoles.Support);
        var totalStaff = staffUsers.Count + superStaff.Count + warehouseStaff.Count + supportStaff.Count;

        // 2. Trends
        var salesTrend = await _context.Orders
            .Where(o => o.CreatedAt >= startDate && o.OrderStatus != OrderStatus.Cancelled)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new SalesTrendDto
            {
                Date = g.Key,
                Revenue = g.Sum(o => o.TotalAmount),
                OrderCount = g.Count()
            })
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        var returnsTrendData = await _context.Returns
            .Where(r => r.RequestedAt >= startDate)
            .GroupBy(r => r.RequestedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, cancellationToken);

        foreach (var item in salesTrend)
        {
            if (returnsTrendData.TryGetValue(item.Date.Date, out var returnCount))
            {
                item.ReturnCount = returnCount;
            }
        }

        var userGrowth = await _context.CustomerProfiles
            .Where(c => c.CreatedAt >= startDate)
            .GroupBy(c => c.CreatedAt.Date)
            .Select(g => new UserGrowthDto
            {
                Date = g.Key,
                NewCustomers = g.Count()
            })
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        // 3. Distributions
        var orderStatusDist = await _context.Orders
            .GroupBy(o => o.OrderStatus)
            .Select(g => new StatusDistributionDto
            {
                Status = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var totalOrderCount = orderStatusDist.Sum(x => x.Count);
        foreach (var dist in orderStatusDist) dist.Percentage = totalOrderCount > 0 ? (decimal)dist.Count / totalOrderCount * 100 : 0;

        var returnReasonDist = await _context.Returns
            .SelectMany(r => r.Items)
            .GroupBy(ri => ri.Reason)
            .Select(g => new StatusDistributionDto
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);
            
        var totalReturnCount = returnReasonDist.Sum(x => x.Count);
        foreach (var dist in returnReasonDist) dist.Percentage = totalReturnCount > 0 ? (decimal)dist.Count / totalReturnCount * 100 : 0;

        var paymentMethodDist = await _context.Orders
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new StatusDistributionDto
            {
                Status = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var totalPaymentCount = paymentMethodDist.Sum(x => x.Count);
        foreach (var dist in paymentMethodDist) dist.Percentage = totalPaymentCount > 0 ? (decimal)dist.Count / totalPaymentCount * 100 : 0;

        // 4. Category Performance
        var categoryPerf = await _context.OrderItems
            .Include(oi => oi.ProductVariant)
                .ThenInclude(pv => pv.Product)
                    .ThenInclude(p => p.Category)
            .GroupBy(oi => oi.ProductVariant.Product.CategoryId)
            .Select(g => new CategoryPerformanceDto
            {
                CategoryName = g.First().ProductVariant.Product.Category.Name,
                TotalSales = g.Sum(oi => oi.UnitPrice * oi.Quantity),
                ProductCount = g.Select(oi => oi.ProductVariant.ProductId).Distinct().Count()
            })
            .OrderByDescending(c => c.TotalSales)
            .Take(5)
            .ToListAsync(cancellationToken);

        // 5. Coupon Performance
        var couponPerf = await _context.Orders
            .Where(o => o.AppliedCouponCode != null)
            .GroupBy(o => o.AppliedCouponCode)
            .Select(g => new CouponPerformanceDto
            {
                Code = g.Key!,
                Uses = g.Count(),
                TotalDiscountGiven = g.Sum(o => o.DiscountAmount)
            })
            .OrderByDescending(cp => cp.Uses)
            .Take(5)
            .ToListAsync(cancellationToken);

        // 6. Rankings & Alerts
        var topProducts = await _context.OrderItems
            .Include(oi => oi.ProductVariant)
                .ThenInclude(pv => pv.Product)
            .GroupBy(oi => oi.ProductVariantId)
            .Select(g => new TopProductDto
            {
                ProductId = g.First().ProductVariant.ProductId,
                ProductName = g.First().ProductVariant.Product.Name,
                VariantName = g.First().ProductVariant.SKU,
                TotalSold = g.Sum(oi => oi.Quantity),
                TotalRevenue = g.Sum(oi => oi.UnitPrice * oi.Quantity),
                ImageUrl = g.First().ProductVariant.ImageUrls.FirstOrDefault() ?? g.First().ProductVariant.Product.ImageUrls.FirstOrDefault()
            })
            .OrderByDescending(p => p.TotalSold)
            .Take(5)
            .ToListAsync(cancellationToken);

        var lowStock = await _context.ProductVariants
            .Include(pv => pv.Product)
            .Where(pv => pv.StockQuantity < 10)
            .Select(pv => new InventoryAlertDto
            {
                VariantId = pv.Id,
                ProductName = pv.Product.Name,
                SKU = pv.SKU,
                StockQuantity = pv.StockQuantity
            })
            .OrderBy(pv => pv.StockQuantity)
            .Take(5)
            .ToListAsync(cancellationToken);

        // 7. System Health Pulse (Replacing hardcoded data with actual telemetry)
        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var verifiedUsers = await _context.Users.CountAsync(u => u.EmailConfirmed, cancellationToken);
        var identityVerificationRate = totalUsers > 0 ? (double)verifiedUsers / totalUsers * 100 : 0;

        var totalProductVariants = await _context.ProductVariants.CountAsync(cancellationToken);
        var inStockVariants = await _context.ProductVariants.CountAsync(pv => pv.StockQuantity > 0, cancellationToken);
        var assetIntegrity = totalProductVariants > 0 ? (double)inStockVariants / totalProductVariants * 100 : 0;

        var totalDelivered = await _context.Orders.CountAsync(o => o.OrderStatus == OrderStatus.Delivered, cancellationToken);
        var returnRate = totalDelivered > 0 ? (decimal)totalReturnCount / totalDelivered * 100 : 0;

        var systemHealth = new List<HealthSignifierDto>
        {
            new HealthSignifierDto 
            { 
                Label = "Identity Verification Rate", 
                Value = $"{identityVerificationRate:F1}%", 
                Status = identityVerificationRate > 90 ? "OPTIMAL" : "STABLE", 
                Color = "emerald" 
            },
            new HealthSignifierDto 
            { 
                Label = "Asset Portfolio Integrity", 
                Value = $"{assetIntegrity:F1}%", 
                Status = assetIntegrity > 80 ? "OPTIMAL" : "WARNING", 
                Color = "indigo" 
            },
            new HealthSignifierDto 
            { 
                Label = "Return Ratio Index", 
                Value = $"{returnRate:F1}%", 
                Status = returnRate < 5 ? "OPTIMAL" : "STABLE", 
                Color = "amber" 
            }
        };

        return new DashboardDataDto
        {
            Summary = new DashboardSummaryDto
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                TotalCustomers = totalCustomers,
                ActiveProducts = activeProducts,
                RevenueChangePercentage = revenueChange,
                OrderChangePercentage = orderChange,
                OpenReturnRequests = openReturns,
                ActiveCoupons = activeCoupons,
                OutOfStockCount = outOfStock,
                TotalReturns = await _context.Returns.CountAsync(cancellationToken),
                StaffCount = totalStaff
            },
            SalesTrend = salesTrend,
            UserGrowth = userGrowth,
            OrderStatusDistribution = orderStatusDist,
            ReturnReasonDistribution = returnReasonDist,
            PaymentMethodDistribution = paymentMethodDist,
            CategoryPerformance = categoryPerf,
            TopCoupons = couponPerf,
            TopProducts = topProducts,
            lowStockAlerts = lowStock,
            SystemHealth = systemHealth,
            ReturnRate = returnRate,
            AverageOrderValue = totalOrders > 0 ? (double)(totalRevenue / totalOrders) : 0
        };
    }
}
