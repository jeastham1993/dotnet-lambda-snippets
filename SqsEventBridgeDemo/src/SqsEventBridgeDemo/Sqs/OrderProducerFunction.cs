using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using SqsEventBridgeDemo.Models;

namespace SqsEventBridgeDemo.Sqs;

// Produces order messages to an SQS queue and returns immediately.
// The producer has no knowledge of how or when the order will be processed —
// downstream slowness is completely invisible here.
public class OrderProducerFunction(IAmazonSQS sqsClient)
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/sqs/orders")]
    public async Task<IHttpResult> PlaceOrder([FromBody] OrderRequest request, ILambdaContext context)
    {
        var orderId = Guid.NewGuid().ToString();
        context.Logger.LogInformation($"Queuing order {orderId} for customer {request.CustomerId}");

        var message = new OrderMessage(
            orderId,
            request.CustomerId,
            request.ProductId,
            request.Quantity,
            TotalAmount: 99.99m);

        await sqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = Environment.GetEnvironmentVariable("ORDER_QUEUE_URL"),
            MessageBody = JsonSerializer.Serialize(message)
        });

        // Returns immediately — producer is decoupled from the consumer.
        // SQS absorbs burst traffic; the consumer processes at its own pace.
        // If 10,000 orders arrive at once, they queue — nothing gets dropped.
        return HttpResults.Ok(new { OrderId = orderId, Status = "QUEUED" });
    }
}
