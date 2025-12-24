using Commerce.Application.Common.DTOs;
using Commerce.Domain.Entities.Products;

namespace Commerce.Application.Features.Products;

public interface IProductService
{
    Task<IEnumerable<Product>> GetAllProductsAsync(CancellationToken cancellationToken = default);
    Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product> CreateProductAsync(string name, string description, decimal price, CancellationToken cancellationToken = default);
}
