using LambdaTestingDemo.Models;

namespace LambdaTestingDemo.Repositories;

public interface IOrderRepository
{
    Task SaveAsync(Order order);
    Task<Order?> GetByIdAsync(string orderId);
}
