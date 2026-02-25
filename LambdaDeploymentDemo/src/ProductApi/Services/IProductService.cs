using ProductApi.Models;

namespace ProductApi.Services;

public interface IProductService
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(string id);
    Task<Product> CreateAsync(CreateProductRequest request);
}
