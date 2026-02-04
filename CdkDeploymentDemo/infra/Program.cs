using Amazon.CDK;

namespace CdkDeploymentDemo.Infra;

class Program
{
    static void Main(string[] args)
    {
        var app = new App();

        new CdkDeploymentDemoStack(app, "CdkDeploymentDemoStack", new StackProps
        {
            Description = "Items API - Lambda, API Gateway, and DynamoDB deployed with CDK"
        });

        app.Synth();
    }
}
