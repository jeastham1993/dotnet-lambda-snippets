using LambdaAnnotationsDemo.Models;

namespace LambdaAnnotationsDemo.Services;

public interface IItemService
{
    Task<Item> CreateItem(CreateItemRequest request);
    Task<Item?> GetItem(string id);
    Task<IEnumerable<Item>> GetAllItems();
}
