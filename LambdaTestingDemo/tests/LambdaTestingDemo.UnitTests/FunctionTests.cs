using Amazon.Lambda.Core;
using LambdaTestingDemo.Models;
using LambdaTestingDemo.Services;
using NSubstitute;

namespace LambdaTestingDemo.UnitTests;

// The handler is thin by design — business logic lives in OrderService.
// These tests verify the handler correctly maps service results to HTTP responses.
public class FunctionTests
{
    private readonly IOrderService _orderService = Substitute.For<IOrderService>();
    private readonly ILambdaContext _context = Substitute.For<ILambdaContext>();
    private readonly Functions _handler;

    public FunctionTests()
    {
        _handler = new Functions(_orderService);
        _context.Logger.Returns(Substitute.For<ILambdaLogger>());
    }

    [Fact]
    public async Task PlaceOrder_ServiceSucceeds_Returns201()
    {
        var order = new Order
        {
            OrderId = "ORDER-1",
            CustomerId = "CUST-123",
            Status = "CONFIRMED",
            TotalAmount = 59.98m
        };
        _orderService.PlaceOrderAsync(Arg.Any<PlaceOrderRequest>())
            .Returns(OrderResult.Success(order));

        var request = new PlaceOrderRequest { CustomerId = "CUST-123", Items = new() };
        var response = await _handler.PlaceOrder(request, _context);

        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public async Task PlaceOrder_ServiceFails_Returns400()
    {
        _orderService.PlaceOrderAsync(Arg.Any<PlaceOrderRequest>())
            .Returns(OrderResult.Failure("CustomerId is required"));

        var request = new PlaceOrderRequest { CustomerId = "", Items = new() };
        var response = await _handler.PlaceOrder(request, _context);

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_OrderExists_Returns200()
    {
        _orderService.GetOrderAsync("ORDER-1")
            .Returns(new Order { OrderId = "ORDER-1", CustomerId = "CUST-123" });

        var response = await _handler.GetOrder("ORDER-1", _context);

        Assert.Equal(200, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_OrderNotFound_Returns404()
    {
        _orderService.GetOrderAsync("MISSING").Returns((Order?)null);

        var response = await _handler.GetOrder("MISSING", _context);

        Assert.Equal(404, response.StatusCode);
    }
}
