using Amazon.Lambda.Annotations;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using SqsEventBridgeDemo.Models;

namespace SqsEventBridgeDemo.EventBridge;

// Reacts to order.placed events from EventBridge.
// Completely independent of FulfilmentFunction and AnalyticsFunction.
public class NotificationsFunction
{
    [LambdaFunction]
    public async Task HandleOrderPlaced(CloudWatchEvent<OrderPlacedEvent> orderEvent, ILambdaContext context)
    {
        var order = orderEvent.Detail;

        context.Logger.LogInformation(
            $"Notifications: sending order confirmation to customer {order.CustomerId} for order {order.OrderId}");

        // In production: send email confirmation, push notification, SMS alert, etc.
        await Task.CompletedTask;
    }
}
