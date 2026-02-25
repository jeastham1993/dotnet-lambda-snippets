using Amazon.Lambda.Annotations;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using SqsEventBridgeDemo.Models;

namespace SqsEventBridgeDemo.EventBridge;

// Reacts to order.placed events from EventBridge.
// Has no knowledge of the publisher or the other consumers.
public class FulfilmentFunction
{
    [LambdaFunction]
    public async Task HandleOrderPlaced(CloudWatchEvent<OrderPlacedEvent> orderEvent, ILambdaContext context)
    {
        var order = orderEvent.Detail;

        context.Logger.LogInformation(
            $"Fulfilment: reserving {order.Quantity}x {order.ProductId} for order {order.OrderId}");

        // In production: check inventory, create shipment record, arrange courier pickup, etc.
        await Task.CompletedTask;
    }
}
