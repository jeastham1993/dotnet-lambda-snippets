using Amazon.CDK;
using SqsEventBridgeCdk;

var app = new App();

new SqsEventBridgeStack(app, "SqsEventBridgeStack", new StackProps
{
    Env = new Amazon.CDK.Environment
    {
        Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
        Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
    }
});

app.Synth();
