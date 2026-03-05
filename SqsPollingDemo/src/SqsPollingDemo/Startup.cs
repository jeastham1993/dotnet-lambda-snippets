using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace SqsPollingDemo;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register services needed by the Lambda handler.
        // AWS clients, repositories, and other dependencies go here.
    }
}
