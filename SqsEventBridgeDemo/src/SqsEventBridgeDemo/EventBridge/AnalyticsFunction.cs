using Amazon.Lambda.Annotations;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using SqsEventBridgeDemo.Models;

namespace SqsEventBridgeDemo.EventBridge;

// Reacts to order.placed events from EventBridge.
// Completely independent of FulfilmentFunction and NotificationsFunction.
public class AnalyticsFunction
{
    [LambdaFunction]
    public async Task HandleOrderPlaced(CloudWatchEvent<OrderPlacedEvent> orderEvent, ILambdaContext context)
    {
        var order = orderEvent.Detail;

        context.Logger.LogInformation(
            $"Analytics: recording order {order.OrderId} — customer {order.CustomerId}, " +
            $"product {order.ProductId} x{order.Quantity}, total {order.TotalAmount:C}");

        // In production: write to analytics store, update dashboards, feed ML pipelines, etc.
        await Task.CompletedTask;
    }
}
