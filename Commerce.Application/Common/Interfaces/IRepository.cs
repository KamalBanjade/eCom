// File: Commerce.Application/Common/Interfaces/IRepository.cs
using System.Linq.Expressions;

namespace Commerce.Application.Common.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes);

    Task<IEnumerable<T>> GetAllAsync(
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes);

    Task<IEnumerable<T>> GetAsync(
        Expression<Func<T, bool>>? filter = null,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes);

    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}