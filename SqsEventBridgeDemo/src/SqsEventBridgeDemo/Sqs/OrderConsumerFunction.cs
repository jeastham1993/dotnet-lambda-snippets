using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using SqsEventBridgeDemo.Models;

namespace SqsEventBridgeDemo.Sqs;

// Consumes order messages from SQS. Lambda invokes this with a batch of messages.
// Partial batch failure support means only failed messages are retried —
// successfully processed messages in the same batch are not reprocessed.
public class OrderConsumerFunction
{
    [LambdaFunction]
    public async Task<SQSBatchResponse> ProcessOrders(SQSEvent sqsEvent, ILambdaContext context)
    {
        var batchItemFailures = new List<SQSBatchResponse.BatchItemFailure>();

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

                // Report as a batch item failure — SQS will retry this message.
                // Other messages in the batch that succeeded are not retried.
                batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
        }

        return new SQSBatchResponse { BatchItemFailures = batchItemFailures };
    }

    private static Task ProcessOrderAsync(OrderMessage order, ILambdaContext context)
    {
        // In production: reserve inventory, write to database, trigger fulfilment, etc.
        context.Logger.LogInformation(
            $"Order {order.OrderId} processed — product {order.ProductId} x{order.Quantity}, total {order.TotalAmount:C}");
        return Task.CompletedTask;
    }
}
