// Commerce.Infrastructure/Repositories/Repository.cs
using Commerce.Application.Common.Interfaces;
using Commerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Commerce.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly CommerceDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(CommerceDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        if (includes != null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        return await query.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken);
    }

    public async Task<IEnumerable<T>> GetAllAsync(
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        if (includes != null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }

public async Task<IEnumerable<T>> GetAsync(
    Expression<Func<T, bool>>? filter = null,
    CancellationToken cancellationToken = default,
    params Expression<Func<T, object>>[] includes)
{
    IQueryable<T> query = _dbSet;

    if (includes?.Length > 0)
    {
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
    }

    if (filter != null)
    {
        query = query.Where(filter);
    }

    return await query.ToListAsync(cancellationToken);
}

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    // Add this if you want explicit save
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}