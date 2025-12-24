// File: Commerce.Application/Common/Interfaces/IUnitOfWork.cs
using System.Threading;
using System.Threading.Tasks;

namespace Commerce.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}