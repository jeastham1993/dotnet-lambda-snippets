using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.DotNet;
using Constructs;

namespace CdkDeploymentDemo.Infra;

public class CdkDeploymentDemoStack : Stack
{
    public CdkDeploymentDemoStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // DynamoDB Table
        var table = new Table(this, "ItemsTable", new TableProps
        {
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "Id", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // Lambda Function - CDK builds and packages automatically
        var getAllItemsFunction = new DotNetFunction(this, "ItemsFunction", new DotNetFunctionProps
        {
            ProjectDir = "../src/ItemsApi",
            Handler = "ItemsApi::ItemsApi.Functions_GetAllItems_Generated::GetAllItems",
            Runtime = Runtime.DOTNET_10,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            }
        });
        // Grant Lambda permissions to DynamoDB - one line!
        table.GrantReadWriteData(getAllItemsFunction);
        
        var getItemFunction = new DotNetFunction(this, "GetItemFunction", new DotNetFunctionProps
        {
            ProjectDir = "../src/ItemsApi",
            Handler = "ItemsApi::ItemsApi.Functions_GetItem_Generated::GetItem",
            Runtime = Runtime.DOTNET_10,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            }
        });
        // Grant Lambda permissions to DynamoDB - one line!
        table.GrantReadWriteData(getItemFunction);
        
        var createItemFunction = new DotNetFunction(this, "CreateItemFunction", new DotNetFunctionProps
        {
            ProjectDir = "../src/ItemsApi",
            Handler = "ItemsApi::ItemsApi.Functions_CreateItem_Generated::CreateItem",
            Runtime = Runtime.DOTNET_10,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            }
        });
        // Grant Lambda permissions to DynamoDB - one line!
        table.GrantReadWriteData(createItemFunction);

        // API Gateway HTTP API
        var api = new CfnApi(this, "ItemsApi", new CfnApiProps
        {
            Name = "Items API",
            ProtocolType = "HTTP"
        });

        // Auto-deploy stage
        var stage = new CfnStage(this, "DefaultStage", new CfnStageProps
        {
            ApiId = api.Ref,
            StageName = "$default",
            AutoDeploy = true
        });

        // Lambda integration
        var getItemsIntegration = new CfnIntegration(this, "GetItemsLambdaIntegration", new CfnIntegrationProps
        {
            ApiId = api.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = getAllItemsFunction.FunctionArn,
            PayloadFormatVersion = "2.0"
        });
        // Grant API Gateway permission to invoke Lambda
        getAllItemsFunction.AddPermission("ApiGatewayInvoke", new Permission
        {
            Principal = new Amazon.CDK.AWS.IAM.ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{api.Ref}/*"
        });

        // Lambda integration
        var getItemIntegration = new CfnIntegration(this, "GetItemLambdaIntegration", new CfnIntegrationProps
        {
            ApiId = api.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = getItemFunction.FunctionArn,
            PayloadFormatVersion = "2.0"
        });
        // Grant API Gateway permission to invoke Lambda
        getItemFunction.AddPermission("ApiGatewayInvoke", new Permission
        {
            Principal = new Amazon.CDK.AWS.IAM.ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{api.Ref}/*"
        });

        // Lambda integration
        var createItemIntegration = new CfnIntegration(this, "CreateItemLambdaIntegration", new CfnIntegrationProps
        {
            ApiId = api.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = createItemFunction.FunctionArn,
            PayloadFormatVersion = "2.0"
        });
        // Grant API Gateway permission to invoke Lambda
        createItemFunction.AddPermission("ApiGatewayInvoke", new Permission
        {
            Principal = new Amazon.CDK.AWS.IAM.ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{api.Ref}/*"
        });

        new CfnRoute(this, "PostItemsRoute", new CfnRouteProps
        {
            ApiId = api.Ref,
            RouteKey = "POST /items",
            Target = $"integrations/{createItemIntegration.Ref}"
        });

        new CfnRoute(this, "GetItemByIdRoute", new CfnRouteProps
        {
            ApiId = api.Ref,
            RouteKey = "GET /items/{id}",
            Target = $"integrations/{getItemIntegration.Ref}"
        });

        // Output the API endpoint
        _ = new CfnOutput(this, "ApiEndpoint", new CfnOutputProps
        {
            Value = $"https://{api.Ref}.execute-api.{this.Region}.amazonaws.com/items",
            Description = "API Gateway endpoint URL"
        });
    }
}
        
// // Lambda Function - CDK builds and packages automatically
// var getAllItemsFunction = new ApiDotnetFunction(this, "ItemsFunction", new ApiDotnetFunctionProps()
// {
//     ProjectDir = "../src/ItemsApi",
//     Handler = "ItemsApi::ItemsApi.Functions_GetAllItems_Generated::GetAllItems",
//     Runtime = Runtime.DOTNET_10,
//     MemorySize = 512,
//     Timeout = Duration.Seconds(30),
//     Environment = new Dictionary<string, string>
//     {
//         { "TABLE_NAME", table.TableName }
//     },
//     Api = api,
//     RouteKey = "GET /items",
//     Region = this.Region,
//     Account = this.Account
// });
// // Grant Lambda permissions to DynamoDB - one line!
// table.GrantReadWriteData(getAllItemsFunction);
