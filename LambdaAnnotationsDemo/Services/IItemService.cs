using LambdaAnnotationsDemo.Models;

namespace LambdaAnnotationsDemo.Services;

public interface IItemService
{
    Item CreateItem(CreateItemRequest request);
    Item? GetItem(string id);
    IEnumerable<Item> GetAllItems();
}
