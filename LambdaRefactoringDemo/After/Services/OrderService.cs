using LambdaRefactoringDemo.After.Models;
using LambdaRefactoringDemo.After.Repositories;

namespace LambdaRefactoringDemo.After.Services;

public class OrderService : IOrderService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IPricingService _pricingService;
    private readonly INotificationService _notificationService;

    public OrderService(
        IInventoryRepository inventoryRepository,
        IOrderRepository orderRepository,
        IPricingService pricingService,
        INotificationService notificationService)
    {
        _inventoryRepository = inventoryRepository;
        _orderRepository = orderRepository;
        _pricingService = pricingService;
        _notificationService = notificationService;
    }

    public async Task<OrderResult> ProcessOrderAsync(OrderRequest request)
    {
        // Check inventory and build order lines
        var orderLines = new List<OrderLine>();
        var inventoryUpdates = new List<(string ProductId, int NewQuantity)>();

        foreach (var item in request.Items)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId);

            if (inventory == null)
                return OrderResult.Fail($"Product {item.ProductId} not found");

            if (inventory.StockQuantity < item.Quantity)
                return OrderResult.Fail($"Insufficient stock for {inventory.ProductName}. Available: {inventory.StockQuantity}");

            orderLines.Add(new OrderLine
            {
                ProductId = item.ProductId,
                ProductName = inventory.ProductName,
                Quantity = item.Quantity,
                UnitPrice = inventory.UnitPrice,
                LineTotal = inventory.UnitPrice * item.Quantity
            });

            inventoryUpdates.Add((item.ProductId, inventory.StockQuantity - item.Quantity));
        }

        // Calculate pricing
        var pricing = _pricingService.CalculatePricing(orderLines);

        // Create order
        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = request.CustomerId,
            Email = request.Email,
            ShippingAddress = request.ShippingAddress,
            Status = "CONFIRMED",
            OrderDate = DateTime.UtcNow,
            Items = orderLines,
            Subtotal = pricing.Subtotal,
            DiscountPercent = pricing.DiscountPercent,
            DiscountAmount = pricing.DiscountAmount,
            TaxAmount = pricing.TaxAmount,
            TotalAmount = pricing.TotalAmount
        };

        // Persist order
        await _orderRepository.SaveAsync(order);

        // Update inventory
        foreach (var (productId, newQuantity) in inventoryUpdates)
        {
            await _inventoryRepository.UpdateStockAsync(productId, newQuantity);
        }

        // Send notification
        await _notificationService.SendOrderConfirmationAsync(order);

        return OrderResult.Ok(order);
    }
}
