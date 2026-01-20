using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using LambdaAnnotationsDemo.Models;
using LambdaAnnotationsDemo.Services;

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
    public string GetRoot()
    {
        return "Hello from Lambda Annotations!";
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/items")]
    public IEnumerable<Item> GetItems()
    {
        return _itemService.GetAllItems();
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/items/{id}")]
    public IHttpResult GetItem(string id)
    {
        var item = _itemService.GetItem(id);

        if (item is null)
        {
            return HttpResults.NotFound($"Item with id '{id}' not found");
        }

        return HttpResults.Ok(item);
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/items")]
    public IHttpResult CreateItem([FromBody] CreateItemRequest request)
    {
        var item = _itemService.CreateItem(request);
        return HttpResults.Created($"/items/{item.Id}", item);
    }
}
