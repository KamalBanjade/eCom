// File: Commerce.Infrastructure/Data/UnitOfWork.cs
using Commerce.Application.Common.Interfaces;
using Commerce.Infrastructure.Data;

namespace Commerce.Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly CommerceDbContext _context;

    public UnitOfWork(CommerceDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}