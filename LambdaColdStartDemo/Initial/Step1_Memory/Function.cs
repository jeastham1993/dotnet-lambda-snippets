using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace LambdaColdStartDemo.Step1_Memory;

/// <summary>
/// STEP 1: MEMORY INCREASE
/// - 1024MB memory (see template.yaml) - MORE CPU!
/// - Code is identical to Step 0
/// - Only change is memory configuration
/// Expected cold start: ~1.5 seconds (50% improvement from memory alone)
///
/// WHY THIS WORKS:
/// Lambda allocates CPU proportionally to memory.
/// 128MB = fraction of a vCPU
/// 1024MB = nearly a full vCPU
/// More CPU = faster initialization, faster JIT compilation
/// </summary>
public class Function
{
    public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation("Handler starting - creating all resources...");

        // Still creating DynamoDB client in handler (will fix in Step 2)
        context.Logger.LogInformation("Creating DynamoDB client...");
        var dynamoDbClient = new AmazonDynamoDBClient();

        // Still creating HTTP client in handler (will fix in Step 2)
        context.Logger.LogInformation("Creating HTTP client...");
        var httpClient = new HttpClient();

        // Still fetching secrets in handler (will fix in Step 2)
        context.Logger.LogInformation("Fetching secrets...");
        var secretsClient = new AmazonSecretsManagerClient();
        string apiKey;
        try
        {
            var secretResponse = await secretsClient.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = Environment.GetEnvironmentVariable("SECRET_NAME") ?? "my-api-key"
            });
            apiKey = secretResponse.SecretString ?? "default-key";
            context.Logger.LogInformation("Secret retrieved");
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning($"Could not fetch secret: {ex.Message}. Using default.");
            apiKey = "default-key";
        }

        // External HTTP call
        context.Logger.LogInformation("Fetching external configuration...");
        string externalConfig;
        try
        {
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            var configResponse = await httpClient.GetStringAsync("https://httpbin.org/json");
            externalConfig = configResponse;
            context.Logger.LogInformation("External config retrieved");
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning($"Could not fetch external config: {ex.Message}");
            externalConfig = "{}";
        }

        // Business logic
        context.Logger.LogInformation("Querying DynamoDB...");
        var tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "Products";

        List<Dictionary<string, AttributeValue>> items;
        try
        {
            var scanResponse = await dynamoDbClient.ScanAsync(new ScanRequest
            {
                TableName = tableName,
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
            message = "Response from Step 1 - Memory Optimized",
            itemCount = items.Count,
            optimization = "1024MB memory (vs 128MB baseline)",
            expectedImprovement = "~50% faster cold start",
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
