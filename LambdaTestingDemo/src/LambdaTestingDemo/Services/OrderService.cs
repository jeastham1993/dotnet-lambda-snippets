using LambdaTestingDemo.Adapters;
using LambdaTestingDemo.Models;
using LambdaTestingDemo.Repositories;

namespace LambdaTestingDemo.Services;

public class OrderService : IOrderService
{
    private readonly IProductCatalogClient _productCatalog;
    private readonly IOrderRepository _orderRepository;

    public OrderService(IProductCatalogClient productCatalog, IOrderRepository orderRepository)
    {
        _productCatalog = productCatalog;
        _orderRepository = orderRepository;
    }

    public async Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            return OrderResult.Failure("CustomerId is required");

        if (request.Items == null || request.Items.Count == 0)
            return OrderResult.Failure("At least one item is required");

        var enrichedLines = new List<EnrichedOrderLine>();

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                return OrderResult.Failure($"Quantity for '{item.ProductId}' must be greater than zero");

            var product = await _productCatalog.GetProductAsync(item.ProductId);

            if (product == null)
                return OrderResult.Failure($"Product '{item.ProductId}' not found in the catalog");

            if (!product.InStock)
                return OrderResult.Failure($"Product '{product.ProductName}' is currently out of stock");

            enrichedLines.Add(new EnrichedOrderLine
            {
                ProductId = item.ProductId,
                ProductName = product.ProductName,
                Category = product.Category,
                Quantity = item.Quantity,
                UnitPrice = product.UnitPrice,
                LineTotal = product.UnitPrice * item.Quantity
            });
        }

        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = request.CustomerId,
            Status = "CONFIRMED",
            CreatedAt = DateTime.UtcNow,
            Items = enrichedLines,
            TotalAmount = enrichedLines.Sum(l => l.LineTotal)
        };

        await _orderRepository.SaveAsync(order);

        return OrderResult.Success(order);
    }

    public Task<Order?> GetOrderAsync(string orderId)
    {
        return _orderRepository.GetByIdAsync(orderId);
    }
}
