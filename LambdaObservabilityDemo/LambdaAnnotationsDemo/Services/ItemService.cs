using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LambdaAnnotationsDemo.Models;

namespace LambdaAnnotationsDemo.Services;

public class ItemService : IItemService
{
    private readonly IAmazonDynamoDB _dynamoDB;
    private readonly string _tableName = Environment.GetEnvironmentVariable("ITEMS_TABLE_NAME") ?? "Items";

    public ItemService(IAmazonDynamoDB dynamoDB)
    {
        _dynamoDB = dynamoDB;
    }

    public async Task<Item> CreateItem(CreateItemRequest request)
    {
        using var activity = Observability.Source.StartActivity("item.create");

        var item = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        activity?.SetTag("item.id", item.Id);
        activity?.SetTag("item.name", item.Name);

        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new AttributeValue { S = item.Id },
            ["Name"] = new AttributeValue { S = item.Name },
            ["Description"] = new AttributeValue { S = string.IsNullOrEmpty(item.Description) ? " " : item.Description },
            ["CreatedAt"] = new AttributeValue { S = item.CreatedAt.ToString("O") }
        };

        await _dynamoDB.PutItemAsync(_tableName, attributes);
        return item;
    }

    public async Task<Item?> GetItem(string id)
    {
        using var activity = Observability.Source.StartActivity("item.get");
        activity?.SetTag("item.id", id);

        var response = await _dynamoDB.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["Id"] = new AttributeValue { S = id }
        });

        if (!response.IsItemSet)
            return null;

        return MapToItem(response.Item);
    }

    public async Task<IEnumerable<Item>> GetAllItems()
    {
        using var activity = Observability.Source.StartActivity("item.list");

        var response = await _dynamoDB.ScanAsync(new ScanRequest { TableName = _tableName });

        return response.Items.Select(MapToItem);
    }

    private static Item MapToItem(Dictionary<string, AttributeValue> attrs) => new()
    {
        Id = attrs["Id"].S,
        Name = attrs["Name"].S,
        Description = attrs.TryGetValue("Description", out var desc) ? desc.S : string.Empty,
        CreatedAt = DateTime.Parse(attrs["CreatedAt"].S)
    };
}
