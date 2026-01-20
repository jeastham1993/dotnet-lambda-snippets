using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using LambdaRefactoringDemo.After.Repositories;
using LambdaRefactoringDemo.After.Services;
using LambdaRefactoringDemo.After.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaRefactoringDemo.After;

public static class Startup
{
    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

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

        return services.BuildServiceProvider();
    }
}
