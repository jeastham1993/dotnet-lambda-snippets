using Aspire.Hosting.AWS.Lambda;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Aspire.Hosting;

#pragma warning disable CA2252 // Opt in to preview features

const string ItemsTableName = "Items";

var builder = DistributedApplication.CreateBuilder(args);

var dynamoDb = builder.AddAWSDynamoDBLocal("DynamoDB");

builder.Eventing.Subscribe<ResourceReadyEvent>(dynamoDb.Resource, async (evt, ct) =>
{
    var serviceUrl = dynamoDb.Resource.GetEndpoint("http").Url;
    using var client = new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl });
    try
    {
        await client.CreateTableAsync(new CreateTableRequest
        {
            TableName = ItemsTableName,
            KeySchema = [new KeySchemaElement("Id", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("Id", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        }, ct);
    }
    catch (ResourceInUseException)
    {
        // Table already exists — idempotent on Aspire restart
    }
});

var getRootFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetRoot",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetRoot_Generated::GetRoot")
    .WithReference(dynamoDb)
    .WaitFor(dynamoDb)
    .WithEnvironment("ITEMS_TABLE_NAME", ItemsTableName);

var getItemsFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetItems",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetItems_Generated::GetItems")
    .WithReference(dynamoDb)
    .WaitFor(dynamoDb)
    .WithEnvironment("ITEMS_TABLE_NAME", ItemsTableName);

var getItemFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetItem",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetItem_Generated::GetItem")
    .WithReference(dynamoDb)
    .WaitFor(dynamoDb)
    .WithEnvironment("ITEMS_TABLE_NAME", ItemsTableName);

var createItemFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("CreateItem",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_CreateItem_Generated::CreateItem")
    .WithReference(dynamoDb)
    .WaitFor(dynamoDb)
    .WithEnvironment("ITEMS_TABLE_NAME", ItemsTableName);

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(getRootFunction, Method.Get, "/")
    .WithReference(getItemsFunction, Method.Get, "/items")
    .WithReference(getItemFunction, Method.Get, "/items/{id}")
    .WithReference(createItemFunction, Method.Post, "/items");

builder.Build().Run();
