using System.Net;
using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LambdaTestingDemo.Models;
using LambdaTestingDemo.Services;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaTestingDemo;

// The handler is intentionally thin — it delegates all business logic to OrderService.
// This is what makes unit testing the business logic possible without touching AWS at all.
// See LambdaTestingDemo.UnitTests/OrderServiceTests.cs for the actual test coverage.
public class Functions(IOrderService orderService)
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/orders")]
    public async Task<APIGatewayProxyResponse> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        ILambdaContext context)
    {
        context.Logger.LogInformation("Processing order for customer {CustomerId}", request.CustomerId);

        var result = await orderService.PlaceOrderAsync(request);

        return result.IsSuccess
            ? Created(result.Order!)
            : BadRequest(result.ErrorMessage!);
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/orders/{orderId}")]
    public async Task<APIGatewayProxyResponse> GetOrder(string orderId, ILambdaContext context)
    {
        context.Logger.LogInformation("Fetching order {OrderId}", orderId);

        var order = await orderService.GetOrderAsync(orderId);

        return order is null
            ? NotFound($"Order '{orderId}' not found")
            : Ok(order);
    }

    private static APIGatewayProxyResponse Ok(object body) => new()
    {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonSerializer.Serialize(body),
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
    };

    private static APIGatewayProxyResponse Created(object body) => new()
    {
        StatusCode = (int)HttpStatusCode.Created,
        Body = JsonSerializer.Serialize(body),
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
    };

    private static APIGatewayProxyResponse BadRequest(string message) => new()
    {
        StatusCode = (int)HttpStatusCode.BadRequest,
        Body = JsonSerializer.Serialize(new { error = message }),
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
    };

    private static APIGatewayProxyResponse NotFound(string message) => new()
    {
        StatusCode = (int)HttpStatusCode.NotFound,
        Body = JsonSerializer.Serialize(new { error = message }),
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
    };
}
