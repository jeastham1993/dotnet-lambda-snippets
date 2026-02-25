using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.DotNet;
using Constructs;

namespace LambdaTestingDemo.Infra;

public class LambdaTestingDemoStackProps : StackProps
{
    // The suffix isolates every resource in this stack.
    // Orders-prod, Orders-james, Orders-abc123f are all independent.
    public required string Suffix { get; init; }
}

public class LambdaTestingDemoStack : Stack
{
    public LambdaTestingDemoStack(Construct scope, string id, LambdaTestingDemoStackProps props)
        : base(scope, id, props)
    {
        var suffix = props.Suffix;

        // DynamoDB — table name includes the suffix so every environment is isolated
        var ordersTable = new Table(this, "OrdersTable", new TableProps
        {
            TableName = $"Orders-{suffix}",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "OrderId",
                Type = AttributeType.STRING
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = suffix == "prod" ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY
        });

        // API Gateway
        var api = new CfnApi(this, "OrdersApi", new CfnApiProps
        {
            Name = $"Orders API ({suffix})",
            ProtocolType = "HTTP"
        });

        new CfnStage(this, "DefaultStage", new CfnStageProps
        {
            ApiId = api.Ref,
            StageName = "$default",
            AutoDeploy = true
        });

        // Shared Lambda environment variables
        var lambdaEnv = new Dictionary<string, string>
        {
            { "ORDERS_TABLE", ordersTable.TableName },
            { "PRODUCT_CATALOG_URL", "https://catalog.example.com" }, // replace with real URL
            { "RESOURCE_SUFFIX", suffix }
        };

        // PlaceOrder Lambda — function name includes the suffix
        var placeOrderFn = new DotNetFunction(this, "PlaceOrderFn", new DotNetFunctionProps
        {
            FunctionName = $"PlaceOrder-{suffix}",
            ProjectDir = "../src/LambdaTestingDemo",
            Handler = "LambdaTestingDemo::LambdaTestingDemo.Functions_PlaceOrder_Generated::PlaceOrder",
            Runtime = Runtime.DOTNET_8,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = lambdaEnv
        });
        ordersTable.GrantWriteData(placeOrderFn);

        new CfnIntegration(this, "PlaceOrderIntegration", new CfnIntegrationProps
        {
            ApiId = api.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = placeOrderFn.FunctionArn,
            PayloadFormatVersion = "2.0"
        });

        // GetOrder Lambda
        var getOrderFn = new DotNetFunction(this, "GetOrderFn", new DotNetFunctionProps
        {
            FunctionName = $"GetOrder-{suffix}",
            ProjectDir = "../src/LambdaTestingDemo",
            Handler = "LambdaTestingDemo::LambdaTestingDemo.Functions_GetOrder_Generated::GetOrder",
            Runtime = Runtime.DOTNET_8,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = lambdaEnv
        });
        ordersTable.GrantReadData(getOrderFn);

        // Outputs — the integration test uses API_GATEWAY_URL
        _ = new CfnOutput(this, "ApiEndpoint", new CfnOutputProps
        {
            Value = $"https://{api.Ref}.execute-api.{this.Region}.amazonaws.com",
            Description = $"API Gateway endpoint for suffix '{suffix}'. Set as API_GATEWAY_URL for integration tests."
        });

        _ = new CfnOutput(this, "ResourceSuffix", new CfnOutputProps
        {
            Value = suffix,
            Description = "Set as RESOURCE_SUFFIX when running integration tests."
        });
    }
}
