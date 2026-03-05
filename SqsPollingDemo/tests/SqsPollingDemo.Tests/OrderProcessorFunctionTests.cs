using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using SqsPollingDemo.Models;
using Xunit;

namespace SqsPollingDemo.Tests;

public class OrderProcessorFunctionTests
{
    private readonly OrderProcessorFunction _function = new();
    private readonly TestLambdaContext _context = new();

    [Fact]
    public async Task ProcessOrders_WithValidMessages_ReturnsEmptyFailureList()
    {
        var sqsEvent = BuildSqsEvent(
            new OrderMessage("order-1", "customer-1", "product-abc", 2, 49.99m),
            new OrderMessage("order-2", "customer-2", "product-xyz", 1, 19.99m));

        var response = await _function.ProcessOrders(sqsEvent, _context);

        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public async Task ProcessOrders_WithInvalidMessage_ReportsPartialFailure()
    {
        // One valid message and one with an invalid body
        var sqsEvent = new SQSEvent
        {
            Records =
            [
                BuildSqsRecord("msg-valid", JsonSerializer.Serialize(
                    new OrderMessage("order-1", "customer-1", "product-abc", 2, 49.99m))),
                BuildSqsRecord("msg-invalid", "this is not valid json")
            ]
        };

        var response = await _function.ProcessOrders(sqsEvent, _context);

        // Only the invalid message should be reported as a failure
        var failure = Assert.Single(response.BatchItemFailures);
        Assert.Equal("msg-invalid", failure.ItemIdentifier);
    }

    [Fact]
    public async Task ProcessOrders_WithAllInvalidMessages_ReportsAllFailures()
    {
        var sqsEvent = new SQSEvent
        {
            Records =
            [
                BuildSqsRecord("msg-1", "not json"),
                BuildSqsRecord("msg-2", "also not json"),
                BuildSqsRecord("msg-3", "null")  // Deserialises to null — triggers the null guard
            ]
        };

        var response = await _function.ProcessOrders(sqsEvent, _context);

        Assert.Equal(3, response.BatchItemFailures.Count);
    }

    [Fact]
    public async Task ProcessOrders_WithEmptyBatch_ReturnsEmptyFailureList()
    {
        var sqsEvent = new SQSEvent { Records = [] };

        var response = await _function.ProcessOrders(sqsEvent, _context);

        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public async Task ProcessOrders_SuccessfulMessages_AreNotInFailureList()
    {
        // Three messages: first and third valid, second invalid
        var sqsEvent = new SQSEvent
        {
            Records =
            [
                BuildSqsRecord("msg-1", JsonSerializer.Serialize(
                    new OrderMessage("order-1", "customer-1", "product-a", 1, 10.00m))),
                BuildSqsRecord("msg-2", "bad json"),
                BuildSqsRecord("msg-3", JsonSerializer.Serialize(
                    new OrderMessage("order-3", "customer-3", "product-c", 3, 30.00m)))
            ]
        };

        var response = await _function.ProcessOrders(sqsEvent, _context);

        var failure = Assert.Single(response.BatchItemFailures);
        Assert.Equal("msg-2", failure.ItemIdentifier);
    }

    private static SQSEvent BuildSqsEvent(params OrderMessage[] orders)
    {
        return new SQSEvent
        {
            Records = orders
                .Select((order, i) => BuildSqsRecord($"msg-{i + 1}", JsonSerializer.Serialize(order)))
                .ToList()
        };
    }

    private static SQSEvent.SQSMessage BuildSqsRecord(string messageId, string body)
    {
        return new SQSEvent.SQSMessage
        {
            MessageId = messageId,
            Body = body,
            ReceiptHandle = $"receipt-{messageId}"
        };
    }
}
