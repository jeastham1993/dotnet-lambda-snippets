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
        
        // Lambda Function - CDK builds and packages automatically
        var getAllItemsFunction = new ApiDotnetFunction(this, "ItemsFunction", new ApiDotnetFunctionProps()
        {
            ProjectDir = "../src/ItemsApi",
            Handler = "ItemsApi::ItemsApi.Functions_GetAllItems_Generated::GetAllItems",
            Runtime = Runtime.DOTNET_10,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            },
            Api = api,
            RouteKey = "GET /items",
            Region = this.Region,
            Account = this.Account
        });
        table.GrantReadData(getAllItemsFunction);
        
        var getItemFunction = new ApiDotnetFunction(this, "GetItemFunction", new ApiDotnetFunctionProps()
        {
            ProjectDir = "../src/ItemsApi",
            Handler = "ItemsApi::ItemsApi.Functions_GetItem_Generated::GetItem",
            Runtime = Runtime.DOTNET_10,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            },
            Api = api,
            RouteKey = "GET /items/{id}",
            Region = this.Region,
            Account = this.Account
        });
        // Grant Lambda permissions to DynamoDB - one line!
        table.GrantReadData(getItemFunction);
        
        var createItemFunction = new ApiDotnetFunction(this, "GetItemFunction", new ApiDotnetFunctionProps()
        {
            ProjectDir = "../src/ItemsApi",
            Handler = "ItemsApi::ItemsApi.Functions_CreateItem_Generated::CreateItem",
            Runtime = Runtime.DOTNET_10,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            },
            Api = api,
            RouteKey = "POST /items",
            Region = this.Region,
            Account = this.Account
        });
        // Grant Lambda permissions to DynamoDB - one line!
        table.GrantReadWriteData(createItemFunction);

        // Output the API endpoint
        _ = new CfnOutput(this, "ApiEndpoint", new CfnOutputProps
        {
            Value = $"https://{api.Ref}.execute-api.{this.Region}.amazonaws.com/items",
            Description = "API Gateway endpoint URL"
        });
    }
}
