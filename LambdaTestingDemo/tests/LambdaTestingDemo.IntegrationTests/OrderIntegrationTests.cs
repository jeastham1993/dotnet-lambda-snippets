using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LambdaTestingDemo.Models;

namespace LambdaTestingDemo.IntegrationTests;

// Integration tests invoke the real deployed Lambda via API Gateway.
// They catch what unit tests can't: IAM permission errors, missing environment
// variables, misconfigured DynamoDB table names, and real service integration failures.
//
// RESOURCE SUFFIXING
// ------------------
// Every deployed resource includes a suffix driven by the RESOURCE_SUFFIX environment variable:
//
//   RESOURCE_SUFFIX=prod        →  production stack  (Orders-prod, PlaceOrder-prod)
//   RESOURCE_SUFFIX=james       →  developer stack   (Orders-james, PlaceOrder-james)
//   RESOURCE_SUFFIX=abc123f     →  CI stack          (Orders-abc123f, PlaceOrder-abc123f)
//
// This means these tests run against a real, fully isolated copy of the infrastructure.
// No risk of stomping on production data. No conflicts with other developers.
// Same code path that production traffic takes.
//
// PREREQUISITES
// -------------
// 1. Deploy the stack: dotnet cdk deploy -c suffix=<your-suffix>
// 2. Set environment variables:
//    - API_GATEWAY_URL: the URL from the CDK output
//    - RESOURCE_SUFFIX: the suffix you deployed with
[Collection("Integration")]
public class OrderIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly HttpClient _http;

    public OrderIntegrationTests(IntegrationTestFixture fixture)
    {
        _http = fixture.HttpClient;
    }

    [Fact]
    public async Task PlaceOrder_ValidRequest_Returns201WithOrder()
    {
        var request = new PlaceOrderRequest
        {
            // Use a unique customer ID per test run to avoid state collisions
            CustomerId = $"integration-test-{Guid.NewGuid():N}",
            Items = new List<OrderLineRequest>
            {
                new() { ProductId = "PRODUCT-001", Quantity = 1 }
            }
        };

        var response = await _http.PostAsJsonAsync("/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);
        Assert.NotEmpty(order.OrderId);
        Assert.Equal(request.CustomerId, order.CustomerId);
        Assert.Equal("CONFIRMED", order.Status);
        Assert.NotEmpty(order.Items);
        // Product name should be enriched from the real catalog — not empty
        Assert.NotEmpty(order.Items[0].ProductName);
    }

    [Fact]
    public async Task PlaceOrder_InvalidRequest_Returns400()
    {
        var request = new PlaceOrderRequest
        {
            CustomerId = "",  // deliberately invalid
            Items = new List<OrderLineRequest>()
        };

        var response = await _http.PostAsJsonAsync("/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_AfterPlacing_Returns200WithSameOrder()
    {
        // Place an order first
        var placeRequest = new PlaceOrderRequest
        {
            CustomerId = $"integration-test-{Guid.NewGuid():N}",
            Items = new List<OrderLineRequest>
            {
                new() { ProductId = "PRODUCT-001", Quantity = 2 }
            }
        };

        var placeResponse = await _http.PostAsJsonAsync("/orders", placeRequest);
        placeResponse.EnsureSuccessStatusCode();
        var placedOrder = await placeResponse.Content.ReadFromJsonAsync<Order>();

        // Then retrieve it — this also validates DynamoDB read permissions
        var getResponse = await _http.GetAsync($"/orders/{placedOrder!.OrderId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrievedOrder = await getResponse.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(retrievedOrder);
        Assert.Equal(placedOrder.OrderId, retrievedOrder.OrderId);
        Assert.Equal(placedOrder.TotalAmount, retrievedOrder.TotalAmount);
    }

    [Fact]
    public async Task GetOrder_UnknownId_Returns404()
    {
        var response = await _http.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class IntegrationTestFixture
{
    public HttpClient HttpClient { get; }

    public IntegrationTestFixture()
    {
        var apiUrl = Environment.GetEnvironmentVariable("API_GATEWAY_URL")
            ?? throw new InvalidOperationException(
                "API_GATEWAY_URL must be set. Deploy the stack first: dotnet cdk deploy -c suffix=<your-suffix>");

        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(apiUrl)
        };
    }
}
