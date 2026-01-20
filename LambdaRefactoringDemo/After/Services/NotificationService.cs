using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Services;

public class NotificationService : INotificationService
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly string? _topicArn;

    public NotificationService(IAmazonSimpleNotificationService sns)
    {
        _sns = sns;
        _topicArn = Environment.GetEnvironmentVariable("NOTIFICATION_TOPIC_ARN");
    }

    public async Task SendOrderConfirmationAsync(Order order)
    {
        if (string.IsNullOrEmpty(_topicArn))
            return;

        var message = new
        {
            orderId = order.OrderId,
            customerId = order.CustomerId,
            email = order.Email,
            totalAmount = order.TotalAmount,
            itemCount = order.Items.Count,
            orderDate = order.OrderDate,
            status = order.Status
        };

        await _sns.PublishAsync(new PublishRequest
        {
            TopicArn = _topicArn,
            Subject = $"Order Confirmation - {order.OrderId}",
            Message = JsonSerializer.Serialize(message)
        });
    }
}
