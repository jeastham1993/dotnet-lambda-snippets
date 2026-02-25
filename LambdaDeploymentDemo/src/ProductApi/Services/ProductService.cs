using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using ProductApi.Models;

namespace ProductApi.Services;

public class ProductService : IProductService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<ProductService> _logger;
    private readonly string _tableName;

    public ProductService(IAmazonDynamoDB dynamoDb, ILogger<ProductService> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;

        // Fails fast at startup if the environment variable is missing.
        // This is intentional — a missing PRODUCTS_TABLE_NAME is a deployment
        // misconfiguration, not a runtime error. Failing the cold start ensures
        // the pre-deployment validation step catches it before any traffic shifts.
        _tableName = Environment.GetEnvironmentVariable("PRODUCTS_TABLE_NAME")
            ?? throw new InvalidOperationException(
                "PRODUCTS_TABLE_NAME environment variable is not set. " +
                "Check the Lambda function configuration.");
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        var response = await _dynamoDb.ScanAsync(new ScanRequest { TableName = _tableName });
        return response.Items.Select(MapToProduct);
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["id"] = new AttributeValue { S = id },
            },
        });

        return response.Item.Count == 0 ? null : MapToProduct(response.Item);
    }

    public async Task<Product> CreateAsync(CreateProductRequest request)
    {
        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Price = request.Price,
            Category = request.Category,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["id"] = new AttributeValue { S = product.Id },
                ["name"] = new AttributeValue { S = product.Name },
                ["price"] = new AttributeValue { N = product.Price.ToString() },
                ["category"] = new AttributeValue { S = product.Category },
                ["createdAt"] = new AttributeValue { S = product.CreatedAt.ToString("O") },
            },
        });

        _logger.LogInformation("Created product {ProductId}", product.Id);
        return product;
    }

    private static Product MapToProduct(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item["id"].S,
        Name = item["name"].S,
        Price = decimal.Parse(item["price"].N),
        Category = item["category"].S,
        CreatedAt = DateTimeOffset.Parse(item["createdAt"].S),
    };
}
