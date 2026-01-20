using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaColdStartDemo.Step0_Baseline;

/// <summary>
/// STEP 0: THE BASELINE - Everything wrong
/// - 128MB memory (see template.yaml)
/// - All initialization in the handler
/// - New clients created every invocation
/// - Secrets fetched every invocation
/// Expected cold start: ~3 seconds
/// </summary>
public class Function
{
    public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation("Handler starting - creating all resources...");

        // BAD: Creating DynamoDB client in handler - happens every cold start AND warm invocation
        context.Logger.LogInformation("Creating DynamoDB client...");
        var dynamoDbClient = new AmazonDynamoDBClient();

        // BAD: Creating HTTP client in handler - expensive, should be reused
        context.Logger.LogInformation("Creating HTTP client...");
        var httpClient = new HttpClient();

        // BAD: Fetching secrets in handler - network call every invocation
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

        // BAD: Making external HTTP call that could be cached or optimized
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

        // Actual business logic - query DynamoDB
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

        // Build response
        var response = new
        {
            message = "Response from Step 0 - Baseline (slow)",
            itemCount = items.Count,
            coldStartPenalty = "All resources created in handler",
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
