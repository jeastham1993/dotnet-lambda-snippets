using OpenTelemetry;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LambdaAnnotationsDemo;

internal static class Observability
{
    internal const string ServiceName = "lambda-annotations-demo";

    internal static readonly ActivitySource Source = new(ServiceName);

    internal static readonly TracerProvider TracerProvider =
        Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName))
            .AddAWSLambdaConfigurations(o => o.DisableAwsXRayContextExtraction = true)
            .AddAWSInstrumentation()
            .AddSource(ServiceName)
            .AddOtlpExporter()
            .Build()!;
}
