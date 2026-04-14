using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using LambdaAnnotationsDemo.Models;
using LambdaAnnotationsDemo.Services;
using OpenTelemetry.Instrumentation.AWSLambda;
using System.Diagnostics;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaAnnotationsDemo;

public class Functions
{
    private readonly IItemService _itemService;

    public Functions(IItemService itemService)
    {
        _itemService = itemService;
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/")]
    public Task<string> GetRoot(ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(
               Observability.TracerProvider,
               async (_, _) => "Hello from Lambda Annotations!",
               context, context);

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/items")]
    public Task<IEnumerable<Item>> GetItems(ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(
               Observability.TracerProvider,
               async (_, _) => await _itemService.GetAllItems(),
               context, context);

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/items/{id}")]
    public Task<IHttpResult> GetItem(string id, ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(
               Observability.TracerProvider,
               async (_, _) =>
               {
                   Activity.Current?.SetTag("item.id", id);
                   var item = await _itemService.GetItem(id);

                   if (item is null)
                       return (IHttpResult)HttpResults.NotFound($"Item with id '{id}' not found");

                   return (IHttpResult)HttpResults.Ok(item);
               },
               context, context);

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/items")]
    public Task<IHttpResult> CreateItem([FromBody] CreateItemRequest request, ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(
               Observability.TracerProvider,
               async (_, _) =>
               {
                   Activity.Current?.SetTag("item.name", request.Name);
                   var item = await _itemService.CreateItem(request);
                   Activity.Current?.SetTag("item.id", item.Id);
                   return (IHttpResult)HttpResults.Created($"/items/{item.Id}", item);
               },
               context, context);
}
