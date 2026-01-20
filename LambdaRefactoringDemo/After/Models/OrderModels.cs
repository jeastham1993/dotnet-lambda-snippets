namespace LambdaRefactoringDemo.After.Models;

public class OrderRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ShippingAddress { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class InventoryItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ShippingAddress { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public List<OrderLine> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class OrderLine
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderResponse
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public List<OrderLine> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public static OrderResponse FromOrder(Order order) => new()
    {
        OrderId = order.OrderId,
        CustomerId = order.CustomerId,
        Status = order.Status,
        OrderDate = order.OrderDate,
        Items = order.Items,
        Subtotal = order.Subtotal,
        DiscountPercent = order.DiscountPercent,
        DiscountAmount = order.DiscountAmount,
        TaxAmount = order.TaxAmount,
        TotalAmount = order.TotalAmount
    };
}
