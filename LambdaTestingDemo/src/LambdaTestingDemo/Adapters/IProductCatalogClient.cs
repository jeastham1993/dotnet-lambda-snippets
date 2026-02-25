using LambdaTestingDemo.Models;

namespace LambdaTestingDemo.Adapters;

public interface IProductCatalogClient
{
    Task<ProductDetails?> GetProductAsync(string productId);
}
