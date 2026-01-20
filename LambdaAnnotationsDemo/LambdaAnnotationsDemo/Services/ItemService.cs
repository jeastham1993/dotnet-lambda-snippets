using LambdaAnnotationsDemo.Models;

namespace LambdaAnnotationsDemo.Services;

public class ItemService : IItemService
{
    private static readonly Dictionary<string, Item> Items = new();

    public Item CreateItem(CreateItemRequest request)
    {
        var item = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        Items[item.Id] = item;
        return item;
    }

    public Item? GetItem(string id)
    {
        return Items.GetValueOrDefault(id);
    }

    public IEnumerable<Item> GetAllItems()
    {
        return Items.Values;
    }
}
