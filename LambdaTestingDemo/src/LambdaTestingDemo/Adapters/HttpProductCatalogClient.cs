using System.Net.Http.Json;
using LambdaTestingDemo.Models;

namespace LambdaTestingDemo.Adapters;

public class HttpProductCatalogClient : IProductCatalogClient
{
    private readonly HttpClient _http;

    public HttpProductCatalogClient(HttpClient http)
    {
        _http = http;
    }

    public Task<ProductDetails?> GetProductAsync(string productId)
    {
        return _http.GetFromJsonAsync<ProductDetails>($"/products/{productId}");
    }
}
