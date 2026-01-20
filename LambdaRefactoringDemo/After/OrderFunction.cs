using System.Net;
using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LambdaRefactoringDemo.After.Models;
using LambdaRefactoringDemo.After.Services;
using LambdaRefactoringDemo.After.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaRefactoringDemo.After;

public class OrderFunction(IOrderValidator validator, IOrderService orderService)
{
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/after/orders")]
    public async Task<APIGatewayProxyResponse> ProcessOrder([FromBody] OrderRequest orderRequest, ILambdaContext context)
    {
        var validation = validator.Validate(orderRequest);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage!);

        var result = await orderService.ProcessOrderAsync(orderRequest!);

        if (!result.Success)
            return BadRequest(result.ErrorMessage!);

        return Created(OrderResponse.FromOrder(result.Order!));
    }

    private static APIGatewayProxyResponse BadRequest(string message) => new()
    {
        StatusCode = (int)HttpStatusCode.BadRequest,
        Body = JsonSerializer.Serialize(new { error = message }),
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
    };

    private static APIGatewayProxyResponse Created(OrderResponse response) => new()
    {
        StatusCode = (int)HttpStatusCode.Created,
        Body = JsonSerializer.Serialize(response),
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
    };
}
