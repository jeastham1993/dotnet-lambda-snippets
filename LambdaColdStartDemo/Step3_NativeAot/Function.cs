using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using LambdaColdStartDemo.NativeAot;

// Configure Lambda Annotations for Native AOT
[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<ApiSerializerContext>))]
[assembly: LambdaGlobalProperties(GenerateMain = true, Runtime = "provided.al2023")]

namespace LambdaColdStartDemo.NativeAot;

/// <summary>
/// STEP 3: NATIVE AOT WITH LAMBDA ANNOTATIONS
/// - 1024MB memory
/// - Proper initialization placement
/// - Native AOT compilation with Lambda Annotations
/// Expected cold start: <300ms
///
/// LAMBDA ANNOTATIONS + NATIVE AOT:
/// - GenerateMain = true creates the bootstrap automatically
/// - Source generator handles all the AOT-compatible code generation
/// - No manual Program.cs needed
/// - Same familiar [HttpApi] attributes
/// </summary>
public class Functions(IAmazonDynamoDB dynamoDbClient, HttpClient httpClient, AppConfig appConfig)
{
    /// <summary>
    /// Lambda Annotations makes AOT feel just like regular Lambda development.
    /// Same [HttpApi] attribute pattern you already know.
    /// </summary>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/step3")]
    public async Task<ApiResponse> GetProducts()
    {
        Console.WriteLine("Handler starting (Native AOT with Annotations)...");

        List<Dictionary<string, AttributeValue>> items;
        try
        {
            var scanResponse = await dynamoDbClient.ScanAsync(new ScanRequest
            {
                TableName = appConfig.TableName,
                Limit = 10
            });
            items = scanResponse.Items;
            Console.WriteLine($"Retrieved {items.Count} items from DynamoDB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DynamoDB error: {ex.Message}");
            items = new List<Dictionary<string, AttributeValue>>();
        }

        Console.WriteLine("Handler complete (Native AOT with Annotations)");

        return new ApiResponse
        {
            Message = "Response from Step 3 - Native AOT with Lambda Annotations",
            ItemCount = items.Count,
            Optimization = "Native AOT + Lambda Annotations - no JIT, familiar patterns",
            ExpectedColdStart = "<300ms",
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Response model for AOT-compatible serialization
/// </summary>
public class ApiResponse
{
    public string Message { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string Optimization { get; set; } = string.Empty;
    public string ExpectedColdStart { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// SOURCE-GENERATED SERIALIZATION CONTEXT
/// Required for Native AOT - replaces reflection-based serialization.
/// Lambda Annotations uses this automatically when you configure
/// SourceGeneratorLambdaJsonSerializer in the assembly attribute.
/// </summary>
[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
public partial class ApiSerializerContext : JsonSerializerContext
{
}
