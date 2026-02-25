using LambdaTestingDemo.Models;

namespace LambdaTestingDemo.Services;

public interface IOrderService
{
    Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request);
    Task<Order?> GetOrderAsync(string orderId);
}

public class OrderResult
{
    public bool IsSuccess { get; init; }
    public Order? Order { get; init; }
    public string? ErrorMessage { get; init; }

    public static OrderResult Success(Order order) => new() { IsSuccess = true, Order = order };
    public static OrderResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
