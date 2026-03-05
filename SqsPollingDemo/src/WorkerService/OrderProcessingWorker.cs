using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using WorkerService.Models;

namespace WorkerService;

// A long-running background service that continuously polls SQS for messages.
// You write the loop. You manage concurrency. You pay for idle time.
public class OrderProcessingWorker(
    IAmazonSQS sqsClient,
    ILogger<OrderProcessingWorker> logger) : BackgroundService
{
    private readonly string _queueUrl = Environment.GetEnvironmentVariable("ORDER_QUEUE_URL")
        ?? throw new InvalidOperationException("ORDER_QUEUE_URL environment variable is required");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Order processing worker started. Polling {QueueUrl}", _queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Starting long poll. Waiting for messages...");
                // Poll the queue — your code is always running, always asking "is there work?"
                var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20,     // Long polling — waits up to 20s for messages
                    VisibilityTimeout = 30,   // How long you have to process before it reappears
                }, stoppingToken);

                if (response.Messages is not { Count: > 0 })
                {
                    // No work. Still running. Still costing money.
                    logger.LogInformation("No messages. Waiting...");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                logger.LogInformation("Received {Count} messages", response.Messages.Count);

                foreach (var message in response.Messages)
                {
                    try
                    {
                        await ProcessMessageAsync(message, stoppingToken);

                        // You must manually delete processed messages.
                        // Forget this and the message reappears after the visibility timeout.
                        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                        {
                            QueueUrl = _queueUrl,
                            ReceiptHandle = message.ReceiptHandle
                        }, stoppingToken);

                        logger.LogInformation("Processed and deleted message {MessageId}", message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        // Don't delete — the message will become visible again
                        // after the visibility timeout and will be retried.
                        logger.LogError(ex, "Failed to process message {MessageId}. It will be retried.",
                            message.MessageId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — exit the loop cleanly
                break;
            }
            catch (Exception ex)
            {
                // Something went wrong with the poll itself — back off and try again
                logger.LogError(ex, "Error polling SQS. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Order processing worker stopped");
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var order = JsonSerializer.Deserialize<OrderMessage>(message.Body)
            ?? throw new InvalidOperationException($"Failed to deserialise message {message.MessageId}");

        logger.LogInformation(
            "Processing order {OrderId} for customer {CustomerId} — product {ProductId} x{Quantity}, total {TotalAmount:C}",
            order.OrderId, order.CustomerId, order.ProductId, order.Quantity, order.TotalAmount);

        // Your business logic here:
        // - Reserve inventory
        // - Write to database
        // - Trigger fulfilment
        // - Send confirmation email

        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

        logger.LogInformation("Order {OrderId} processed successfully", order.OrderId);
    }
}