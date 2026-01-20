using Amazon.Lambda.Annotations;
using LambdaAnnotationsDemo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaAnnotationsDemo;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IItemService, ItemService>();
    }
}
