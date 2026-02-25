using LambdaTestingDemo.Adapters;
using LambdaTestingDemo.Models;
using LambdaTestingDemo.Repositories;
using LambdaTestingDemo.Services;
using NSubstitute;

namespace LambdaTestingDemo.UnitTests;

// Unit tests run in milliseconds, need no AWS credentials, and catch regressions instantly.
// They test the business logic in OrderService with all external dependencies mocked out.
public class OrderServiceTests
{
    private readonly IProductCatalogClient _productCatalog = Substitute.For<IProductCatalogClient>();
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _sut = new OrderService(_productCatalog, _orderRepository);
    }

    [Fact]
    public async Task PlaceOrder_ValidRequest_ReturnsConfirmedOrder()
    {
        _productCatalog.GetProductAsync("P001").Returns(new ProductDetails
        {
            ProductId = "P001",
            ProductName = "Widget Pro",
            UnitPrice = 29.99m,
            Category = "Electronics",
            InStock = true
        });

        var request = new PlaceOrderRequest
        {
            CustomerId = "CUST-123",
            Items = new List<OrderLineRequest>
            {
                new() { ProductId = "P001", Quantity = 2 }
            }
        };

        var result = await _sut.PlaceOrderAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Order);
        Assert.Equal("CUST-123", result.Order.CustomerId);
        Assert.Equal("CONFIRMED", result.Order.Status);
        Assert.Equal(59.98m, result.Order.TotalAmount);
    }

    [Fact]
    public async Task PlaceOrder_EnrichesLineItemsWithCatalogData()
    {
        _productCatalog.GetProductAsync("P001").Returns(new ProductDetails
        {
            ProductId = "P001",
            ProductName = "Widget Pro",
            UnitPrice = 10.00m,
            Category = "Tools",
            InStock = true
        });

        var request = new PlaceOrderRequest
        {
            CustomerId = "CUST-123",
            Items = new List<OrderLineRequest> { new() { ProductId = "P001", Quantity = 3 } }
        };

        var result = await _sut.PlaceOrderAsync(request);

        var line = result.Order!.Items.Single();
        Assert.Equal("Widget Pro", line.ProductName);
        Assert.Equal("Tools", line.Category);
        Assert.Equal(10.00m, line.UnitPrice);
        Assert.Equal(30.00m, line.LineTotal);
    }

    [Fact]
    public async Task PlaceOrder_MissingCustomerId_ReturnsFailure()
    {
        var request = new PlaceOrderRequest
        {
            CustomerId = "",
            Items = new List<OrderLineRequest> { new() { ProductId = "P001", Quantity = 1 } }
        };

        var result = await _sut.PlaceOrderAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("CustomerId is required", result.ErrorMessage);
    }

    [Fact]
    public async Task PlaceOrder_EmptyItemsList_ReturnsFailure()
    {
        var request = new PlaceOrderRequest
        {
            CustomerId = "CUST-123",
            Items = new List<OrderLineRequest>()
        };

        var result = await _sut.PlaceOrderAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("At least one item is required", result.ErrorMessage);
    }

    [Fact]
    public async Task PlaceOrder_ZeroQuantity_ReturnsFailure()
    {
        var request = new PlaceOrderRequest
        {
            CustomerId = "CUST-123",
            Items = new List<OrderLineRequest> { new() { ProductId = "P001", Quantity = 0 } }
        };

        var result = await _sut.PlaceOrderAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("greater than zero", result.ErrorMessage);
    }

    [Fact]
    public async Task PlaceOrder_ProductNotInCatalog_ReturnsFailure()
    {
        _productCatalog.GetProductAsync("UNKNOWN").Returns((ProductDetails?)null);

        var request = new PlaceOrderRequest
        {
            CustomerId = "CUST-123",
            Items = new List<OrderLineRequest> { new() { ProductId = "UNKNOWN", Quantity = 1 } }
        };

        var result = await _sut.PlaceOrderAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found in the catalog", result.ErrorMessage);
    }

    [Fact]
    public async Task PlaceOrder_ProductOutOfStock_ReturnsFailure()
    {
        _productCatalog.GetProductAsync("P001").Returns(new ProductDetails
        {
            ProductId = "P001",
            ProductName = "Widget Pro",
            UnitPrice = 29.99m,
            InStock = false
        });

        var request = new PlaceOrderRequest
        {
            CustomerId = "CUST-123",
            Items = new List<OrderLineRequest> { new() { ProductId = "P001", Quantity = 1 } }
        };

        var result = await _sut.PlaceOrderAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("out of stock", result.ErrorMessage);
    }

    [Fact]
    public async Task PlaceOrder_Success_PersistsOrderToRepository()
    {
        _productCatalog.GetProductAsync("P001").Returns(new ProductDetails
        {
            ProductId = "P001",
            ProductName = "Widget Pro",
            UnitPrice = 10.00m,
            Category = "Tools",
            InStock = true
        });

        var request = new PlaceOrderRequest
        {
            CustomerId = "CUST-123",
            Items = new List<OrderLineRequest> { new() { ProductId = "P001", Quantity = 1 } }
        };

        await _sut.PlaceOrderAsync(request);

        await _orderRepository.Received(1).SaveAsync(Arg.Is<Order>(o =>
            o.CustomerId == "CUST-123" &&
            o.Status == "CONFIRMED" &&
            o.TotalAmount == 10.00m));
    }

    [Fact]
    public async Task PlaceOrder_Failure_DoesNotPersistToRepository()
    {
        var request = new PlaceOrderRequest { CustomerId = "", Items = new() };

        await _sut.PlaceOrderAsync(request);

        await _orderRepository.DidNotReceive().SaveAsync(Arg.Any<Order>());
    }
}
