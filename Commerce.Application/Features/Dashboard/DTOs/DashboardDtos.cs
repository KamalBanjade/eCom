using Commerce.Domain.Enums;
using System.Text.Json.Serialization;

namespace Commerce.Application.Features.Dashboard.DTOs;

public class DashboardSummaryDto
{
    [JsonPropertyName("totalRevenue")]
    public decimal TotalRevenue { get; set; }
    
    [JsonPropertyName("totalOrders")]
    public int TotalOrders { get; set; }
    
    [JsonPropertyName("totalCustomers")]
    public int TotalCustomers { get; set; }
    
    [JsonPropertyName("activeProducts")]
    public int ActiveProducts { get; set; }
    
    [JsonPropertyName("revenueChangePercentage")]
    public decimal RevenueChangePercentage { get; set; }
    
    [JsonPropertyName("orderChangePercentage")]
    public decimal OrderChangePercentage { get; set; }
    
    [JsonPropertyName("openReturnRequests")]
    public int OpenReturnRequests { get; set; }
    
    [JsonPropertyName("activeCoupons")]
    public int ActiveCoupons { get; set; }
    
    [JsonPropertyName("outOfStockCount")]
    public int OutOfStockCount { get; set; }
    
    [JsonPropertyName("totalReturns")]
    public int TotalReturns { get; set; }

    [JsonPropertyName("staffCount")]
    public int StaffCount { get; set; }
}

public class SalesTrendDto
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    
    [JsonPropertyName("revenue")]
    public decimal Revenue { get; set; }
    
    [JsonPropertyName("orderCount")]
    public int OrderCount { get; set; }

    [JsonPropertyName("returnCount")]
    public int ReturnCount { get; set; }
}

public class UserGrowthDto
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    
    [JsonPropertyName("newCustomers")]
    public int NewCustomers { get; set; }
}

public class StatusDistributionDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("percentage")]
    public decimal Percentage { get; set; }
}

public class CategoryPerformanceDto
{
    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; } = string.Empty;
    
    [JsonPropertyName("totalSales")]
    public decimal TotalSales { get; set; }
    
    [JsonPropertyName("productCount")]
    public int ProductCount { get; set; }
}

public class CouponPerformanceDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    [JsonPropertyName("uses")]
    public int Uses { get; set; }
    
    [JsonPropertyName("totalDiscountGiven")]
    public decimal TotalDiscountGiven { get; set; }
}

public class TopProductDto
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; set; }
    
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;
    
    [JsonPropertyName("variantName")]
    public string? VariantName { get; set; }
    
    [JsonPropertyName("totalSold")]
    public int TotalSold { get; set; }
    
    [JsonPropertyName("totalRevenue")]
    public decimal TotalRevenue { get; set; }
    
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}

public class InventoryAlertDto
{
    [JsonPropertyName("variantId")]
    public Guid VariantId { get; set; }
    
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;
    
    [JsonPropertyName("sku")]
    public string SKU { get; set; } = string.Empty;
    
    [JsonPropertyName("stockQuantity")]
    public int StockQuantity { get; set; }
}

public class HealthSignifierDto
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;
}

public class DashboardDataDto
{
    [JsonPropertyName("summary")]
    public DashboardSummaryDto Summary { get; set; } = new();
    
    [JsonPropertyName("salesTrend")]
    public List<SalesTrendDto> SalesTrend { get; set; } = new();
    
    [JsonPropertyName("userGrowth")]
    public List<UserGrowthDto> UserGrowth { get; set; } = new();
    
    [JsonPropertyName("orderStatusDistribution")]
    public List<StatusDistributionDto> OrderStatusDistribution { get; set; } = new();
    
    [JsonPropertyName("returnReasonDistribution")]
    public List<StatusDistributionDto> ReturnReasonDistribution { get; set; } = new();
    
    [JsonPropertyName("paymentMethodDistribution")]
    public List<StatusDistributionDto> PaymentMethodDistribution { get; set; } = new();
    
    [JsonPropertyName("categoryPerformance")]
    public List<CategoryPerformanceDto> CategoryPerformance { get; set; } = new();
    
    [JsonPropertyName("topCoupons")]
    public List<CouponPerformanceDto> TopCoupons { get; set; } = new();
    
    [JsonPropertyName("topProducts")]
    public List<TopProductDto> TopProducts { get; set; } = new();
    
    [JsonPropertyName("lowStockAlerts")]
    public List<InventoryAlertDto> lowStockAlerts { get; set; } = new();

    [JsonPropertyName("systemHealth")]
    public List<HealthSignifierDto> SystemHealth { get; set; } = new();
    
    [JsonPropertyName("returnRate")]
    public decimal ReturnRate { get; set; }
    
    [JsonPropertyName("averageOrderValue")]
    public double AverageOrderValue { get; set; }
}
