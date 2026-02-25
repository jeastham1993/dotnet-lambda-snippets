using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LambdaTestingDemo.Models;

namespace LambdaTestingDemo.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public OrderRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
        _tableName = Environment.GetEnvironmentVariable("ORDERS_TABLE") ?? "Orders";
    }

    public async Task SaveAsync(Order order)
    {
        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "OrderId",     new AttributeValue { S = order.OrderId } },
                { "CustomerId",  new AttributeValue { S = order.CustomerId } },
                { "Status",      new AttributeValue { S = order.Status } },
                { "CreatedAt",   new AttributeValue { S = order.CreatedAt.ToString("O") } },
                { "TotalAmount", new AttributeValue { N = order.TotalAmount.ToString() } },
                { "Items",       new AttributeValue { S = JsonSerializer.Serialize(order.Items) } }
            }
        });
    }

    public async Task<Order?> GetByIdAsync(string orderId)
    {
        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "OrderId", new AttributeValue { S = orderId } }
            }
        });

        if (response.Item == null || response.Item.Count == 0)
            return null;

        return new Order
        {
            OrderId = response.Item["OrderId"].S,
            CustomerId = response.Item["CustomerId"].S,
            Status = response.Item["Status"].S,
            CreatedAt = DateTime.Parse(response.Item["CreatedAt"].S),
            TotalAmount = decimal.Parse(response.Item["TotalAmount"].N),
            Items = JsonSerializer.Deserialize<List<EnrichedOrderLine>>(response.Item["Items"].S) ?? new()
        };
    }
}
