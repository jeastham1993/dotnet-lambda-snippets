using Amazon.DynamoDBv2;
using Amazon.Lambda.Annotations;
using Amazon.SimpleNotificationService;
using LambdaRefactoringDemo.After.Repositories;
using LambdaRefactoringDemo.After.Services;
using LambdaRefactoringDemo.After.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaRefactoringDemo.After;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // AWS Services
        services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
        services.AddSingleton<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceClient>();

        // Validation
        services.AddSingleton<IOrderValidator, OrderValidator>();

        // Repositories
        services.AddSingleton<IInventoryRepository, InventoryRepository>();
        services.AddSingleton<IOrderRepository, OrderRepository>();

        // Services
        services.AddSingleton<IPricingService, PricingService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IOrderService, OrderService>();
    }
}
