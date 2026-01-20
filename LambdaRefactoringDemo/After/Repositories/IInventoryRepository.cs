using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Repositories;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByProductIdAsync(string productId);
    Task UpdateStockAsync(string productId, int newQuantity);
}
