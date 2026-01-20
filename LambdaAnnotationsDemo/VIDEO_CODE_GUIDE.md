# Video Code Guide: Building a Complete .NET API on Lambda in 10 Minutes

This document maps the codebase to specific sections of the YouTube video, making it easy to follow along or reference code during editing.

---

## Project Structure Overview

```
LambdaAnnotationsDemo/
├── Functions.cs              # All Lambda endpoints (main demo file)
├── Startup.cs                # Dependency injection configuration
├── Models/
│   ├── Item.cs               # Response model
│   └── CreateItemRequest.cs  # POST request body model
├── Services/
│   ├── IItemService.cs       # Service interface
│   └── ItemService.cs        # Service implementation
├── template.yaml             # SAM/CloudFormation template
├── samconfig.toml            # SAM CLI configuration
└── LambdaAnnotationsDemo.csproj
```

---

## Video Section → Code Mapping

### Hook (0:00 - 1:00)
**"I'm going to build a complete .NET API on AWS Lambda in 10 minutes..."**

No code shown yet - this is the introduction. The finished API preview could show:
- `GET /` - Simple hello message
- `GET /items` - List all items
- `GET /items/{id}` - Get single item
- `POST /items` - Create new item

---

### Reengagement (1:00 - 3:00)
**"First, let's create the project. I'm using the Lambda Annotations template."**

**Terminal Command:**
```bash
dotnet new serverless.Annotations -n LambdaAnnotationsDemo
```

**"Look at this code. [HttpApi], [FromBody] - this is just ASP.NET Core."**

Show the familiar attributes in `Functions.cs:1-20`:
```csharp
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
```

**"Let's add our first endpoint. A simple GET."**

File: `Functions.cs:21-26`
```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/")]
public string GetRoot()
{
    return "Hello from Lambda Annotations!";
}
```

**Key talking points:**
- `[LambdaFunction]` - marks this as a Lambda function
- `[HttpApi]` - familiar routing attribute (like `[HttpGet]` in ASP.NET)
- Regular method signature - no Lambda-specific types required

**Local testing:**
```bash
sam build
sam local start-api
curl http://localhost:3000/
```

---

### Setup (3:00 - 6:00)
**"Now let's build this out properly. A POST endpoint that accepts JSON."**

File: `Functions.cs:45-51`
```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Post, "/items")]
public IHttpResult CreateItem([FromBody] CreateItemRequest request)
{
    var item = _itemService.CreateItem(request);
    return HttpResults.Created($"/items/{item.Id}", item);
}
```

**Supporting model** - `Models/CreateItemRequest.cs`:
```csharp
public class CreateItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

**"Path parameters? Even simpler than ASP.NET Core."**

File: `Functions.cs:34-46`
```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/items/{id}")]
public IHttpResult GetItem(string id)
{
    var item = _itemService.GetItem(id);

    if (item is null)
    {
        return HttpResults.NotFound($"Item with id '{id}' not found");
    }

    return HttpResults.Ok(item);
}
```

**Key talking points:**
- Route parameters are automatically bound when the parameter name matches the route template
- No `[FromRoute]` attribute needed - even simpler than ASP.NET Core
- `IHttpResult` gives you control over HTTP responses
- `HttpResults.Ok()`, `HttpResults.NotFound()`, `HttpResults.Created()` - familiar patterns

**"Dependency injection. Again, exactly what you're used to."**

File: `Startup.cs` (entire file)
```csharp
using Amazon.Lambda.Annotations;
using LambdaAnnotationsDemo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaAnnotationsDemo;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IItemService, ItemService>();
    }
}
```

**Injection in Functions** - `Functions.cs:14-19`:
```csharp
public class Functions
{
    private readonly IItemService _itemService;

    public Functions(IItemService itemService)
    {
        _itemService = itemService;
    }
```

**Key talking points:**
- `[LambdaStartup]` attribute marks the startup class
- `ConfigureServices` method signature is identical to ASP.NET Core
- Constructor injection works naturally

---

### Climax (6:00 - 8:00)
**"Alright, we've got a real API here. Let's deploy it."**

**Deployment commands:**
```bash
sam build
sam deploy --guided  # First time only
# or
sam deploy           # Subsequent deploys
```

**"Notice I haven't touched the CloudFormation template..."**

File: `template.yaml` - Show how the source generator creates handler references:
```yaml
GetRootFunction:
  Type: AWS::Serverless::Function
  Properties:
    Handler: LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetRoot_Generated::GetRoot
```

**Key talking point:** The `_Generated` suffix indicates the source generator created the Lambda handler wrapper automatically.

**"Moment of truth."**

Testing the deployed API:
```bash
# Get the API URL from the stack outputs
API_URL=$(aws cloudformation describe-stacks --stack-name lambda-annotations-demo \
  --query 'Stacks[0].Outputs[?OutputKey==`ApiUrl`].OutputValue' --output text)

# Test endpoints
curl $API_URL
curl $API_URL/items
curl -X POST $API_URL/items -H "Content-Type: application/json" \
  -d '{"name":"Test Item","description":"Created via POST"}'
curl $API_URL/items/{id-from-previous-response}
```

---

### Goosh / Bonus (8:00 - 9:00)
**"One more thing - watch how easy it is to add another endpoint now."**

**Option A: Add a DELETE endpoint**

Add to `Functions.cs`:
```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Delete, "/items/{id}")]
public IHttpResult DeleteItem([FromRoute] string id)
{
    // Implementation here
    return HttpResults.NoContent();
}
```

**Option B: Show attribute configuration options**

```csharp
[LambdaFunction(MemorySize = 1024, Timeout = 60)]
[HttpApi(LambdaHttpMethod.Get, "/heavy-operation")]
public string HeavyOperation()
{
    // You can configure Lambda settings directly in attributes
}
```

**Option C: Show the generated code**

After building, show the generated file at:
```
obj/Debug/net8.0/generated/Amazon.Lambda.Annotations.SourceGenerator/
  Amazon.Lambda.Annotations.SourceGenerator.Generator/
  Functions_GetRoot_Generated.g.cs
```

The generated code shows all the Lambda boilerplate you didn't have to write:
- API Gateway request parsing
- Response formatting
- Dependency injection resolution
- Serialization handling

---

### Wrap Up (9:00 - 10:00)
**"Lambda Annotations. Same patterns as ASP.NET Core."**

Summary of patterns demonstrated:

| ASP.NET Core | Lambda Annotations |
|--------------|-------------------|
| `[HttpGet("/")]` | `[HttpApi(LambdaHttpMethod.Get, "/")]` |
| `[FromBody]` | `[FromBody]` |
| `[FromRoute]` | Automatic (parameter name matches route) |
| `Startup.ConfigureServices()` | `Startup.ConfigureServices()` |
| Constructor injection | Constructor injection |

**Final API endpoints:**
```
GET  /           → Hello message
GET  /items      → List all items
GET  /items/{id} → Get single item
POST /items      → Create new item
```

---

## Commands Reference

| Action | Command |
|--------|---------|
| Create project | `dotnet new serverless.Annotations -n LambdaAnnotationsDemo` |
| Build | `sam build` |
| Run locally | `sam local start-api` |
| Deploy (first time) | `sam deploy --guided` |
| Deploy (subsequent) | `sam deploy` |
| Watch mode | `sam sync --watch` |

---

## Files to Show On Screen

| Video Moment | File | Lines |
|--------------|------|-------|
| "Look at the familiar attributes" | Functions.cs | 1-10, 21-26 |
| "POST with FromBody" | Functions.cs | 45-51 |
| "Path parameters - automatic binding" | Functions.cs | 34-46 |
| "Dependency injection setup" | Startup.cs | entire file |
| "Constructor injection" | Functions.cs | 14-19 |
| "Auto-generated CloudFormation" | template.yaml | Resources section |
