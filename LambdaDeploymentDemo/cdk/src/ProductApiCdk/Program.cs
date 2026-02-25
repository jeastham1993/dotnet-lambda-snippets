using Amazon.CDK;
using ProductApiCdk;

var app = new App();

new ProductApiStack(app, "ProductApiStack", new StackProps
{
    Env = new Amazon.CDK.Environment
    {
        Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
        Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
    },
});

app.Synth();
