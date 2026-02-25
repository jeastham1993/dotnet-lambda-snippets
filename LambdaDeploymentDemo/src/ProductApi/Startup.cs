using Amazon.DynamoDBv2;
using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProductApi.Services;

namespace ProductApi;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
        services.AddSingleton<IProductService, ProductService>();
    }
}
