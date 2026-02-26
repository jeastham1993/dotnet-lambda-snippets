using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CodeDeploy;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.DotNet;
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
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "id", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY,
        });

        // --- Lambda function ---
        // CDK publishes a new immutable version on every deploy when code or
        // configuration changes. The LambdaDeploymentGroup below hands that
        // version off to CodeDeploy, which handles the canary traffic shift,
        // alarm monitoring, and rollback -- no bash polling loops required.
        var function = new DotNetFunction(this, "ProductApiFunction", new DotNetFunctionProps
        {
            FunctionName = "ProductApi",
            Runtime = Runtime.DOTNET_8,
            Handler = "ProductApi",
            ProjectDir = "../src/ProductApi",
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                // The table name is injected at deploy time.
                // Removing this variable and redeploying is the "bad commit"
                // scenario shown in the video -- the pre-traffic hook catches
                // the missing variable before any traffic shifts.
                ["PRODUCTS_TABLE_NAME"] = table.TableName,
            },
        });

        table.GrantReadWriteData(function);

        // --- Prod alias ---
        // CodeDeploy manages the version this alias points to. CDK creates the
        // alias on first deploy; after that, every deployment goes through the
        // canary pipeline defined by the LambdaDeploymentGroup below.
        var prodAlias = function.AddAlias("prod");

        // --- CloudWatch alarm ---
        // Watches the error rate on the "prod" alias qualifier. CodeDeploy
        // polls this alarm during the canary bake period:
        //   - Alarm stays OK  -> promote the new version to 100%
        //   - Alarm fires     -> roll the alias back to the previous version
        var errorAlarm = new Alarm(this, "ProductApiErrorAlarm", new AlarmProps
        {
            AlarmName = "ProductApi-prod-ErrorRate",
            AlarmDescription = "Fires when the prod alias error rate exceeds 0 in a 1-minute window. CodeDeploy uses this alarm to trigger automatic rollback during canary deployments.",
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

        // --- Pre-traffic hook ---
        // CodeDeploy invokes this Lambda BEFORE shifting any traffic to the new
        // version. It invokes the target version directly with a synthetic test
        // event and reports Succeeded or Failed back to CodeDeploy. A failure
        // here aborts the deployment -- the prod alias never moves.
        //
        // This is the CDK-native equivalent of the pre-deployment-check job in
        // the manual deploy-production.yml pipeline.
        var preTrafficHook = new DotNetFunction(this, "PreTrafficHook", new DotNetFunctionProps
        {
            FunctionName = "ProductApi-PreTrafficHook",
            Runtime = Runtime.DOTNET_8,
            Handler = "ProductApiPreTrafficHook::ProductApiPreTrafficHook.Function::FunctionHandler",
            ProjectDir = "../src/ProductApiPreTrafficHook",
            Timeout = Duration.Minutes(5),
        });

        // Allow the hook to invoke any version of the ProductApi function.
        function.GrantInvoke(preTrafficHook);

        // Allow the hook to read the deployment appspec so it can discover the
        // target version ARN. CDK automatically grants PutLifecycleEventHookExecutionStatus
        // when the hook is registered with the deployment group below.
        preTrafficHook.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "codedeploy:GetDeployment" },
            Resources = new[] { "*" },
        }));

        // --- CodeDeploy deployment group ---
        // Replaces the four-stage GitHub Actions pipeline with a single CDK
        // deploy. CodeDeploy handles:
        //   1. Pre-traffic validation  (via preTrafficHook)
        //   2. Canary traffic shift    (10% for 5 minutes)
        //   3. Alarm monitoring        (errorAlarm)
        //   4. Automatic rollback      (if alarm fires or hook fails)
        //   5. Full promotion          (100% if bake period passes cleanly)
        //
        // The GitHub Actions workflow shrinks to a single `cdk deploy` call.
        _ = new LambdaDeploymentGroup(this, "ProductApiDeploymentGroup", new LambdaDeploymentGroupProps
        {
            Alias = prodAlias,
            DeploymentConfig = LambdaDeploymentConfig.CANARY_10PERCENT_5MINUTES,
            Alarms = new[] { errorAlarm },
            PreHook = preTrafficHook,
            AutoRollback = new AutoRollbackConfig
            {
                // Roll back if the error alarm fires during the canary period.
                DeploymentInAlarm = true,
                // Roll back if any part of the deployment fails (e.g. the pre-traffic hook).
                FailedDeployment = true,
            },
        });

        // --- Stack outputs ---
        new CfnOutput(this, "FunctionName", new CfnOutputProps
        {
            Value = function.FunctionName,
            Description = "Lambda function name",
        });

        new CfnOutput(this, "AliasName", new CfnOutputProps
        {
            Value = prodAlias.AliasName,
            Description = "Lambda alias managed by CodeDeploy",
        });

        new CfnOutput(this, "ErrorAlarmName", new CfnOutputProps
        {
            Value = errorAlarm.AlarmName,
            Description = "CloudWatch alarm CodeDeploy monitors during canary bake",
        });
    }
}
