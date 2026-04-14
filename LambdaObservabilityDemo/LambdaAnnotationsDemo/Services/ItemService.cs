using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LambdaAnnotationsDemo.Models;
using System.Diagnostics;

namespace LambdaAnnotationsDemo.Services;

public class ItemService : IItemService
{
    private readonly IAmazonDynamoDB _dynamoDB;
    private readonly ActivitySource _activitySource;
    private readonly string _tableName =
        Environment.GetEnvironmentVariable("ITEMS_TABLE_NAME")
        ?? throw new InvalidOperationException(
            "ITEMS_TABLE_NAME environment variable is required. Set it to the DynamoDB table name.");

    public ItemService(IAmazonDynamoDB dynamoDB, ActivitySource activitySource)
    {
        _dynamoDB = dynamoDB;
        _activitySource = activitySource;
    }

    public async Task<Item> CreateItem(CreateItemRequest request)
    {
        using var activity = _activitySource.StartActivity("item.create");

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
            ["CreatedAt"] = new AttributeValue { S = item.CreatedAt.ToString("O") }
        };

        if (!string.IsNullOrEmpty(item.Description))
            attributes["Description"] = new AttributeValue { S = item.Description };

        await _dynamoDB.PutItemAsync(_tableName, attributes);
        return item;
    }

    public async Task<Item?> GetItem(string id)
    {
        using var activity = _activitySource.StartActivity("item.get");
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
        using var activity = _activitySource.StartActivity("item.list");

        var results = new List<Item>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _dynamoDB.ScanAsync(new ScanRequest
            {
                TableName = _tableName,
                ExclusiveStartKey = lastKey?.Count > 0 ? lastKey : null
            });
            results.AddRange(response.Items.Select(MapToItem));
            lastKey = response.LastEvaluatedKey;
        } while (lastKey?.Count > 0);

        return results;
    }

    private static Item MapToItem(Dictionary<string, AttributeValue> attrs)
    {
        if (!attrs.TryGetValue("Id", out var id) ||
            !attrs.TryGetValue("Name", out var name) ||
            !attrs.TryGetValue("CreatedAt", out var createdAt))
            throw new InvalidOperationException("DynamoDB item is missing required attributes (Id, Name, CreatedAt).");

        return new Item
        {
            Id = id.S,
            Name = name.S,
            Description = attrs.TryGetValue("Description", out var desc) ? desc.S : string.Empty,
            CreatedAt = DateTime.ParseExact(createdAt.S, "O", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }
}
