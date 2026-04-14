using Aspire.Hosting.AWS.Lambda;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Aspire.Hosting;

#pragma warning disable CA2252 // Opt in to preview features

var builder = DistributedApplication.CreateBuilder(args);

var dynamoDb = builder.AddAWSDynamoDBLocal("DynamoDB");

builder.Eventing.Subscribe<ResourceReadyEvent>(dynamoDb.Resource, async (evt, ct) =>
{
    var serviceUrl = dynamoDb.Resource.GetEndpoint("http").Url;
    var client = new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl });
    await client.CreateTableAsync(new CreateTableRequest
    {
        TableName = "Items",
        KeySchema = [new KeySchemaElement("Id", KeyType.HASH)],
        AttributeDefinitions = [new AttributeDefinition("Id", ScalarAttributeType.S)],
        BillingMode = BillingMode.PAY_PER_REQUEST
    }, ct);
});

var getRootFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetRoot",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetRoot_Generated::GetRoot")
    .WithReference(dynamoDb)
    .WaitFor(dynamoDb)
    .WithEnvironment("ITEMS_TABLE_NAME", "Items");

var getItemsFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetItems",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetItems_Generated::GetItems")
    .WithReference(dynamoDb)
    .WaitFor(dynamoDb)
    .WithEnvironment("ITEMS_TABLE_NAME", "Items");

var getItemFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetItem",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetItem_Generated::GetItem")
    .WithReference(dynamoDb)
    .WaitFor(dynamoDb)
    .WithEnvironment("ITEMS_TABLE_NAME", "Items");

var createItemFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("CreateItem",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_CreateItem_Generated::CreateItem")
    .WithReference(dynamoDb)
    .WaitFor(dynamoDb)
    .WithEnvironment("ITEMS_TABLE_NAME", "Items");

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(getRootFunction, Method.Get, "/")
    .WithReference(getItemsFunction, Method.Get, "/items")
    .WithReference(getItemFunction, Method.Get, "/items/{id}")
    .WithReference(createItemFunction, Method.Post, "/items");

builder.Build().Run();
