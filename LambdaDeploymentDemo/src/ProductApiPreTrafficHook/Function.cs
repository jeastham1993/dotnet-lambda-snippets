using System.Text.Json;
using Amazon.CodeDeploy;
using Amazon.CodeDeploy.Model;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace ProductApiPreTrafficHook;

/// <summary>
/// CodeDeploy pre-traffic hook for the ProductApi Lambda canary deployment.
///
/// CodeDeploy invokes this function BEFORE shifting any production traffic to the
/// new version. Returning a failed status aborts the deployment — the prod alias
/// stays on the previous version and no user touches broken code.
///
/// This is the CDK-native equivalent of the pre-deployment-check job in the
/// manual deploy-production.yml pipeline.
/// </summary>
public class Function
{
    private readonly IAmazonCodeDeploy _codeDeploy;
    private readonly IAmazonLambda _lambda;

    public Function()
    {
        _codeDeploy = new AmazonCodeDeployClient();
        _lambda = new AmazonLambdaClient();
    }

    // Constructor for unit testing with mocked clients.
    public Function(IAmazonCodeDeploy codeDeploy, IAmazonLambda lambda)
    {
        _codeDeploy = codeDeploy;
        _lambda = lambda;
    }

    public async Task FunctionHandler(CodeDeployLifecycleEvent lifecycleEvent, ILambdaContext context)
    {
        var status = "Failed";

        try
        {
            var targetVersionArn = await GetTargetVersionArn(lifecycleEvent.DeploymentId, context);
            context.Logger.LogLine($"Invoking target version: {targetVersionArn}");

            var invokeResponse = await _lambda.InvokeAsync(new InvokeRequest
            {
                FunctionName = targetVersionArn,
                Payload = TestEventJson,
            });

            if (invokeResponse.FunctionError is not null)
            {
                // Lambda threw an unhandled exception — cold start failure, missing env var, etc.
                var errorPayload = await new StreamReader(invokeResponse.Payload).ReadToEndAsync();
                context.Logger.LogLine($"Target version threw an unhandled exception: {invokeResponse.FunctionError}");
                context.Logger.LogLine($"Error payload: {errorPayload}");
            }
            else
            {
                var payload = await JsonDocument.ParseAsync(invokeResponse.Payload);
                var statusCode = payload.RootElement.GetProperty("statusCode").GetInt32();

                context.Logger.LogLine($"Response status code: {statusCode}");

                if (statusCode == 200)
                {
                    status = "Succeeded";
                    context.Logger.LogLine("Pre-traffic check PASSED");
                }
                else
                {
                    context.Logger.LogLine($"Pre-traffic check FAILED: expected 200, got {statusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Pre-traffic check threw an exception: {ex}");
        }

        // Always report back — CodeDeploy waits indefinitely if we don't.
        await _codeDeploy.PutLifecycleEventHookExecutionStatusAsync(
            new PutLifecycleEventHookExecutionStatusRequest
            {
                DeploymentId = lifecycleEvent.DeploymentId,
                LifecycleEventHookExecutionId = lifecycleEvent.LifecycleEventHookExecutionId,
                Status = status,
            });
    }

    private async Task<string> GetTargetVersionArn(string deploymentId, ILambdaContext context)
    {
        var response = await _codeDeploy.GetDeploymentAsync(new GetDeploymentRequest
        {
            DeploymentId = deploymentId,
        });

        // CodeDeploy embeds the appspec as a JSON string.
        // For Lambda deployments it looks like:
        // {
        //   "Resources": [{
        //     "ProductApiFunction": {
        //       "Type": "AWS::Lambda::Function",
        //       "Properties": {
        //         "Name": "ProductApi",
        //         "Alias": "prod",
        //         "CurrentVersion": "arn:aws:lambda:...:ProductApi:5",
        //         "TargetVersion":  "arn:aws:lambda:...:ProductApi:6"
        //       }
        //     }
        //   }]
        // }
        var appSpecContent = response.DeploymentInfo.Revision.AppSpecContent.Content;
        var appSpec = JsonDocument.Parse(appSpecContent);

        foreach (var resource in appSpec.RootElement.GetProperty("Resources").EnumerateArray())
        {
            foreach (var property in resource.EnumerateObject())
            {
                var resourceInfo = property.Value;
                if (resourceInfo.GetProperty("Type").GetString() == "AWS::Lambda::Function")
                {
                    return resourceInfo.GetProperty("Properties").GetProperty("TargetVersion").GetString()
                        ?? throw new System.InvalidOperationException("TargetVersion is null in appspec");
                }
            }
        }

        throw new System.InvalidOperationException(
            $"No Lambda TargetVersion found in appspec for deployment {deploymentId}");
    }

    // Mirrors test-events/get-products.json used by the GitHub Actions pipeline.
    private const string TestEventJson = """
        {
            "version": "2.0",
            "routeKey": "GET /products",
            "rawPath": "/products",
            "rawQueryString": "",
            "headers": {
                "content-type": "application/json",
                "user-agent": "codedeploy-pre-traffic-hook"
            },
            "requestContext": {
                "accountId": "123456789012",
                "apiId": "pre-deployment-check",
                "http": {
                    "method": "GET",
                    "path": "/products",
                    "protocol": "HTTP/1.1",
                    "sourceIp": "127.0.0.1",
                    "userAgent": "codedeploy-pre-traffic-hook"
                },
                "requestId": "pre-traffic-hook",
                "routeKey": "GET /products",
                "stage": "$default",
                "time": "01/Jan/2025:00:00:00 +0000",
                "timeEpoch": 1735689600000
            },
            "isBase64Encoded": false
        }
        """;
}

/// <summary>
/// The event payload CodeDeploy sends when invoking a lifecycle hook Lambda.
/// </summary>
public class CodeDeployLifecycleEvent
{
    public string DeploymentId { get; set; } = string.Empty;
    public string LifecycleEventHookExecutionId { get; set; } = string.Empty;
}
