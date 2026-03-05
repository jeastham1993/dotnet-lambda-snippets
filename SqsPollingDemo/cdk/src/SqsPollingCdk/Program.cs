using Amazon.CDK;
using SqsPollingCdk;

var app = new App();

new SqsPollingStack(app, "SqsPollingStack", new StackProps
{
    Description = "SQS polling demo — Lambda event source mapping vs worker service polling"
});

app.Synth();
