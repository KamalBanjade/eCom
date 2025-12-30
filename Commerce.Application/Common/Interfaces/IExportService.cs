using Commerce.Application.Features.Orders.DTOs;
using Commerce.Application.Features.Returns.DTOs;

namespace Commerce.Application.Common.Interfaces;

/// <summary>
/// Service for exporting data to CSV format for reporting and reconciliation
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports a collection of orders to CSV format
    /// </summary>
    /// <param name="orders">Orders to export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CSV file as byte array</returns>
    Task<byte[]> ExportOrdersToCsvAsync(IEnumerable<OrderDto> orders, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports a collection of return requests to CSV format
    /// </summary>
    /// <param name="returns">Return requests to export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CSV file as byte array</returns>
    Task<byte[]> ExportReturnsToCsvAsync(IEnumerable<ReturnRequestDto> returns, CancellationToken cancellationToken = default);
}
