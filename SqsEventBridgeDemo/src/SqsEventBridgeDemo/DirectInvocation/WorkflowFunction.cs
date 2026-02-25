using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using SqsEventBridgeDemo.Models;

namespace SqsEventBridgeDemo.DirectInvocation;

// This Lambda calls PaymentFunction directly and synchronously.
// If the payment service slows down, this caller times out — and the
// error propagates upstream to whoever called this function.
// This is the fragility the video opens with.
public class WorkflowFunction(IAmazonLambda lambdaClient)
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/direct/orders")]
    public async Task<IHttpResult> PlaceOrder([FromBody] OrderRequest request, ILambdaContext context)
    {
        var orderId = Guid.NewGuid().ToString();
        context.Logger.LogInformation($"Processing order {orderId} for customer {request.CustomerId}");

        var paymentRequest = new PaymentRequest(orderId, 99.99m, request.CustomerId);

        // Direct, synchronous invocation — this Lambda blocks until PaymentFunction responds.
        // If PaymentFunction is slow, this Lambda times out and returns an error.
        // The caller of this Lambda then receives that error too — failure cascades upstream.
        var invokeRequest = new InvokeRequest
        {
            FunctionName = Environment.GetEnvironmentVariable("PAYMENT_FUNCTION_ARN"),
            InvocationType = InvocationType.RequestResponse,
            Payload = JsonSerializer.Serialize(paymentRequest)
        };

        var response = await lambdaClient.InvokeAsync(invokeRequest);

        if (response.FunctionError != null)
        {
            context.Logger.LogError($"Payment Lambda returned an error for order {orderId}");
            return HttpResults.InternalServerError();
        }

        var result = JsonSerializer.Deserialize<PaymentResult>(response.Payload);

        if (result?.Success != true)
        {
            context.Logger.LogError($"Payment failed for order {orderId}: {result?.ErrorMessage}");
            return HttpResults.BadRequest(result?.ErrorMessage ?? "Payment failed");
        }

        return HttpResults.Ok(new { OrderId = orderId, Status = "CONFIRMED" });
    }
}
