using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using SqsPollingDemo.Models;

namespace SqsPollingDemo;

// Lambda receives a batch of SQS messages and calls this function.
// No polling loop. No deletion. No sleep intervals. No manual scaling.
// You write the business logic — AWS handles the rest.
public class OrderProcessorFunction
{
    [LambdaFunction]
    public async Task<SQSBatchResponse> ProcessOrders(SQSEvent sqsEvent, ILambdaContext context)
    {
        var batchItemFailures = new List<SQSBatchResponse.BatchItemFailure>();

        // Lambda delivers messages in batches — iterate and process each one.
        foreach (var record in sqsEvent.Records)
        {
            try
            {
                var order = JsonSerializer.Deserialize<OrderMessage>(record.Body)
                    ?? throw new InvalidOperationException("Failed to deserialise order message");

                context.Logger.LogInformation(
                    $"Processing order {order.OrderId} for customer {order.CustomerId}");

                await ProcessOrderAsync(order, context);
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Failed to process message {record.MessageId}: {ex.Message}");

                // Report this message as failed. SQS will retry it.
                // Every other message in this batch that succeeded stays processed.
                // You do not need to retry the whole batch.
                batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
        }

        // Return the list of failures. An empty list means everything succeeded.
        return new SQSBatchResponse { BatchItemFailures = batchItemFailures };
    }

    private static Task ProcessOrderAsync(OrderMessage order, ILambdaContext context)
    {
        // Your business logic here:
        // - Reserve inventory
        // - Write to database
        // - Trigger fulfilment
        // - Send confirmation email
        context.Logger.LogInformation(
            $"Order {order.OrderId} processed — product {order.ProductId} x{order.Quantity}, total {order.TotalAmount:C}");

        return Task.CompletedTask;
    }
}
