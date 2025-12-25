using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Inventory;
using Commerce.Domain.Entities.Products;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using InventoryEntities = Commerce.Domain.Entities.Inventory;

namespace Commerce.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly CommerceDbContext _context;
    private readonly IRepository<InventoryEntities.StockReservation> _reservationRepository;
    private readonly IRepository<ProductVariant> _variantRepository;

    public InventoryService(
        CommerceDbContext context,
        IRepository<InventoryEntities.StockReservation> reservationRepository,
        IRepository<ProductVariant> variantRepository)
    {
        _context = context;
        _reservationRepository = reservationRepository;
        _variantRepository = variantRepository;
    }

    public async Task<int> GetAvailableStockAsync(Guid productVariantId, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetByIdAsync(productVariantId, cancellationToken);
        if (variant == null) return 0;
        
        // Sum active reservations
        var reservedQuantity = await _context.Set<InventoryEntities.StockReservation>()
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
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var available = await GetAvailableStockAsync(productVariantId, cancellationToken);
            
            var existingReservation = await _context.Set<InventoryEntities.StockReservation>()
                .FirstOrDefaultAsync(r => r.ProductVariantId == productVariantId && 
                                          r.UserId == userId && 
                                          !r.IsReleased && 
                                          !r.IsConfirmed, cancellationToken);
                                          
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
                return false; // Not enough stock
            }

            if (existingReservation != null)
            {
                existingReservation.Quantity = quantity;
                existingReservation.ExpiresAt = DateTime.UtcNow.Add(duration);
                _context.Set<InventoryEntities.StockReservation>().Update(existingReservation);
            }
            else
            {
                var reservation = new InventoryEntities.StockReservation
                {
                    Id = Guid.NewGuid(),
                    ProductVariantId = productVariantId,
                    Quantity = quantity,
                    UserId = userId,
                    ExpiresAt = DateTime.UtcNow.Add(duration),
                    IsReleased = false,
                    IsConfirmed = false
                };
                await _context.Set<InventoryEntities.StockReservation>().AddAsync(reservation, cancellationToken);
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

    public async Task ReleaseReservationAsync(Guid productVariantId, string userId, CancellationToken cancellationToken = default)
    {
        var reservation = await _context.Set<InventoryEntities.StockReservation>()
            .FirstOrDefaultAsync(r => r.ProductVariantId == productVariantId && 
                                      r.UserId == userId && 
                                      !r.IsReleased && 
                                      !r.IsConfirmed, cancellationToken);

        if (reservation != null)
        {
            reservation.IsReleased = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ConfirmStockAsync(Guid productVariantId, int quantity, string userId, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var reservation = await _context.Set<InventoryEntities.StockReservation>()
                .FirstOrDefaultAsync(r => r.ProductVariantId == productVariantId && 
                                          r.UserId == userId && 
                                          !r.IsReleased && 
                                          !r.IsConfirmed, cancellationToken);

            var variant = await _variantRepository.GetByIdAsync(productVariantId, cancellationToken);
            if (variant == null) throw new InvalidOperationException("Product Variant not found.");

            // Deduct permanent stock
            variant.StockQuantity -= quantity;
            if (variant.StockQuantity < 0) variant.StockQuantity = 0;

            if (reservation != null)
            {
                reservation.IsConfirmed = true;
            }
            else
            {
                if (variant.StockQuantity < 0) 
                    throw new InvalidOperationException("Insufficient stock to confirm without reservation.");
            }

            await _variantRepository.UpdateAsync(variant, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task CleanupExpiredReservationsAsync(CancellationToken cancellationToken = default)
    {
        var expired = await _context.Set<InventoryEntities.StockReservation>()
            .Where(r => !r.IsReleased && !r.IsConfirmed && r.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var r in expired)
        {
            r.IsReleased = true;
        }
        
        if (expired.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
