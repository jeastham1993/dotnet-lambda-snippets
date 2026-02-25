using System.Net.Http.Json;
using LambdaTestingDemo.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace LambdaTestingDemo.ContractTests;

// Contract tests are the layer most teams skip — and the one that would have prevented
// the production incident in the video intro.
//
// These tests spin up a WireMock server that simulates the upstream Product Catalog API,
// then verify our Lambda's model can correctly deserialise the expected response shape.
//
// Run these in CI before any deployment. If the upstream team changes their API shape,
// they show up red here — not as corrupted data in production 48 hours later.
public class ProductCatalogContractTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;

    public ProductCatalogContractTests()
    {
        _server = WireMockServer.Start();
        _http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
    }

    // -------------------------------------------------------------------------
    // THE HAPPY PATH CONTRACT
    // This is the shape we have agreed with the Product Catalog team.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetProduct_CurrentContract_DeserializesAllRequiredFields()
    {
        // This is the agreed contract: field names, types, and presence.
        // If upstream breaks any of these, this test turns red.
        _server
            .Given(Request.Create().WithPath("/products/P001").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    productId = "P001",
                    productName = "Widget Pro",
                    unitPrice = 29.99,
                    category = "Electronics",
                    inStock = true
                }));

        var product = await _http.GetFromJsonAsync<ProductDetails>("/products/P001");

        Assert.NotNull(product);
        Assert.Equal("P001", product.ProductId);
        Assert.Equal("Widget Pro", product.ProductName);
        Assert.Equal(29.99m, product.UnitPrice);
        Assert.Equal("Electronics", product.Category);
        Assert.True(product.InStock);
    }

    [Fact]
    public async Task GetProduct_NotFound_Returns404()
    {
        _server
            .Given(Request.Create().WithPath("/products/DOES-NOT-EXIST").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var response = await _http.GetAsync("/products/DOES-NOT-EXIST");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // THE BREAKING CHANGE SCENARIO
    //
    // This is exactly what happened in production. The upstream team released v2
    // of the Product Catalog API. They renamed three fields. Our Lambda still
    // "worked" — it just silently stored empty product names and zero prices
    // for 48 hours before anyone noticed.
    //
    // This test documents WHAT HAPPENS when the contract breaks. In a real CI
    // pipeline, you'd fail the build the moment upstream started returning v2
    // from your contract verification environment.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetProduct_UpstreamRenamesFields_DataSilentlyCorrupts()
    {
        // Simulate the breaking change: upstream renames fields in their v2 API.
        // They didn't tell us. They thought it was a "non-breaking" change
        // because their existing clients were updated. We weren't.
        _server
            .Given(Request.Create().WithPath("/products/P002").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    id = "P002",           // was: productId
                    name = "Widget Pro",   // was: productName   ← BREAKING CHANGE
                    price = 29.99,         // was: unitPrice     ← BREAKING CHANGE
                    category = "Electronics",
                    available = true       // was: inStock       ← BREAKING CHANGE
                }));

        var product = await _http.GetFromJsonAsync<ProductDetails>("/products/P002");

        // The Lambda doesn't throw. It returns a "valid" object.
        // This is exactly why the incident wasn't caught for 48 hours.
        Assert.NotNull(product);

        // But the data is silently corrupted.
        // Our model has no field called "name" — so productName is null/empty.
        Assert.Empty(product.ProductName);

        // Our model has no field called "price" — so unitPrice is 0.
        Assert.Equal(0m, product.UnitPrice);

        // Every order placed during the outage stored an empty product name and a zero price.
        // A contract test running in CI would have caught this the moment
        // upstream published their v2 contract — before a single order was taken.
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Stop();
        _server.Dispose();
    }
}
