using System.Globalization;
using System.Text;
using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Application.Features.Returns.DTOs;

namespace Commerce.Infrastructure.Services;

/// <summary>
/// Service for exporting data to CSV format
/// </summary>
public class ExportService : IExportService
{
    public Task<byte[]> ExportOrdersToCsvAsync(IEnumerable<OrderDto> orders, CancellationToken cancellationToken = default)
    {
        var csv = new StringBuilder();
        
        // Header
        csv.AppendLine("Order Number,Order Date,Customer Email,Status,Payment Status,Payment Method,Subtotal,Discount,Tax,Shipping,Total,Assigned To,Assigned Role,Assigned At");
        
        // Data rows
        foreach (var order in orders)
        {
            csv.AppendLine($"{EscapeCsv(order.OrderNumber)}," +
                          $"{order.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{EscapeCsv(order.CustomerEmail ?? "")}," +
                          $"{EscapeCsv(order.OrderStatus)}," +
                          $"{EscapeCsv(order.PaymentStatus)}," +
                          $"{EscapeCsv(order.PaymentMethod)}," +
                          $"{order.SubTotal:F2}," +
                          $"{order.DiscountAmount:F2}," +
                          $"{order.TaxAmount:F2}," +
                          $"{order.ShippingAmount:F2}," +
                          $"{order.TotalAmount:F2}," +
                          $"{EscapeCsv(order.AssignedToUserEmail ?? "")}," +
                          $"{EscapeCsv(order.AssignedRole ?? "")}," +
                          $"{(order.AssignedAt.HasValue ? order.AssignedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "")}");
        }
        
        return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    public Task<byte[]> ExportReturnsToCsvAsync(IEnumerable<ReturnRequestDto> returns, CancellationToken cancellationToken = default)
    {
        var csv = new StringBuilder();
        
        // Header
        csv.AppendLine("Return ID,Order Number,Customer Email,Customer Name,Reason,Status,Refund Amount,Refund Method,Requested At,Approved At,Received At,Refunded At,Assigned To,Assigned At");
        
        // Data rows
        foreach (var returnRequest in returns)
        {
            csv.AppendLine($"{returnRequest.Id}," +
                          $"{EscapeCsv(returnRequest.OrderNumber)}," +
                          $"{EscapeCsv(returnRequest.CustomerEmail)}," +
                          $"{EscapeCsv(returnRequest.CustomerName)}," +
                          $"{EscapeCsv(returnRequest.Reason)}," +
                          $"{EscapeCsv(returnRequest.ReturnStatus)}," +
                          $"{returnRequest.RefundAmount:F2}," +
                          $"{EscapeCsv(returnRequest.RefundMethod ?? "")}," +
                          $"{returnRequest.RequestedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{(returnRequest.ApprovedAt.HasValue ? returnRequest.ApprovedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "")}," +
                          $"{(returnRequest.ReceivedAt.HasValue ? returnRequest.ReceivedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "")}," +
                          $"{(returnRequest.RefundedAt.HasValue ? returnRequest.RefundedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "")}," +
                          $"{EscapeCsv(returnRequest.AssignedToUserEmail ?? "")}," +
                          $"{(returnRequest.AssignedAt.HasValue ? returnRequest.AssignedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "")}");
        }
        
        return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    /// <summary>
    /// Escapes CSV values to handle commas, quotes, and newlines
    /// </summary>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
            
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
    }
}
