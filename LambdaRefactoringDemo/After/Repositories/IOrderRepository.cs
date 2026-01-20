using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Repositories;

public interface IOrderRepository
{
    Task SaveAsync(Order order);
}
