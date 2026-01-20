using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public InventoryRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
        _tableName = Environment.GetEnvironmentVariable("INVENTORY_TABLE") ?? "Inventory";
    }

    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "ProductId", new AttributeValue { S = productId } }
            }
        });

        if (response.Item == null || response.Item.Count == 0)
            return null;

        return new InventoryItem
        {
            ProductId = productId,
            ProductName = response.Item["ProductName"].S,
            StockQuantity = int.Parse(response.Item["StockQuantity"].N),
            UnitPrice = decimal.Parse(response.Item["UnitPrice"].N)
        };
    }

    public async Task UpdateStockAsync(string productId, int newQuantity)
    {
        await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "ProductId", new AttributeValue { S = productId } }
            },
            UpdateExpression = "SET StockQuantity = :newQty",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":newQty", new AttributeValue { N = newQuantity.ToString() } }
            }
        });
    }
}
