using Amazon.DynamoDBv2;
using Amazon.Lambda.Annotations;
using LambdaTestingDemo.Adapters;
using LambdaTestingDemo.Repositories;
using LambdaTestingDemo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaTestingDemo;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        var productCatalogUrl = Environment.GetEnvironmentVariable("PRODUCT_CATALOG_URL")
            ?? throw new InvalidOperationException("PRODUCT_CATALOG_URL environment variable is required");

        // AWS services
        services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();

        // Typed HTTP client — the adapter to the upstream Product Catalog API.
        // Swapping this for a mock in tests is what makes contract testing possible.
        services.AddHttpClient<IProductCatalogClient, HttpProductCatalogClient>(client =>
        {
            client.BaseAddress = new Uri(productCatalogUrl);
        });

        // Repositories
        services.AddSingleton<IOrderRepository, OrderRepository>();

        // Services
        services.AddSingleton<IOrderService, OrderService>();
    }
}
