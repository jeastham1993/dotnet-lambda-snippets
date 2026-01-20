# Video Code Guide: This Lambda Takes 3 Seconds to Cold Start (Let's Fix It)

This document maps the codebase to specific sections of the YouTube video, making it easy to follow along or reference code during editing.

---

## Project Structure Overview

```
LambdaColdStartDemo/
├── Step0_Baseline/
│   └── Function.cs           # THE DISASTER - 128MB, init in handler (~3s)
│
├── Step1_Memory/
│   └── Function.cs           # Memory bump only - 1024MB (~1.5s)
│
├── Step2_Initialization/
│   └── Function.cs           # Proper init placement (~1s)
│
├── Step3_NativeAot/
│   ├── Function.cs           # Native AOT with Lambda Annotations (<300ms)
│   └── LambdaColdStartDemo.NativeAot.csproj
│
├── template.yaml             # All four Lambda configurations
└── LambdaColdStartDemo.csproj
```

---

## The Cold Start Journey

| Step | Memory | Initialization | AOT | Expected Cold Start |
|------|--------|----------------|-----|---------------------|
| 0 - Baseline | 128MB | In handler | No | ~3 seconds |
| 1 - Memory | 1024MB | In handler | No | ~1.5 seconds |
| 2 - Initialization | 1024MB | In constructor | No | ~1 second |
| 3 - Native AOT | 1024MB | In constructor | Yes | <300ms |

---

## Video Section → Code Mapping

### Hook (0:00 - 1:00)
**Show the Lambda invoke and the painful wait. No talking for first 5 seconds.**

Invoke the baseline function:
```bash
# Force cold start by updating function (or wait for container timeout)
curl $API_URL/step0
```

**Show the cold start time: ~3000ms**

**"3 second cold start. Default .NET Lambda. By the end of this video, we're getting this under 300 milliseconds."**

Flash forward briefly to Step 3 result: <300ms

---

### Step 1: Memory (1:00 - 3:00)
**"Step one. The easiest performance win you'll ever get. Memory."**

**Show the Lambda configuration:**

File: `template.yaml` - Step 0 vs Step 1

```yaml
# Step 0 - Baseline
Step0BaselineFunction:
  Properties:
    MemorySize: 128  # LOW MEMORY = LOW CPU = SLOW

# Step 1 - Memory Optimized
Step1MemoryFunction:
  Properties:
    MemorySize: 1024  # MORE MEMORY = MORE CPU = FASTER
```

**"Lambda allocates CPU proportionally to memory. 128MB isn't just low memory - it's a fraction of a CPU core."**

**Visual to show on screen:**
```
Memory → CPU Allocation
128MB  → ~0.08 vCPU
512MB  → ~0.33 vCPU
1024MB → ~0.67 vCPU
1769MB → 1 full vCPU
```

**"Let's bump this to 1GB."**

**Invoke Step 1:**
```bash
curl $API_URL/step1
```

**Show result: ~1500ms (50% improvement)**

**"That's half the cold start time from changing one number."**

**Mention Lambda Power Tuning:**
```
GitHub: alexcasalboni/aws-lambda-power-tuning
```

Show example Power Tuning output graph briefly.

---

### Step 2: Initialization (3:00 - 6:00)
**"Step two. Let's look at what this Lambda is actually doing when it starts up."**

**Show the problematic code in** `Step0_Baseline/Function.cs:25-65`:

```csharp
public async Task<APIGatewayProxyResponse> Handler(...)
{
    // BAD: Creating DynamoDB client in handler
    context.Logger.LogInformation("Creating DynamoDB client...");
    var dynamoDbClient = new AmazonDynamoDBClient();

    // BAD: Creating HTTP client in handler
    context.Logger.LogInformation("Creating HTTP client...");
    var httpClient = new HttpClient();

    // BAD: Fetching secrets in handler - network call every invocation
    context.Logger.LogInformation("Fetching secrets...");
    var secretsClient = new AmazonSecretsManagerClient();
    // ... fetches secret every time
}
```

**"Database connection - created every cold start. HTTP client - new instance every cold start. Secrets - fetched every time."**

**Show the execution model diagram:**
```
┌─────────────────────────────────────────────────┐
│  Constructor                                     │
│  Runs ONCE per cold start                       │
│  ─────────────────────────────────────────────  │
│  Perfect for: AWS clients, HttpClient, secrets  │
└────────────────────┬────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────┐
│  Handler                                         │
│  Runs EVERY invocation                          │
│  ─────────────────────────────────────────────  │
│  Should contain: Business logic only            │
└─────────────────────────────────────────────────┘
```

**"Let's move these to where they belong."**

**Show the refactored code** `Step2_Initialization/Function.cs:25-60`:

```csharp
public class Function
{
    // GOOD: Clients created once, reused across invocations
    private readonly AmazonDynamoDBClient _dynamoDbClient;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    /// <summary>
    /// Constructor runs ONCE per cold start.
    /// </summary>
    public Function()
    {
        Console.WriteLine("Constructor starting - initializing resources once...");

        // GOOD: DynamoDB client created once
        _dynamoDbClient = new AmazonDynamoDBClient();

        // GOOD: HttpClient created once
        _httpClient = new HttpClient();

        // GOOD: Fetch secrets once at startup
        _apiKey = FetchSecretSync();
    }

    /// <summary>
    /// Handler is now thin - just business logic
    /// </summary>
    public async Task<APIGatewayProxyResponse> Handler(...)
    {
        // No resource creation - just use pre-initialized clients
        var scanResponse = await _dynamoDbClient.ScanAsync(...);
        // ...
    }
}
```

**Key points to highlight:**
- Resources as private readonly fields
- Constructor does all initialization
- Handler just uses pre-initialized resources

**Invoke Step 2:**
```bash
curl $API_URL/step2
```

**Show result: ~1000ms**

**"Cold start improved. But warm invocations are faster too - they're not recreating resources every time."**

---

### Step 3: Native AOT - The Climax (6:00 - 8:00)
**"Step three. Native AOT. This is the big one."**

**Explain AOT vs JIT:**
```
Standard .NET Lambda:
┌─────────────────────────────────────────────┐
│ 1. Lambda loads your .dll (IL bytecode)     │
│ 2. JIT compiler converts IL → native code   │ ← Takes time!
│ 3. Code executes                            │
└─────────────────────────────────────────────┘

Native AOT Lambda:
┌─────────────────────────────────────────────┐
│ 1. Lambda loads native binary               │
│ 2. Code executes immediately                │ ← No JIT step!
└─────────────────────────────────────────────┘
```

**"This requires some code changes. But with Lambda Annotations, it's surprisingly familiar."**

**Show the assembly attributes** `Step3_NativeAot/Function.cs:13-15`:

```csharp
// Configure Lambda Annotations for Native AOT
[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<ApiSerializerContext>))]
[assembly: LambdaGlobalProperties(GenerateMain = true, Runtime = "provided.al2023")]
```

**Key point:** `GenerateMain = true` - Lambda Annotations generates all the bootstrap code for you. No manual `Program.cs` needed.

**Show project file changes** `Step3_NativeAot/LambdaColdStartDemo.NativeAot.csproj`:

```xml
<PropertyGroup>
  <!-- NATIVE AOT CONFIGURATION -->
  <PublishAot>true</PublishAot>
  <StripSymbols>true</StripSymbols>

  <!-- Required for Lambda Annotations AOT - generates Main method -->
  <OutputType>exe</OutputType>
</PropertyGroup>
```

**"The handler code looks exactly like what we've been writing all along."**

**Show the handler** `Step3_NativeAot/Function.cs:74-76`:

```csharp
[LambdaFunction]
[HttpApi(LambdaHttpMethod.Get, "/step3")]
public async Task<ApiResponse> GetProducts()
{
    // Same familiar code - Lambda Annotations handles the AOT complexity
}
```

**"Source-generated serialization - tell the compiler which types you need."**

**Show serialization context** `Step3_NativeAot/Function.cs:128-131`:

```csharp
/// <summary>
/// SOURCE-GENERATED SERIALIZATION CONTEXT
/// Required for Native AOT - replaces reflection-based serialization
/// </summary>
[JsonSerializable(typeof(ApiResponse))]
public partial class ApiSerializerContext : JsonSerializerContext
{
}
```

**"There are tradeoffs - not everything works with AOT."**

Show brief list:
- No runtime reflection
- Some NuGet packages not compatible
- Longer build times
- Platform-specific binaries

**"Let's build and deploy."**

```bash
sam build
sam deploy
```

**"Moment of truth."**

```bash
curl $API_URL/step3
```

**Show result: <300ms**

**"Under 300 milliseconds. Same Lambda. Same functionality. Ten times faster."**

---

### Final Comparison

**Show the stair step progression:**

```
Cold Start Time
     │
3.0s ┤████████████████████████████████████  Step 0: Baseline
     │
2.0s ┤
     │
1.5s ┤██████████████████████████  Step 1: Memory
     │
1.0s ┤████████████████████  Step 2: Initialization
     │
0.3s ┤██████  Step 3: Native AOT
     │
     └─────────────────────────────────────────
```

---

### Goosh / Bonus (8:00 - 9:00)
**"Quick mention of other options..."**

**ReadyToRun (middle ground):**
```xml
<!-- In .csproj - not full AOT but partial pre-compilation -->
<PublishReadyToRun>true</PublishReadyToRun>
```

**Graviton/ARM64:**
```yaml
# In template.yaml
Architectures:
  - arm64  # Instead of x86_64
```
Benefits: Better price/performance, often faster.

---

### Wrap Up (9:00 - 10:00)
**"Memory configuration - the easy win. Start at 1GB, use Power Tuning to optimize."**

**"Initialization placement - move reusable resources out of your handler."**

**"Native AOT - the biggest gain, but requires code changes."**

**Final side-by-side:**

| Step | Change | Cold Start |
|------|--------|------------|
| Baseline | Default settings | ~3000ms |
| +Memory | 128MB → 1024MB | ~1500ms |
| +Init | Resources to constructor | ~1000ms |
| +AOT | Native compilation | <300ms |

**End on the numbers: "3 seconds to under 300 milliseconds."**

---

## Deployment Commands

```bash
# Build all steps
sam build

# Deploy
sam deploy --guided  # First time
sam deploy           # Subsequent

# Test each step (force cold start by waiting or updating)
curl $API_URL/step0   # ~3s cold start
curl $API_URL/step1   # ~1.5s cold start
curl $API_URL/step2   # ~1s cold start
curl $API_URL/step3   # <300ms cold start

# Force cold start by updating env var
aws lambda update-function-configuration \
  --function-name ColdStartDemo-Step0-Baseline \
  --environment "Variables={TABLE_NAME=Products,FORCE_COLD_START=$(date +%s)}"
```

---

## Files to Show On Screen

| Video Moment | File | What to Highlight |
|--------------|------|-------------------|
| Baseline code | Step0_Baseline/Function.cs | All init in handler (lines 25-65) |
| Memory config | template.yaml | MemorySize: 128 vs 1024 |
| Init pattern | Step2_Initialization/Function.cs | Constructor vs Handler split |
| AOT project | Step3_NativeAot/*.csproj | PublishAot, OutputType=exe |
| AOT assembly attrs | Step3_NativeAot/Function.cs | LambdaGlobalProperties (line 15) |
| AOT handler | Step3_NativeAot/Function.cs | Same [HttpApi] pattern (lines 74-76) |
| Serialization | Step3_NativeAot/Function.cs | JsonSerializerContext (lines 128-131) |

---

## Key Numbers to Display

- **Baseline**: ~3000ms
- **After Memory**: ~1500ms (50% improvement)
- **After Init**: ~1000ms (67% improvement from baseline)
- **After AOT**: <300ms (90% improvement from baseline)

These are representative numbers - actual results will vary based on region, account, and specific code.
