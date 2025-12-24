using Commerce.Application.Common.Interfaces;
using Commerce.Application.Features.Products;
using Commerce.Domain.Entities.Products;

namespace Commerce.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IRepository<Product> _productRepository;

    public ProductService(IRepository<Product> productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        return await _productRepository.GetAllAsync(cancellationToken);
    }

    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _productRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Product> CreateProductAsync(string name, string description, decimal price, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Name = name,
            Description = description,
            BasePrice = price,
            Category = "General",
            IsActive = true
        };

        await _productRepository.AddAsync(product, cancellationToken);
        return product;
    }
}
