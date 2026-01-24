using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Inventory;
using Commerce.Domain.Configuration;
using Commerce.Domain.Entities.Inventory;
using Commerce.Domain.Entities.Products;
using Commerce.Domain.Exceptions;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly CommerceDbContext _context;
    private readonly IRepository<StockReservation> _reservationRepository;
    private readonly IRepository<ProductVariant> _variantRepository;
    private readonly IOptions<InventoryConfiguration> _config;
    private readonly ILogger<InventoryService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public InventoryService(
        CommerceDbContext context,
        IRepository<StockReservation> reservationRepository,
        IRepository<ProductVariant> variantRepository,
        IOptions<InventoryConfiguration> config,
        ILogger<InventoryService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reservationRepository = reservationRepository ?? throw new ArgumentNullException(nameof(reservationRepository));
        _variantRepository = variantRepository ?? throw new ArgumentNullException(nameof(variantRepository));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    #region Helper Methods

    private void ValidateQuantity(int quantity, string paramName)
    {
        if (quantity < _config.Value.MinReservationQuantity)
            throw new ArgumentException($"Quantity must be at least {_config.Value.MinReservationQuantity}", paramName);
        if (quantity > _config.Value.MaxReservationQuantity)
            throw new ArgumentException($"Quantity cannot exceed {_config.Value.MaxReservationQuantity}", paramName);
    }

    private void ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
    }

   private Task LogStockChangeAsync(
    Guid productVariantId,
    string action,
    int quantityChanged,
    int stockBefore,
    int stockAfter,
    string? userId = null,
    Guid? reservationId = null,
    string? reason = null)
{
    var log = new StockAuditLog
    {
        ProductVariantId = productVariantId,
        Action = action,
        QuantityChanged = quantityChanged,
        StockBefore = stockBefore,
        StockAfter = stockAfter,
        UserId = userId,
        ReservationId = reservationId,
        Reason = reason,
        Timestamp = DateTime.UtcNow
    };

    _context.StockAuditLogs.Add(log);
    
    return Task.CompletedTask; // ✅ ADD THIS
}

    private void LogStockChangeFireAndForget(
        Guid productVariantId,
        string action,
        int quantityChanged,
        int stockBefore,
        int stockAfter,
        string? userId = null,
        Guid? reservationId = null,
        string? reason = null)
    {
        // Fire-and-forget logging for non-critical operations using new scope for thread safety
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
                
                var log = new StockAuditLog
                {
                    ProductVariantId = productVariantId,
                    Action = action,
                    QuantityChanged = quantityChanged,
                    StockBefore = stockBefore,
                    StockAfter = stockAfter,
                    UserId = userId,
                    ReservationId = reservationId,
                    Reason = reason,
                    Timestamp = DateTime.UtcNow
                };
                
                dbContext.StockAuditLogs.Add(log);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log stock change for variant {ProductVariantId}", productVariantId);
            }
        });
    }

    #endregion

    public async Task<int> GetAvailableStockAsync(Guid productVariantId, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(productVariantId, cancellationToken);
        if (variant == null) return 0;

        // Sum active reservations
        var reservedQuantity = await _context.Set<StockReservation>()
            .Where(r => r.ProductVariantId == productVariantId &&
                        !r.IsReleased &&
                        !r.IsConfirmed &&
                        r.ExpiresAt > DateTime.UtcNow)
            .SumAsync(r => r.Quantity, cancellationToken);

        return Math.Max(0, variant.StockQuantity - reservedQuantity);
    }

    public async Task<bool> IsStockAvailableAsync(Guid productVariantId, int quantity, CancellationToken cancellationToken = default)
    {
        var available = await GetAvailableStockAsync(productVariantId, cancellationToken);
        return available >= quantity;
    }

    public async Task<bool> ReserveStockAsync(Guid productVariantId, int quantity, string userId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        ValidateQuantity(quantity, nameof(quantity));
        ValidateUserId(userId);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Pessimistic locking: Lock the ProductVariant row (PostgreSQL: FOR UPDATE)
            var variant = await _context.ProductVariants
                .FromSqlRaw(@"SELECT * FROM ""ProductVariants"" WHERE ""Id"" = {0} FOR UPDATE", productVariantId)
                .FirstOrDefaultAsync(cancellationToken);

            if (variant == null)
                throw new InvalidOperationException($"Product variant {productVariantId} not found");

            // Auto-cleanup expired reservations for this user
            var expiredForUser = await _context.Set<StockReservation>()
                .Where(r => r.UserId == userId && !r.IsReleased && !r.IsConfirmed && r.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var expired in expiredForUser)
            {
                expired.IsReleased = true;
            }

            var existingReservation = await _context.Set<StockReservation>()
                .FirstOrDefaultAsync(r => r.ProductVariantId == productVariantId &&
                                          r.UserId == userId &&
                                          !r.IsReleased &&
                                          !r.IsConfirmed, cancellationToken);

            // Calculate available stock
            var reservedQuantity = await _context.Set<StockReservation>()
                .Where(r => r.ProductVariantId == productVariantId &&
                            !r.IsReleased &&
                            !r.IsConfirmed &&
                            r.ExpiresAt > DateTime.UtcNow)
                .SumAsync(r => r.Quantity, cancellationToken);

            var available = variant.StockQuantity - reservedQuantity;

            int quantityNeeded = quantity;
            if (existingReservation != null)
            {
                // If updating, only need to check the delta if increasing
                if (quantity > existingReservation.Quantity)
                {
                    quantityNeeded = quantity - existingReservation.Quantity;
                }
                else
                {
                    // Decreasing reservation, always allowed
                    quantityNeeded = 0;
                }
            }

            if (quantityNeeded > 0 && available < quantityNeeded)
            {
                throw new InsufficientStockException(productVariantId, quantity, available);
            }

            Guid reservationId;
            if (existingReservation != null)
            {
                int oldQuantity = existingReservation.Quantity;
                existingReservation.Quantity = quantity;
                existingReservation.ExpiresAt = DateTime.UtcNow.Add(duration);
                _context.Set<StockReservation>().Update(existingReservation);
                reservationId = existingReservation.Id;

                // Audit log - reservations don't change physical stock
                await LogStockChangeAsync(
                    productVariantId,
                    "Reserve_Update",
                    quantity - oldQuantity,
                    0,  // Physical stock unchanged
                    0,  // Physical stock unchanged
                    userId,
                    reservationId,
                    $"Updated reservation from {oldQuantity} to {quantity}");
            }
            else
            {
                var reservation = new StockReservation
                {
                    Id = Guid.NewGuid(),
                    ProductVariantId = productVariantId,
                    Quantity = quantity,
                    UserId = userId,
                    ExpiresAt = DateTime.UtcNow.Add(duration),
                    IsReleased = false,
                    IsConfirmed = false
                };
                await _context.Set<StockReservation>().AddAsync(reservation, cancellationToken);
                reservationId = reservation.Id;

                // Audit log - reservations don't change physical stock
                await LogStockChangeAsync(
                    productVariantId,
                    "Reserve",
                    quantity,
                    0,  // Physical stock unchanged
                    0,  // Physical stock unchanged
                    userId,
                    reservationId,
                    "New reservation created");
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> ReleaseReservationAsync(Guid productVariantId, string userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var reservation = await _context.Set<StockReservation>()
                .FirstOrDefaultAsync(r => r.ProductVariantId == productVariantId &&
                                          r.UserId == userId &&
                                          !r.IsReleased &&
                                          !r.IsConfirmed, cancellationToken);

            if (reservation == null)
            {
                return false; // No active reservation found
            }

            reservation.IsReleased = true;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Fire-and-forget audit logging
            LogStockChangeFireAndForget(
                productVariantId,
                "Release",
                -reservation.Quantity,
                0, // Not tracking variant stock here
                0,
                userId,
                reservation.Id,
                "Reservation released manually");

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ConfirmStockAsync(Guid productVariantId, int quantity, string userId, CancellationToken cancellationToken = default)
    {
        ValidateQuantity(quantity, nameof(quantity));
        ValidateUserId(userId);

        // Check for existing transaction to allow composing this method into larger units of work
        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction == null)
        {
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            // Pessimistic locking on ProductVariant (PostgreSQL: FOR UPDATE)
            var variant = await _context.ProductVariants
                .FromSqlRaw(@"SELECT * FROM ""ProductVariants"" WHERE ""Id"" = {0} FOR UPDATE", productVariantId)
                .FirstOrDefaultAsync(cancellationToken);

            if (variant == null)
                throw new InvalidOperationException("Product Variant not found.");

            var reservation = await _context.Set<StockReservation>()
                .FirstOrDefaultAsync(r => r.ProductVariantId == productVariantId &&
                                          r.UserId == userId &&
                                          !r.IsReleased &&
                                          !r.IsConfirmed, cancellationToken);

            // RESILIENCY FIX: If reservation is missing (expired), check if we can still fulfill it directly.
            // This prevents "Payment Successful -> Order Failed" scenarios.
            if (reservation == null)
            {
                if (variant.StockQuantity < quantity)
                    throw new ReservationNotFoundException(productVariantId, userId); // Or InsufficientStockException

                // We have stock, but no reservation. Treat as direct "late" confirmation.
                // Deduct permanent stock
                int stockBeforeLog = variant.StockQuantity;
                variant.StockQuantity -= quantity;

                await LogStockChangeAsync(
                    productVariantId,
                    "Confirm_Late",
                    -quantity,
                    stockBeforeLog,
                    variant.StockQuantity,
                    userId,
                    null,
                    "Stock confirmed (Reservation expired but stock available)");
            }
            else
            {
                // Validate reservation quantity matches
                if (reservation.Quantity != quantity)
                    throw new InvalidOperationException($"Reservation quantity ({reservation.Quantity}) does not match requested quantity ({quantity})");

                // Validate stock BEFORE decrementing
                if (variant.StockQuantity < quantity)
                     throw new InsufficientStockException(productVariantId, quantity, variant.StockQuantity);

                int stockBefore = variant.StockQuantity;

                // Deduct permanent stock
                variant.StockQuantity -= quantity;

                // Mark reservation as confirmed
                reservation.IsConfirmed = true;

                // Audit log
                await LogStockChangeAsync(
                    productVariantId,
                    "Confirm",
                    -quantity,
                    stockBefore,
                    variant.StockQuantity,
                    userId,
                    reservation.Id,
                    "Stock confirmed and deducted");
            }

            await _variantRepository.UpdateAsync(variant, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<int> CleanupExpiredReservationsAsync(CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var expired = await _context.Set<StockReservation>()
                .Where(r => !r.IsReleased && !r.IsConfirmed && r.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            int count = expired.Count;

            foreach (var r in expired)
            {
                r.IsReleased = true;
            }

            if (expired.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            // Fire-and-forget audit logging AFTER transaction commits
            if (count > 0)
            {
                foreach (var r in expired)
                {
                    LogStockChangeFireAndForget(
                        r.ProductVariantId,
                        "Cleanup",
                        -r.Quantity,
                        0,
                        0,
                        r.UserId,
                        r.Id,
                        "Expired reservation auto-released");
                }
            }

            return count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }


    public async Task AdjustStockAsync(Guid productVariantId, int quantityChange, string reason, string? userId, CancellationToken cancellationToken = default)
    {
        if (quantityChange == 0) return;

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // [MANDATORY] Pessimistic locking to prevent race conditions during adjustment
            var variant = await _context.ProductVariants
                .FromSqlRaw(@"SELECT * FROM ""ProductVariants"" WHERE ""Id"" = {0} FOR UPDATE", productVariantId)
                .FirstOrDefaultAsync(cancellationToken);

            if (variant == null)
                throw new InvalidOperationException($"Product Variant {productVariantId} not found.");

            // Prevent negative physical stock
            if (variant.StockQuantity + quantityChange < 0)
                throw new InvalidOperationException($"Insufficient physical stock. Current: {variant.StockQuantity}, Requested Change: {quantityChange}");

            int stockBefore = variant.StockQuantity;
            variant.StockQuantity += quantityChange;

            // [MANDATORY] Audit log creation within the SAME transaction
            await LogStockChangeAsync(
                productVariantId,
                quantityChange > 0 ? "Adjustment_Add" : "Adjustment_Subtract",
                quantityChange,
                stockBefore,
                variant.StockQuantity,
                userId ?? "SYSTEM",
                null,
                reason);

            _context.ProductVariants.Update(variant);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
