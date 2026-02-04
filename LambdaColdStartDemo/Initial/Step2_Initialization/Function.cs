using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Annotations;
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
public class Function(IAmazonDynamoDB dynamoDbClient, HttpClient httpClient, AppConfig appConfig)
{
    /// <summary>
    /// Handler runs EVERY invocation.
    /// Now it's thin - just business logic, no initialization.
    /// </summary>
    [LambdaFunction]
    public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation("Handler starting - resources already initialized!");

        // Business logic only - no resource creation
        context.Logger.LogInformation("Querying DynamoDB with pre-initialized client...");

        List<Dictionary<string, AttributeValue>> items;
        try
        {
            var scanResponse = await dynamoDbClient.ScanAsync(new ScanRequest
            {
                TableName = appConfig.TableName,
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
