using Amazon.SQS;
using WorkerService;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register the AWS SQS client
        services.AddAWSService<IAmazonSQS>();

        // Register the worker — this runs forever, polling for messages
        services.AddHostedService<OrderProcessingWorker>();
    })
    .Build();

await host.RunAsync();
