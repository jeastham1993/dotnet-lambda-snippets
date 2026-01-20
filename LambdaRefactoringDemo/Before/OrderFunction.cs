using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaRefactoringDemo.Before;

public class OrderFunction
{
    private readonly AmazonDynamoDBClient _dynamoDbClient;
    private readonly AmazonSimpleNotificationServiceClient _snsClient;
    private readonly string _ordersTableName;
    private readonly string _inventoryTableName;
    private readonly string _notificationTopicArn;

    public OrderFunction()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
        _snsClient = new AmazonSimpleNotificationServiceClient();
        _ordersTableName = Environment.GetEnvironmentVariable("ORDERS_TABLE") ?? "Orders";
        _inventoryTableName = Environment.GetEnvironmentVariable("INVENTORY_TABLE") ?? "Inventory";
        _notificationTopicArn = Environment.GetEnvironmentVariable("NOTIFICATION_TOPIC_ARN") ?? "";
    }

    public async Task<APIGatewayProxyResponse> ProcessOrder(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation("Starting order processing");

        // ============================================
        // INPUT VALIDATION - Lines 35-95
        // ============================================
        if (string.IsNullOrEmpty(request.Body))
        {
            context.Logger.LogError("Request body is null or empty");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = JsonSerializer.Serialize(new { error = "Request body is required" }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        OrderRequest? orderRequest;
        try
        {
            orderRequest = JsonSerializer.Deserialize<OrderRequest>(request.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            context.Logger.LogError($"Failed to parse request body: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = JsonSerializer.Serialize(new { error = "Invalid JSON format" }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        if (orderRequest == null)
        {
            context.Logger.LogError("Deserialized order request is null");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = JsonSerializer.Serialize(new { error = "Invalid order request" }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        if (string.IsNullOrWhiteSpace(orderRequest.CustomerId))
        {
            context.Logger.LogError("CustomerId is missing");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = JsonSerializer.Serialize(new { error = "CustomerId is required" }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        if (orderRequest.Items == null || orderRequest.Items.Count == 0)
        {
            context.Logger.LogError("Order has no items");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = JsonSerializer.Serialize(new { error = "At least one item is required" }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        foreach (var item in orderRequest.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId))
            {
                context.Logger.LogError("Item has missing ProductId");
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = JsonSerializer.Serialize(new { error = "All items must have a ProductId" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }

            if (item.Quantity <= 0)
            {
                context.Logger.LogError($"Item {item.ProductId} has invalid quantity: {item.Quantity}");
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = JsonSerializer.Serialize(new { error = $"Item {item.ProductId} must have quantity greater than 0" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }
        }

        if (!string.IsNullOrEmpty(orderRequest.Email) && !orderRequest.Email.Contains("@"))
        {
            context.Logger.LogError("Invalid email format");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = JsonSerializer.Serialize(new { error = "Invalid email format" }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        context.Logger.LogInformation($"Validation passed for customer {orderRequest.CustomerId} with {orderRequest.Items.Count} items");

        // ============================================
        // INVENTORY CHECK - Lines 140-210
        // ============================================
        var inventoryItems = new Dictionary<string, InventoryItem>();

        foreach (var item in orderRequest.Items)
        {
            context.Logger.LogInformation($"Checking inventory for product {item.ProductId}");

            var getItemRequest = new GetItemRequest
            {
                TableName = _inventoryTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "ProductId", new AttributeValue { S = item.ProductId } }
                }
            };

            GetItemResponse inventoryResponse;
            try
            {
                inventoryResponse = await _dynamoDbClient.GetItemAsync(getItemRequest);
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"DynamoDB error checking inventory: {ex.Message}");
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = JsonSerializer.Serialize(new { error = "Failed to check inventory" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }

            if (inventoryResponse.Item == null || inventoryResponse.Item.Count == 0)
            {
                context.Logger.LogError($"Product {item.ProductId} not found in inventory");
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = JsonSerializer.Serialize(new { error = $"Product {item.ProductId} not found" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }

            var stockQuantity = int.Parse(inventoryResponse.Item["StockQuantity"].N);
            var unitPrice = decimal.Parse(inventoryResponse.Item["UnitPrice"].N);
            var productName = inventoryResponse.Item["ProductName"].S;

            if (stockQuantity < item.Quantity)
            {
                context.Logger.LogError($"Insufficient stock for {item.ProductId}. Requested: {item.Quantity}, Available: {stockQuantity}");
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = JsonSerializer.Serialize(new { error = $"Insufficient stock for {productName}. Available: {stockQuantity}" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }

            inventoryItems[item.ProductId] = new InventoryItem
            {
                ProductId = item.ProductId,
                ProductName = productName,
                StockQuantity = stockQuantity,
                UnitPrice = unitPrice
            };

            context.Logger.LogInformation($"Inventory check passed for {item.ProductId}: {stockQuantity} in stock at ${unitPrice}");
        }

        // ============================================
        // PRICING CALCULATION - Lines 215-280
        // ============================================
        context.Logger.LogInformation("Calculating order total");

        decimal subtotal = 0;
        var orderLines = new List<OrderLine>();

        foreach (var item in orderRequest.Items)
        {
            var inventory = inventoryItems[item.ProductId];
            var lineTotal = inventory.UnitPrice * item.Quantity;
            subtotal += lineTotal;

            orderLines.Add(new OrderLine
            {
                ProductId = item.ProductId,
                ProductName = inventory.ProductName,
                Quantity = item.Quantity,
                UnitPrice = inventory.UnitPrice,
                LineTotal = lineTotal
            });

            context.Logger.LogInformation($"Line item: {inventory.ProductName} x {item.Quantity} = ${lineTotal}");
        }

        decimal discountPercent = 0;
        if (subtotal >= 1000)
        {
            discountPercent = 0.15m;
            context.Logger.LogInformation("Applied 15% discount for orders over $1000");
        }
        else if (subtotal >= 500)
        {
            discountPercent = 0.10m;
            context.Logger.LogInformation("Applied 10% discount for orders over $500");
        }
        else if (subtotal >= 100)
        {
            discountPercent = 0.05m;
            context.Logger.LogInformation("Applied 5% discount for orders over $100");
        }

        var discountAmount = subtotal * discountPercent;
        var taxRate = 0.08m;
        var taxableAmount = subtotal - discountAmount;
        var taxAmount = taxableAmount * taxRate;
        var totalAmount = taxableAmount + taxAmount;

        context.Logger.LogInformation($"Subtotal: ${subtotal}, Discount: ${discountAmount}, Tax: ${taxAmount}, Total: ${totalAmount}");

        // ============================================
        // CREATE ORDER IN DATABASE - Lines 285-360
        // ============================================
        var orderId = Guid.NewGuid().ToString();
        var orderDate = DateTime.UtcNow;

        context.Logger.LogInformation($"Creating order {orderId}");

        var orderItem = new Dictionary<string, AttributeValue>
        {
            { "OrderId", new AttributeValue { S = orderId } },
            { "CustomerId", new AttributeValue { S = orderRequest.CustomerId } },
            { "OrderDate", new AttributeValue { S = orderDate.ToString("O") } },
            { "Status", new AttributeValue { S = "CONFIRMED" } },
            { "Subtotal", new AttributeValue { N = subtotal.ToString() } },
            { "DiscountPercent", new AttributeValue { N = discountPercent.ToString() } },
            { "DiscountAmount", new AttributeValue { N = discountAmount.ToString() } },
            { "TaxAmount", new AttributeValue { N = taxAmount.ToString() } },
            { "TotalAmount", new AttributeValue { N = totalAmount.ToString() } },
            { "ItemCount", new AttributeValue { N = orderRequest.Items.Count.ToString() } },
            { "Items", new AttributeValue
                {
                    L = orderLines.Select(line => new AttributeValue
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

        if (!string.IsNullOrEmpty(orderRequest.Email))
        {
            orderItem["Email"] = new AttributeValue { S = orderRequest.Email };
        }

        if (!string.IsNullOrEmpty(orderRequest.ShippingAddress))
        {
            orderItem["ShippingAddress"] = new AttributeValue { S = orderRequest.ShippingAddress };
        }

        try
        {
            await _dynamoDbClient.PutItemAsync(new PutItemRequest
            {
                TableName = _ordersTableName,
                Item = orderItem
            });
            context.Logger.LogInformation($"Order {orderId} saved to database");
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Failed to save order: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = JsonSerializer.Serialize(new { error = "Failed to create order" }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        // ============================================
        // UPDATE INVENTORY - Lines 365-400
        // ============================================
        foreach (var item in orderRequest.Items)
        {
            var inventory = inventoryItems[item.ProductId];
            var newQuantity = inventory.StockQuantity - item.Quantity;

            context.Logger.LogInformation($"Updating inventory for {item.ProductId}: {inventory.StockQuantity} -> {newQuantity}");

            try
            {
                await _dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = _inventoryTableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "ProductId", new AttributeValue { S = item.ProductId } }
                    },
                    UpdateExpression = "SET StockQuantity = :newQty",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":newQty", new AttributeValue { N = newQuantity.ToString() } }
                    }
                });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Failed to update inventory for {item.ProductId}: {ex.Message}");
            }
        }

        // ============================================
        // SEND NOTIFICATIONS - Lines 405-470
        // ============================================
        if (!string.IsNullOrEmpty(_notificationTopicArn))
        {
            context.Logger.LogInformation("Sending order confirmation notification");

            var notificationMessage = new
            {
                orderId = orderId,
                customerId = orderRequest.CustomerId,
                email = orderRequest.Email,
                totalAmount = totalAmount,
                itemCount = orderRequest.Items.Count,
                orderDate = orderDate,
                status = "CONFIRMED"
            };

            try
            {
                await _snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = _notificationTopicArn,
                    Subject = $"Order Confirmation - {orderId}",
                    Message = JsonSerializer.Serialize(notificationMessage)
                });
                context.Logger.LogInformation("Notification sent successfully");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Failed to send notification: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(orderRequest.Email))
        {
            context.Logger.LogInformation($"Would send email confirmation to {orderRequest.Email}");
        }

        // ============================================
        // BUILD RESPONSE - Lines 475-500
        // ============================================
        context.Logger.LogInformation($"Order {orderId} processing complete");

        var response = new OrderResponse
        {
            OrderId = orderId,
            CustomerId = orderRequest.CustomerId,
            Status = "CONFIRMED",
            OrderDate = orderDate,
            Items = orderLines,
            Subtotal = subtotal,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount
        };

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.Created,
            Body = JsonSerializer.Serialize(response),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }
}

// ============================================
// MODELS EMBEDDED IN SAME FILE - Lines 505-580
// ============================================
public class OrderRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ShippingAddress { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class InventoryItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderLine
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderResponse
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public List<OrderLine> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
