using Amazon.EventBridge;
using Amazon.Lambda;
using Amazon.Lambda.Annotations;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;

namespace SqsEventBridgeDemo;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Used by WorkflowFunction for direct Lambda-to-Lambda invocation
        services.AddSingleton<IAmazonLambda, AmazonLambdaClient>();

        // Used by OrderProducerFunction to send messages to SQS
        services.AddSingleton<IAmazonSQS, AmazonSQSClient>();

        // Used by OrderPublisherFunction to publish events to EventBridge
        services.AddSingleton<IAmazonEventBridge, AmazonEventBridgeClient>();
    }
}
