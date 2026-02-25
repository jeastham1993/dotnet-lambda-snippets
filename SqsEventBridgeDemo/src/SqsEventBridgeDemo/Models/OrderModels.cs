namespace SqsEventBridgeDemo.Models;

public record OrderRequest(
    string CustomerId,
    string ProductId,
    int Quantity);

// The event published to EventBridge when an order is placed.
// All three consumers (Fulfilment, Notifications, Analytics) receive this.
public record OrderPlacedEvent(
    string OrderId,
    string CustomerId,
    string ProductId,
    int Quantity,
    decimal TotalAmount,
    DateTime PlacedAt);

// Payload sent from WorkflowFunction to PaymentFunction via direct invocation
public record PaymentRequest(
    string OrderId,
    decimal Amount,
    string CustomerId);

public record PaymentResult(
    string OrderId,
    bool Success,
    string? ErrorMessage);

// The message body stored in SQS — serialised to JSON
public record OrderMessage(
    string OrderId,
    string CustomerId,
    string ProductId,
    int Quantity,
    decimal TotalAmount);
