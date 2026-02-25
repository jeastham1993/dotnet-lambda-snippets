using Amazon.CDK;

namespace LambdaTestingDemo.Infra;

class Program
{
    static void Main(string[] args)
    {
        var app = new App();

        // The suffix is what isolates environments from each other.
        // In a CDK pipeline this is driven by the stage name or commit hash.
        var suffix = app.Node.TryGetContext("suffix")?.ToString()
            ?? throw new ArgumentException(
                "suffix context value is required. Pass it with: -c suffix=<value>\n" +
                "  Production: -c suffix=prod\n" +
                "  Developer:  -c suffix=yourname\n" +
                "  CI:         -c suffix=$(git rev-parse --short HEAD)");

        new LambdaTestingDemoStack(app, $"LambdaTestingDemo-{suffix}", new LambdaTestingDemoStackProps
        {
            Suffix = suffix,
            Description = $"Order API Lambda — suffix: {suffix}"
        });

        app.Synth();
    }
}
