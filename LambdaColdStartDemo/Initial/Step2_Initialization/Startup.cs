// Unless explicitly stated otherwise all files in this repository are licensed under the Apache License Version 2.0.
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2026 Datadog, Inc.

using Amazon.DynamoDBv2;
using Amazon.Lambda.Annotations;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaColdStartDemo.Step2_Initialization;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        Console.WriteLine("Startup starting (Initialization)...");
        
        services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient());
        services.AddHttpClient();

        var apiKey = FetchSecretSync();

        services.AddSingleton(new AppConfig()
        {
            ApiKey = apiKey,
            TableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "Products"
        });
        
        Console.WriteLine("Startup completed (Initialization)...");
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
}