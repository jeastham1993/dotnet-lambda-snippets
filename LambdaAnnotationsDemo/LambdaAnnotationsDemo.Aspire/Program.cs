using Aspire.Hosting.AWS.Lambda;

#pragma warning disable CA2252 // Opt in to preview features

var builder = DistributedApplication.CreateBuilder(args);

var getRootFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetRoot",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetRoot_Generated::GetRoot");
var getItemsFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetItems",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetItems_Generated::GetItems");
var getItemFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("GetItem",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_GetItem_Generated::GetItem");
var createItemFunction = builder.AddAWSLambdaFunction<Projects.LambdaAnnotationsDemo>("CreateItem",
    lambdaHandler: "LambdaAnnotationsDemo::LambdaAnnotationsDemo.Functions_CreateItem_Generated::CreateItem");


builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", Aspire.Hosting.AWS.Lambda.APIGatewayType.HttpV2)
    .WithReference(getRootFunction, Method.Get, "/")
    .WithReference(getItemsFunction, Method.Get, "/items")
    .WithReference(getItemFunction, Method.Get, "/items/{id}")
    .WithReference(createItemFunction, Method.Post, "/items");

builder.Build().Run();