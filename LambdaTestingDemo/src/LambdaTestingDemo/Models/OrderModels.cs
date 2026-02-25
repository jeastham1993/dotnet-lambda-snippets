namespace LambdaTestingDemo.Models;

public class PlaceOrderRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderLineRequest> Items { get; set; } = new();
}

public class OrderLineRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<EnrichedOrderLine> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
}

public class EnrichedOrderLine
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
