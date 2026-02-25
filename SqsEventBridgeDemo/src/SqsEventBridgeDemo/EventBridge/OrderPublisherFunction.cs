using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using SqsEventBridgeDemo.Models;

namespace SqsEventBridgeDemo.EventBridge;

// Publishes an order.placed event to EventBridge and returns immediately.
// This function has no knowledge of who will react to the event —
// Fulfilment, Notifications, and Analytics all subscribe independently via routing rules.
public class OrderPublisherFunction(IAmazonEventBridge eventBridgeClient)
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/eventbridge/orders")]
    public async Task<IHttpResult> PlaceOrder([FromBody] OrderRequest request, ILambdaContext context)
    {
        var orderId = Guid.NewGuid().ToString();
        context.Logger.LogInformation($"Publishing order.placed event for order {orderId}");

        var orderEvent = new OrderPlacedEvent(
            orderId,
            request.CustomerId,
            request.ProductId,
            request.Quantity,
            TotalAmount: 99.99m,
            PlacedAt: DateTime.UtcNow);

        await eventBridgeClient.PutEventsAsync(new PutEventsRequest
        {
            Entries =
            [
                new PutEventsRequestEntry
                {
                    EventBusName = Environment.GetEnvironmentVariable("EVENT_BUS_NAME"),
                    Source = "order-service",
                    DetailType = "order.placed",
                    Detail = JsonSerializer.Serialize(orderEvent)
                }
            ]
        });

        // All three consumers react to the same event in parallel —
        // zero coupling between them, and none of them know about each other.
        return HttpResults.Ok(new { OrderId = orderId, Status = "PLACED" });
    }
}
