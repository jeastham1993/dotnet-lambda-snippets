using System.Net;
using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using ProductApi.Models;
using ProductApi.Services;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ProductApi;

public class Functions
{
    private readonly IProductService _productService;
    private readonly ILogger<Functions> _logger;

    public Functions(IProductService productService, ILogger<Functions> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products")]
    public async Task<IEnumerable<Product>> GetProducts(ILambdaContext context)
    {
        _logger.LogInformation("Listing all products");
        return await _productService.GetAllAsync();
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/products/{id}")]
    public async Task<APIGatewayHttpApiV2ProxyResponse> GetProduct(string id, ILambdaContext context)
    {
        _logger.LogInformation("Getting product {ProductId}", id);

        var product = await _productService.GetByIdAsync(id);

        return product is null
            ? NotFound($"Product '{id}' not found.")
            : Ok(product);
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/products")]
    public async Task<APIGatewayHttpApiV2ProxyResponse> CreateProduct(
        [FromBody] CreateProductRequest request,
        ILambdaContext context)
    {
        _logger.LogInformation("Creating product {ProductName}", request.Name);

        var product = await _productService.CreateAsync(request);
        return Created(product);
    }

    private static APIGatewayHttpApiV2ProxyResponse Ok(object body) =>
        Response(HttpStatusCode.OK, body);

    private static APIGatewayHttpApiV2ProxyResponse Created(object body) =>
        Response(HttpStatusCode.Created, body);

    private static APIGatewayHttpApiV2ProxyResponse NotFound(string message) =>
        Response(HttpStatusCode.NotFound, new { message });

    private static APIGatewayHttpApiV2ProxyResponse Response(HttpStatusCode statusCode, object body) => new()
    {
        StatusCode = (int)statusCode,
        Body = JsonSerializer.Serialize(body),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
    };
}
