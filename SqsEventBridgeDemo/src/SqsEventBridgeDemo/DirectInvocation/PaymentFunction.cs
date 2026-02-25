using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using SqsEventBridgeDemo.Models;

namespace SqsEventBridgeDemo.DirectInvocation;

// Simulates a downstream payment service.
// Uncomment the delay to reproduce the opening incident from the video —
// WorkflowFunction will time out waiting for this response, and the
// error will propagate to every caller in the chain.
public class PaymentFunction
{
    [LambdaFunction]
    public async Task<PaymentResult> ProcessPayment(PaymentRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing payment for order {request.OrderId}");

        // Uncomment to simulate downstream latency and trigger caller timeout:
        // await Task.Delay(TimeSpan.FromSeconds(30));

        await Task.Delay(TimeSpan.FromMilliseconds(50));

        return new PaymentResult(request.OrderId, Success: true, ErrorMessage: null);
    }
}
