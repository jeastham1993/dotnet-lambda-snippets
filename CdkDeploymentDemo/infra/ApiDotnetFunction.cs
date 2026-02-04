// Unless explicitly stated otherwise all files in this repository are licensed under the Apache License Version 2.0.
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2026 Datadog, Inc.

using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.DotNet;
using Constructs;

namespace CdkDeploymentDemo.Infra;

public class ApiDotnetFunctionProps : DotNetFunctionProps
{
    public CfnApi Api { get; set; }

    public string RouteKey { get; set; }

    public string Region { get; set; }

    public string Account { get; set; }
}

public class ApiDotnetFunction : DotNetFunction
{
    public ApiDotnetFunction(Construct scope, string id, ApiDotnetFunctionProps props) : base(scope, id, props)
    {
        if (string.IsNullOrEmpty(props.RouteKey) || props.Api == null || string.IsNullOrEmpty(props.Region) ||
            string.IsNullOrEmpty(props.Account))
            throw new ArgumentException(
                "RouteKey, Api, Region, and Account must be provided in ApiDotnetFunctionProps");

        // Default memory to 1024 for optimal performance with API Gateway
        if (props.MemorySize is null)
        {
            props.MemorySize = 1024;
        }

        var apiIntegration = new CfnIntegration(this, $"{id}Integration", new CfnIntegrationProps
        {
            ApiId = props.Api.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = FunctionArn,
            PayloadFormatVersion = "2.0"
        });
        // Grant API Gateway permission to invoke Lambda
        AddPermission("ApiGatewayInvoke", new Permission
        {
            Principal = new Amazon.CDK.AWS.IAM.ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = $"arn:aws:execute-api:{props.Region}:{props.Account}:{props.Api.Ref}/*"
        });

        Route = new CfnRoute(this, "GetItemsRoute", new CfnRouteProps
        {
            ApiId = props.Api.Ref,
            RouteKey = props.RouteKey,
            Target = $"integrations/{apiIntegration.Ref}"
        });
    }

    public CfnRoute Route { get; set; }
}