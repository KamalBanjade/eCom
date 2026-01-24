
namespace Commerce.Application.Features.Inventory;

public interface IInventoryService
{
    // Check if stock is available (considering active reservations)
    Task<bool> IsStockAvailableAsync(Guid productVariantId, int quantity, CancellationToken cancellationToken = default);
    
    // Reserve stock (e.g., adding to cart)
    Task<bool> ReserveStockAsync(Guid productVariantId, int quantity, string userId, TimeSpan duration, CancellationToken cancellationToken = default);
    
    // Release a specific reservation (e.g., removing from cart)
    Task<bool> ReleaseReservationAsync(Guid productVariantId, string userId, CancellationToken cancellationToken = default);
    
    // Confirm stock deduction (checkout success) - Permanently decrements StockQuantity and marks reservation as confirmed
    Task ConfirmStockAsync(Guid productVariantId, int quantity, string userId, CancellationToken cancellationToken = default);
    
    // Cleanup expired reservations (background job candidate)
    Task<int> CleanupExpiredReservationsAsync(CancellationToken cancellationToken = default);
    
    // Get current available stock (Total - Reserved)
    Task<int> GetAvailableStockAsync(Guid productVariantId, CancellationToken cancellationToken = default);

    // Generic stock adjustment (Manual Admin changes, Restocks, Returns)
    Task AdjustStockAsync(Guid productVariantId, int quantityChange, string reason, string? userId, CancellationToken cancellationToken = default);
}
