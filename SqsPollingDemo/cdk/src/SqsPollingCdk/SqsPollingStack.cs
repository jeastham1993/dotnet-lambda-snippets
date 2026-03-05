using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.DotNet;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace SqsPollingCdk;

public class SqsPollingStack : Stack
{
    public SqsPollingStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        const string lambdaProjectDir = "../src/SqsPollingDemo";

        // ---------------------------------------------------------------
        // WORKER SERVICE QUEUE
        // Your worker service polls this manually. Lambda is not involved.
        // Set ORDER_QUEUE_URL to the output WorkerServiceQueueUrl.
        // ---------------------------------------------------------------

        var workerServiceDlq = new Queue(this, "WorkerServiceDlq", new QueueProps
        {
            RetentionPeriod = Duration.Days(14)
        });

        var workerServiceQueue = new Queue(this, "WorkerServiceQueue", new QueueProps
        {
            // Standard visibility timeout — your worker manages this manually
            VisibilityTimeout = Duration.Seconds(30),
            // Long polling reduces empty receives and cost
            ReceiveMessageWaitTime = Duration.Seconds(20),
            DeadLetterQueue = new DeadLetterQueue
            {
                MaxReceiveCount = 3,
                Queue = workerServiceDlq
            }
        });

        // ---------------------------------------------------------------
        // LAMBDA QUEUE
        // Lambda polls this. You do not. Wire it up once — AWS does the rest.
        // ---------------------------------------------------------------

        var lambdaDlq = new Queue(this, "LambdaDlq", new QueueProps
        {
            RetentionPeriod = Duration.Days(14)
        });

        var lambdaQueue = new Queue(this, "LambdaQueue", new QueueProps
        {
            // Visibility timeout must be at least 6x the Lambda timeout.
            // Prevents a message from reappearing while Lambda is still processing it.
            VisibilityTimeout = Duration.Seconds(180),
            DeadLetterQueue = new DeadLetterQueue
            {
                MaxReceiveCount = 3,
                Queue = lambdaDlq
            }
        });

        // ---------------------------------------------------------------
        // LAMBDA FUNCTION
        // Processes orders from the queue. No polling loop. No infrastructure
        // to manage. AWS calls this function when there are messages.
        // ---------------------------------------------------------------

        var orderProcessor = new DotNetFunction(this, "OrderProcessor", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Runtime = Runtime.DOTNET_10,
            Handler = "SqsPollingDemo::SqsPollingDemo.OrderProcessorFunction_ProcessOrders_Generated::ProcessOrders",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Processes orders from SQS — Lambda handles polling, scaling, and retries"
        });

        // ---------------------------------------------------------------
        // EVENT SOURCE MAPPING
        // This single line is what replaces your polling loop.
        // "When messages arrive on lambdaQueue, call orderProcessor."
        // Lambda handles the polling, batching, concurrency, and retries.
        // ---------------------------------------------------------------

        orderProcessor.AddEventSource(new SqsEventSource(lambdaQueue, new SqsEventSourceProps
        {
            // How many messages Lambda delivers per invocation (1–10000)
            BatchSize = 10,

            // Accumulate messages for up to 5 seconds to fill a batch.
            // Reduces invocations for low-volume queues.
            MaxBatchingWindow = Duration.Seconds(5),

            // Only failed messages are retried — not the whole batch.
            // Your function returns SQSBatchResponse to report which ones failed.
            ReportBatchItemFailures = true
        }));

        // ---------------------------------------------------------------
        // BONUS: RESERVED CONCURRENCY
        // Cap how many concurrent executions Lambda allows for this function.
        // Useful when you need to protect a downstream system (e.g. a database
        // that can only handle 10 concurrent connections).
        //
        // Set it directly on the function props:
        // var orderProcessor = new DotNetFunction(this, "OrderProcessor", new DotNetFunctionProps
        // {
        //     ...
        //     ReservedConcurrentExecutions = 10  // Max 10 concurrent executions
        // });
        // ---------------------------------------------------------------

        // ---------------------------------------------------------------
        // BONUS: FIFO QUEUE
        // Use a FIFO queue when message ordering matters.
        // Lambda processes FIFO queues one message group at a time,
        // preserving order within each group.
        //
        // var orderFifoQueue = new Queue(this, "OrderFifoQueue", new QueueProps
        // {
        //     Fifo = true,
        //     ContentBasedDeduplication = true,
        //     DeadLetterQueue = new DeadLetterQueue
        //     {
        //         MaxReceiveCount = 3,
        //         Queue = new Queue(this, "OrderFifoDlq", new QueueProps { Fifo = true })
        //     }
        // });
        //
        // orderProcessor.AddEventSource(new SqsEventSource(orderFifoQueue, new SqsEventSourceProps
        // {
        //     BatchSize = 10,
        //     ReportBatchItemFailures = true
        // }));
        // ---------------------------------------------------------------

        // Outputs
        new CfnOutput(this, "WorkerServiceQueueUrl", new CfnOutputProps
        {
            Value = workerServiceQueue.QueueUrl,
            Description = "Set ORDER_QUEUE_URL to this value when running the worker service"
        });

        new CfnOutput(this, "WorkerServiceDlqUrl", new CfnOutputProps
        {
            Value = workerServiceDlq.QueueUrl,
            Description = "Dead-letter queue for the worker service"
        });

        new CfnOutput(this, "LambdaQueueUrl", new CfnOutputProps
        {
            Value = lambdaQueue.QueueUrl,
            Description = "Lambda polls this queue automatically via event source mapping"
        });

        new CfnOutput(this, "LambdaDlqUrl", new CfnOutputProps
        {
            Value = lambdaDlq.QueueUrl,
            Description = "Dead-letter queue for the Lambda function"
        });
    }
}
