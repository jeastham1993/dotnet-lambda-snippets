using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LambdaRefactoringDemo.After.Models;
using LambdaRefactoringDemo.After.Services;
using LambdaRefactoringDemo.After.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaRefactoringDemo.After;

public class OrderFunction
{
    private readonly IOrderValidator _validator;
    private readonly IOrderService _orderService;

    public OrderFunction() : this(Startup.ConfigureServices()) { }

    public OrderFunction(ServiceProvider serviceProvider)
    {
        _validator = serviceProvider.GetRequiredService<IOrderValidator>();
        _orderService = serviceProvider.GetRequiredService<IOrderService>();
    }

    public async Task<APIGatewayProxyResponse> ProcessOrder(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var orderRequest = DeserializeRequest(request.Body);

        var validation = _validator.Validate(orderRequest);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage!);

        var result = await _orderService.ProcessOrderAsync(orderRequest!);

        if (!result.Success)
            return BadRequest(result.ErrorMessage!);

        return Created(OrderResponse.FromOrder(result.Order!));
    }

    private static OrderRequest? DeserializeRequest(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        try
        {
            return JsonSerializer.Deserialize<OrderRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch { return null; }
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
