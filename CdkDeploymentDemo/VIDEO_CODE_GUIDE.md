# Video Code Guide: Stop Deploying .NET Lambdas Like This

## Overview

This demo shows how to deploy .NET Lambda functions using AWS CDK instead of manual console clicking. The code demonstrates:

- **DotNetFunction construct** - Automatic building and packaging
- **Infrastructure as C# code** - Type-safe, IntelliSense-enabled
- **One-command deployment** - Lambda + API Gateway + DynamoDB together

## Project Structure

```
CdkDeploymentDemo/
├── src/
│   └── ItemsApi/                    # Lambda function project
│       ├── Functions.cs             # API endpoints (Lambda Annotations)
│       ├── Startup.cs               # Dependency injection
│       ├── Models/
│       │   └── Item.cs              # Data models
│       └── ItemsApi.csproj
├── infra/                           # CDK infrastructure project
│   ├── Program.cs                   # CDK app entry point
│   ├── CdkDeploymentDemoStack.cs    # Infrastructure definition
│   ├── cdk.json                     # CDK configuration
│   └── CdkDeploymentDemo.Infra.csproj
└── VIDEO_CODE_GUIDE.md
```

---

## Section-by-Section Guide

### Section 1: Hook (0:00 - 1:00)

**On Screen:** AWS Console clicking montage (no code shown yet)

Record yourself:
1. Lambda Console → Create Function → Configure runtime → Upload zip
2. API Gateway → Create API → Create route → Connect to Lambda
3. IAM → Create role → Attach policies → Back to Lambda

Speed up the footage to make it feel tedious.

---

### Section 2: Reengagement - CDK Introduction (1:00 - 3:00)

**Show:** Terminal

```bash
# Create a new CDK project
cdk init app --language csharp
```

**Show:** Generated project structure in IDE

```
MyCdkApp/
├── src/
│   └── MyCdkApp/
│       ├── MyCdkApp.csproj
│       ├── Program.cs
│       └── MyCdkAppStack.cs    # <-- "This is where we define our infrastructure"
└── cdk.json
```

**Show:** Empty stack class

```csharp
public class MyCdkAppStack : Stack
{
    public MyCdkAppStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // "This is where we define our infrastructure. In C#."
    }
}
```

---

### Section 3: Setup - Building the Stack (3:00 - 6:00)

**Show:** NuGet Package Manager

```
Install-Package Amazon.CDK.Lib
Install-Package Amazon.CDK.AWS.Lambda.DotNet
```

**Script:** "The DotNetFunction construct is the key."

**Show:** Start typing the construct (build it up piece by piece)

```csharp
// Step 1: Just the function
var function = new DotNetFunction(this, "ItemsFunction", new DotNetFunctionProps
{
    ProjectDir = "../src/ItemsApi"
});
```

**Script:** "Just tell it where your Lambda project is. It handles building and packaging."

**Show:** Add DynamoDB table

```csharp
// Step 2: Add DynamoDB
var table = new Table(this, "ItemsTable", new TableProps
{
    PartitionKey = new Attribute { Name = "Id", Type = AttributeType.STRING },
    BillingMode = BillingMode.PAY_PER_REQUEST,
    RemovalPolicy = RemovalPolicy.DESTROY
});
```

**Script:** "DynamoDB table? A few more lines."

**Show:** Grant permissions

```csharp
// Step 3: Permissions - one line!
table.GrantReadWriteData(function);
```

**Script:** "Permissions? One line. Notice what we're not doing. No console clicking. No hand-written IAM policies."

**Show:** Add API Gateway

```csharp
// Step 4: API Gateway HTTP API
var api = new CfnApi(this, "ItemsApi", new CfnApiProps
{
    Name = "Items API",
    ProtocolType = "HTTP"
});

// Lambda integration
var integration = new CfnIntegration(this, "LambdaIntegration", new CfnIntegrationProps
{
    ApiId = api.Ref,
    IntegrationType = "AWS_PROXY",
    IntegrationUri = function.FunctionArn,
    PayloadFormatVersion = "2.0"
});

// Route
new CfnRoute(this, "GetItemsRoute", new CfnRouteProps
{
    ApiId = api.Ref,
    RouteKey = "GET /items",
    Target = $"integrations/{integration.Ref}"
});
```

**Show:** Complete stack (CdkDeploymentDemoStack.cs)

```csharp
// FULL STACK - Infrastructure as C#
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
        var function = new DotNetFunction(this, "ItemsFunction", new DotNetFunctionProps
        {
            ProjectDir = "../src/ItemsApi",
            Runtime = Runtime.DOTNET_8,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                { "TABLE_NAME", table.TableName }
            }
        });

        // Grant permissions - one line!
        table.GrantReadWriteData(function);

        // API Gateway HTTP API
        var api = new CfnApi(this, "ItemsApi", new CfnApiProps
        {
            Name = "Items API",
            ProtocolType = "HTTP"
        });

        // Auto-deploy stage
        new CfnStage(this, "DefaultStage", new CfnStageProps
        {
            ApiId = api.Ref,
            StageName = "$default",
            AutoDeploy = true
        });

        // Lambda integration
        var integration = new CfnIntegration(this, "LambdaIntegration", new CfnIntegrationProps
        {
            ApiId = api.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = function.FunctionArn,
            PayloadFormatVersion = "2.0"
        });

        // Grant API Gateway permission to invoke Lambda
        function.AddPermission("ApiGatewayInvoke", new Permission
        {
            Principal = new Amazon.CDK.AWS.IAM.ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{api.Ref}/*"
        });

        // Routes
        new CfnRoute(this, "GetItemsRoute", new CfnRouteProps
        {
            ApiId = api.Ref,
            RouteKey = "GET /items",
            Target = $"integrations/{integration.Ref}"
        });

        new CfnRoute(this, "PostItemsRoute", new CfnRouteProps
        {
            ApiId = api.Ref,
            RouteKey = "POST /items",
            Target = $"integrations/{integration.Ref}"
        });

        new CfnRoute(this, "GetItemByIdRoute", new CfnRouteProps
        {
            ApiId = api.Ref,
            RouteKey = "GET /items/{id}",
            Target = $"integrations/{integration.Ref}"
        });

        // Output the API endpoint
        _ = new CfnOutput(this, "ApiEndpoint", new CfnOutputProps
        {
            Value = $"https://{api.Ref}.execute-api.{this.Region}.amazonaws.com/",
            Description = "API Gateway endpoint URL"
        });
    }
}
```

---

### Section 4: Climax - Deployment (6:00 - 8:00)

**Show:** Terminal

```bash
cd infra
cdk deploy
```

**Expected Output (highlight these moments):**

```
CdkDeploymentDemoStack: building assets...

[Container: running dotnet publish]    # <-- "Building the Lambda automatically"
[Lambda code asset bundled]

CdkDeploymentDemoStack: deploying...
CdkDeploymentDemoStack: creating CloudFormation changeset...

 ✅  CdkDeploymentDemoStack

Outputs:
CdkDeploymentDemoStack.ApiEndpoint = https://abc123.execute-api.us-east-1.amazonaws.com/
```

**Script:** "One command. Lambda built, packaged, and deployed. API Gateway created. DynamoDB provisioned. IAM permissions configured."

**Show:** Test the endpoint

```bash
# Create an item
curl -X POST https://abc123.execute-api.us-east-1.amazonaws.com/items \
  -H "Content-Type: application/json" \
  -d '{"name": "Test Item", "description": "Created via CDK", "price": 29.99}'

# Get all items
curl https://abc123.execute-api.us-east-1.amazonaws.com/items
```

---

### Section 5: Goosh - Bonus Features (8:00 - 9:00)

**Show:** cdk diff

```bash
cdk diff
```

**Output:**

```
Stack CdkDeploymentDemoStack
Resources
[~] AWS::Lambda::Function ItemsFunction ItemsFunctionXXX
 └── [~] MemorySize
     ├── [-] 512
     └── [+] 1024
```

**Script:** "Want to see what will change before you deploy? cdk diff."

**Show:** Loop example (bonus code - don't need to add to project)

```csharp
// Because it's C#, you can use loops, conditionals, abstractions
var functionNames = new[] { "Orders", "Users", "Products", "Inventory" };

foreach (var name in functionNames)
{
    new DotNetFunction(this, $"{name}Function", new DotNetFunctionProps
    {
        ProjectDir = $"../src/{name}Api",
        Runtime = Runtime.DOTNET_8,
        MemorySize = 512
    });
}

// "Need to create 10 Lambda functions? That's a for loop."
```

**Show:** Native AOT configuration

```csharp
// For Native AOT (link to cold start video)
var function = new DotNetFunction(this, "ItemsFunction", new DotNetFunctionProps
{
    ProjectDir = "../src/ItemsApi",
    Runtime = Runtime.PROVIDED_AL2023,
    Architecture = Architecture.ARM_64,
    MemorySize = 512,
    MsBuildParameters = "/p:PublishAot=true"
});
```

---

### Section 6: Wrap Up (9:00 - 10:00)

**Show:** Full stack code one more time

**Script:** "We went from clicking through consoles to a single C# file that defines our entire serverless application. Lambda. API Gateway. DynamoDB. Permissions. All in C#. One command. Repeatable."

**Show:** Final API request

```bash
curl https://abc123.execute-api.us-east-1.amazonaws.com/items
```

**Script:** ".NET all the way down."

---

## Deployment Commands Reference

```bash
# First time setup (if CDK not bootstrapped in account)
cdk bootstrap

# Deploy
cd infra
cdk deploy

# See what will change
cdk diff

# Destroy (cleanup)
cdk destroy

# Synthesize CloudFormation (without deploying)
cdk synth
```

---

## Key Points to Emphasize

1. **DotNetFunction handles builds** - No separate dotnet publish step
2. **Type safety** - IntelliSense, compile-time errors
3. **GrantReadWriteData** - One line replaces manual IAM policy
4. **Repeatable** - Same result every time, any environment
5. **It's just C#** - Loops, conditionals, methods, classes

---

## Common Questions

**Q: What if I need to customize the build?**
```csharp
new DotNetFunction(this, "Function", new DotNetFunctionProps
{
    ProjectDir = "../src/MyApi",
    MsBuildParameters = "/p:Configuration=Release /p:PublishReadyToRun=true",
    BundlingOptions = new BundlingOptions
    {
        CommandHooks = new CommandHooks
        {
            // Run commands before/after bundling
        }
    }
});
```

**Q: Multiple environments?**
```csharp
// Program.cs
new CdkDeploymentDemoStack(app, "CdkDeploymentDemoStack-Dev", new StackProps
{
    Env = new Amazon.CDK.Environment { Account = "111111111111", Region = "us-east-1" }
});

new CdkDeploymentDemoStack(app, "CdkDeploymentDemoStack-Prod", new StackProps
{
    Env = new Amazon.CDK.Environment { Account = "222222222222", Region = "us-east-1" }
});
```

---

## Lambda Function Code (for reference)

The Lambda function uses Lambda Annotations (same as other videos in the series):

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/items")]
public async Task<IHttpResult> GetAllItems(ILambdaContext context)
{
    var response = await _dynamoDb.ScanAsync(new ScanRequest
    {
        TableName = _tableName
    });

    var items = response.Items.Select(MapToItem).ToList();
    return HttpResults.Ok(items);
}

[LambdaFunction]
[HttpApi(LambdaHttpMethod.Post, "/items")]
public async Task<IHttpResult> CreateItem([FromBody] CreateItemRequest request, ILambdaContext context)
{
    // Create and store item...
    return HttpResults.Created($"/items/{item.Id}", item);
}
```

---

## Visual Contrast Summary

| Manual Console | AWS CDK |
|----------------|---------|
| Click Lambda → Create | `new DotNetFunction(...)` |
| Click API Gateway → Configure | `new CfnApi(...)` |
| Click DynamoDB → Create Table | `new Table(...)` |
| Click IAM → Create Policy → Attach | `table.GrantReadWriteData(function)` |
| Repeat for staging | `cdk deploy --context env=staging` |
| Repeat for production | `cdk deploy --context env=prod` |
| Hope you remembered everything | Git diff shows exactly what changed |
