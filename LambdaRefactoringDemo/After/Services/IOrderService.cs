using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Services;

public interface IOrderService
{
    Task<OrderResult> ProcessOrderAsync(OrderRequest request);
}

public class OrderResult
{
    public bool Success { get; init; }
    public Order? Order { get; init; }
    public string? ErrorMessage { get; init; }

    public static OrderResult Ok(Order order) => new() { Success = true, Order = order };
    public static OrderResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
