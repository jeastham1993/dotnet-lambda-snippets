using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace LambdaColdStartDemo.Step2_Initialization;

/// <summary>
/// STEP 2: PROPER INITIALIZATION PLACEMENT
/// - 1024MB memory (same as Step 1)
/// - Resources moved to constructor (runs once per cold start)
/// - Handler is now thin (runs every invocation)
/// Expected cold start: ~1 second
/// Expected warm invocation: MUCH faster (no resource recreation)
///
/// LAMBDA EXECUTION MODEL:
/// Constructor → runs ONCE per container (cold start only)
/// Handler → runs EVERY invocation
///
/// Move expensive initialization to constructor = faster warm invocations
/// </summary>
public class Function
{
    // GOOD: Clients created once, reused across invocations
    private readonly AmazonDynamoDBClient _dynamoDbClient;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _tableName;

    /// <summary>
    /// Constructor runs ONCE per cold start.
    /// All expensive initialization happens here.
    /// </summary>
    public Function()
    {
        Console.WriteLine("Constructor starting - initializing resources once...");

        // GOOD: DynamoDB client created once, reused for all invocations
        Console.WriteLine("Creating DynamoDB client (one time)...");
        _dynamoDbClient = new AmazonDynamoDBClient();

        // GOOD: HttpClient created once - this is the recommended pattern
        Console.WriteLine("Creating HTTP client (one time)...");
        _httpClient = new HttpClient();

        // GOOD: Fetch secrets once at startup, cache the value
        Console.WriteLine("Fetching secrets (one time)...");
        _apiKey = FetchSecretSync();

        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "Products";

        Console.WriteLine("Constructor complete - resources ready for reuse");
    }

    private string FetchSecretSync()
    {
        try
        {
            using var secretsClient = new AmazonSecretsManagerClient();
            var secretResponse = secretsClient.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = Environment.GetEnvironmentVariable("SECRET_NAME") ?? "my-api-key"
            }).GetAwaiter().GetResult();

            return secretResponse.SecretString ?? "default-key";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not fetch secret: {ex.Message}. Using default.");
            return "default-key";
        }
    }

    /// <summary>
    /// Handler runs EVERY invocation.
    /// Now it's thin - just business logic, no initialization.
    /// </summary>
    public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation("Handler starting - resources already initialized!");

        // Business logic only - no resource creation
        context.Logger.LogInformation("Querying DynamoDB with pre-initialized client...");

        List<Dictionary<string, AttributeValue>> items;
        try
        {
            var scanResponse = await _dynamoDbClient.ScanAsync(new ScanRequest
            {
                TableName = _tableName,
                Limit = 10
            });
            items = scanResponse.Items;
            context.Logger.LogInformation($"Retrieved {items.Count} items from DynamoDB");
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"DynamoDB error: {ex.Message}");
            items = new List<Dictionary<string, AttributeValue>>();
        }

        var response = new
        {
            message = "Response from Step 2 - Initialization Optimized",
            itemCount = items.Count,
            optimization = "Resources initialized in constructor, reused across invocations",
            expectedImprovement = "Faster cold start + much faster warm invocations",
            timestamp = DateTime.UtcNow
        };

        context.Logger.LogInformation("Handler complete");

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = JsonSerializer.Serialize(response),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }
}
