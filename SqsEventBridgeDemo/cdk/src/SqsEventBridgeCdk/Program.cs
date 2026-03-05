using Amazon.CDK;

namespace SqsEventBridgeCdk;

class Program
{
    static void Main(string[] args)
    {
        var app = new App();

        new SqsEventBridgeStack(app, "CdkDeploymentDemoStack", new StackProps
        {
            Description = "Items API - Lambda, API Gateway, and DynamoDB deployed with CDK"
        });

        app.Synth();
    }
}