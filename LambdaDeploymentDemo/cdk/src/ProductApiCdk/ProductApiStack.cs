using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace ProductApiCdk;

public class ProductApiStack : Stack
{
    public ProductApiStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // --- DynamoDB table ---
        var table = new Table(this, "ProductsTable", new TableProps
        {
            TableName = "Products",
            PartitionKey = new Attribute { Name = "id", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY,
        });

        // --- Lambda function ---
        // The function definition (code, configuration, IAM) is managed here.
        // Version publishing and alias routing are handled by the GitHub Actions
        // pipeline so that canary deployments can be orchestrated independently
        // of infrastructure changes.
        var function = new Function(this, "ProductApiFunction", new FunctionProps
        {
            FunctionName = "ProductApi",
            Runtime = Runtime.DOTNET_8,
            Handler = "ProductApi",
            Code = Code.FromAsset("../../src/ProductApi/publish"),
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                // The table name is injected at deploy time.
                // Removing this variable from the stack and redeploying
                // is the "bad commit" scenario shown in the video — the
                // pre-deployment validation step catches the missing variable
                // before any traffic shifts to the broken version.
                ["PRODUCTS_TABLE_NAME"] = table.TableName,
            },
        });

        table.GrantReadWriteData(function);

        // --- Prod alias ---
        // CDK creates this alias on first deploy so that the GitHub Actions
        // pipeline has a stable target to shift traffic against.
        // After the initial deploy, the pipeline owns routing config —
        // CDK only touches the alias if function configuration changes.
        var prodAlias = function.AddAlias("prod");

        // --- CloudWatch alarm ---
        // Watches the error rate specifically on the "prod" alias qualifier.
        // The canary pipeline polls this alarm after shifting 10% of traffic:
        //   - Alarm stays OK  -> promote the new version to 100%
        //   - Alarm fires     -> roll the alias back to the previous version
        var errorAlarm = new Alarm(this, "ProductApiErrorAlarm", new AlarmProps
        {
            AlarmName = "ProductApi-prod-ErrorRate",
            AlarmDescription = "Fires when the prod alias error rate exceeds 0 in a 1-minute window. Used by the canary pipeline to trigger automatic rollback.",
            Metric = new Metric(new MetricProps
            {
                Namespace = "AWS/Lambda",
                MetricName = "Errors",
                DimensionsMap = new Dictionary<string, string>
                {
                    ["FunctionName"] = function.FunctionName,
                    ["Resource"] = $"{function.FunctionName}:prod",
                },
                Statistic = "Sum",
                Period = Duration.Minutes(1),
            }),
            Threshold = 1,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING,
        });

        // --- Stack outputs used by the GitHub Actions pipeline ---
        new CfnOutput(this, "FunctionName", new CfnOutputProps
        {
            Value = function.FunctionName,
            Description = "Lambda function name",
        });

        new CfnOutput(this, "AliasName", new CfnOutputProps
        {
            Value = prodAlias.AliasName,
            Description = "Lambda alias the pipeline shifts traffic against",
        });

        new CfnOutput(this, "ErrorAlarmName", new CfnOutputProps
        {
            Value = errorAlarm.AlarmName,
            Description = "CloudWatch alarm the canary pipeline polls",
        });
    }
}
