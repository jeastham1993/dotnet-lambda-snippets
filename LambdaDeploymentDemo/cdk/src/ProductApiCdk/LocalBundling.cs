using Amazon.CDK;

namespace ProductApiCdk;

// Runs dotnet publish locally instead of inside a Docker container.
// Requires the .NET 8 SDK to be available on the machine running cdk deploy.
public class LocalBundling : ILocalBundling
{
    public bool TryBundle(string outputDir, IBundlingOptions options)
    {
        var result = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish -c Release -o {outputDir}",
            WorkingDirectory = System.IO.Path.Combine(System.AppContext.BaseDirectory, "../../../../src/ProductApi"),
            UseShellExecute = false,
        });

        result?.WaitForExit();
        return result?.ExitCode == 0;
    }
}
