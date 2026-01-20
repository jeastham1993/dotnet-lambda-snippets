using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Repositories;

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
        var item = new Dictionary<string, AttributeValue>
        {
            { "OrderId", new AttributeValue { S = order.OrderId } },
            { "CustomerId", new AttributeValue { S = order.CustomerId } },
            { "OrderDate", new AttributeValue { S = order.OrderDate.ToString("O") } },
            { "Status", new AttributeValue { S = order.Status } },
            { "Subtotal", new AttributeValue { N = order.Subtotal.ToString() } },
            { "DiscountPercent", new AttributeValue { N = order.DiscountPercent.ToString() } },
            { "DiscountAmount", new AttributeValue { N = order.DiscountAmount.ToString() } },
            { "TaxAmount", new AttributeValue { N = order.TaxAmount.ToString() } },
            { "TotalAmount", new AttributeValue { N = order.TotalAmount.ToString() } },
            { "ItemCount", new AttributeValue { N = order.Items.Count.ToString() } },
            { "Items", new AttributeValue
                {
                    L = order.Items.Select(line => new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            { "ProductId", new AttributeValue { S = line.ProductId } },
                            { "ProductName", new AttributeValue { S = line.ProductName } },
                            { "Quantity", new AttributeValue { N = line.Quantity.ToString() } },
                            { "UnitPrice", new AttributeValue { N = line.UnitPrice.ToString() } },
                            { "LineTotal", new AttributeValue { N = line.LineTotal.ToString() } }
                        }
                    }).ToList()
                }
            }
        };

        if (!string.IsNullOrEmpty(order.Email))
            item["Email"] = new AttributeValue { S = order.Email };

        if (!string.IsNullOrEmpty(order.ShippingAddress))
            item["ShippingAddress"] = new AttributeValue { S = order.ShippingAddress };

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }
}
