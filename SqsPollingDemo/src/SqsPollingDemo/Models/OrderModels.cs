namespace SqsPollingDemo.Models;

public record OrderMessage(
    string OrderId,
    string CustomerId,
    string ProductId,
    int Quantity,
    decimal TotalAmount);
