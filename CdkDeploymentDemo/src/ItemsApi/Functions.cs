using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using ItemsApi.Models;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ItemsApi;

public class Functions
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public Functions(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "Items";
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/items")]
    public async Task<IHttpResult> GetAllItems(ILambdaContext context)
    {
        context.Logger.LogInformation("Getting all items");

        var response = await _dynamoDb.ScanAsync(new ScanRequest
        {
            TableName = _tableName
        });

        var items = response.Items.Select(MapToItem).ToList();
        return HttpResults.Ok(items);
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/items/{id}")]
    public async Task<IHttpResult> GetItem(string id, ILambdaContext context)
    {
        context.Logger.LogInformation($"Getting item {id}");

        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "Id", new AttributeValue { S = id } }
            }
        });

        if (response.Item == null || response.Item.Count == 0)
        {
            return HttpResults.NotFound();
        }

        return HttpResults.Ok(MapToItem(response.Item));
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/items")]
    public async Task<IHttpResult> CreateItem([FromBody] CreateItemRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation($"Creating item: {request.Name}");

        var item = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            CreatedAt = DateTime.UtcNow
        };

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "Id", new AttributeValue { S = item.Id } },
                { "Name", new AttributeValue { S = item.Name } },
                { "Description", new AttributeValue { S = item.Description } },
                { "Price", new AttributeValue { N = item.Price.ToString() } },
                { "CreatedAt", new AttributeValue { S = item.CreatedAt.ToString("O") } }
            }
        });

        return HttpResults.Created($"/items/{item.Id}", item);
    }

    private static Item MapToItem(Dictionary<string, AttributeValue> attributes)
    {
        return new Item
        {
            Id = attributes.GetValueOrDefault("Id")?.S ?? string.Empty,
            Name = attributes.GetValueOrDefault("Name")?.S ?? string.Empty,
            Description = attributes.GetValueOrDefault("Description")?.S ?? string.Empty,
            Price = decimal.TryParse(attributes.GetValueOrDefault("Price")?.N, out var price) ? price : 0,
            CreatedAt = DateTime.TryParse(attributes.GetValueOrDefault("CreatedAt")?.S, out var date) ? date : DateTime.MinValue
        };
    }
}
