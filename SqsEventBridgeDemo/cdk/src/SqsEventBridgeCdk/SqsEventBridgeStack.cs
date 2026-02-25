using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.DotNet;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace SqsEventBridgeCdk;

public class SqsEventBridgeStack : Stack
{
    public SqsEventBridgeStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // The Lambda project shared by all functions
        const string lambdaProjectDir = "../src/SqsEventBridgeDemo";

        // ---------------------------------------------------------------
        // DIRECT INVOCATION
        // Shows the tight coupling problem: if PaymentFunction is slow,
        // WorkflowFunction times out and the error cascades upstream.
        // ---------------------------------------------------------------

        var paymentFunction = new DotNetFunction(this, "PaymentFunction", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.DirectInvocation.PaymentFunction_ProcessPayment_Generated::ProcessPayment",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Simulates a slow downstream payment service"
        });

        var workflowFunction = new DotNetFunction(this, "WorkflowFunction", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.DirectInvocation.WorkflowFunction_PlaceOrder_Generated::PlaceOrder",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Calls PaymentFunction directly — tight coupling demo",
            Environment = new Dictionary<string, string>
            {
                // The ARN of the Lambda to call synchronously
                ["PAYMENT_FUNCTION_ARN"] = paymentFunction.FunctionArn
            }
        });

        // WorkflowFunction must be allowed to invoke PaymentFunction
        paymentFunction.GrantInvoke(workflowFunction);

        // ---------------------------------------------------------------
        // SQS
        // Shows decoupling: producer returns immediately, consumer
        // processes at its own pace, SQS absorbs bursts automatically.
        // ---------------------------------------------------------------

        var orderQueue = new Queue(this, "OrderQueue", new QueueProps
        {
            // Visibility timeout should be at least 6x the Lambda timeout
            VisibilityTimeout = Duration.Seconds(180),
            // Long polling reduces empty receives and cost
            ReceiveMessageWaitTime = Duration.Seconds(20),
            // Dead-letter queue for messages that fail after maxReceiveCount attempts
            DeadLetterQueue = new DeadLetterQueue
            {
                MaxReceiveCount = 3,
                Queue = new Queue(this, "OrderDlq", new QueueProps
                {
                    RetentionPeriod = Duration.Days(14)
                })
            }
        });

        var orderProducer = new DotNetFunction(this, "OrderProducer", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.Sqs.OrderProducerFunction_PlaceOrder_Generated::PlaceOrder",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Sends orders to SQS — returns immediately, decoupled from processing",
            Environment = new Dictionary<string, string>
            {
                ["ORDER_QUEUE_URL"] = orderQueue.QueueUrl
            }
        });

        var orderConsumer = new DotNetFunction(this, "OrderConsumer", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.Sqs.OrderConsumerFunction_ProcessOrders_Generated::ProcessOrders",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Processes orders from SQS with partial batch failure support"
        });

        // Grant the producer permission to send messages
        orderQueue.GrantSendMessages(orderProducer);

        // Wire SQS → Lambda with event source mapping.
        // ReportBatchItemFailures means only failed messages are retried —
        // successfully processed messages in the same batch are not reprocessed.
        orderConsumer.AddEventSource(new SqsEventSource(orderQueue, new SqsEventSourceProps
        {
            BatchSize = 10,
            ReportBatchItemFailures = true
        }));

        // ---------------------------------------------------------------
        // EVENTBRIDGE
        // Shows fan-out: one order.placed event triggers three independent
        // consumers simultaneously — none of them know about each other.
        // ---------------------------------------------------------------

        var orderEventBus = new EventBus(this, "OrderEventBus", new EventBusProps
        {
            EventBusName = "order-events"
        });

        var orderPublisher = new DotNetFunction(this, "OrderPublisher", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.EventBridge.OrderPublisherFunction_PlaceOrder_Generated::PlaceOrder",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Publishes order.placed events to EventBridge — zero knowledge of consumers",
            Environment = new Dictionary<string, string>
            {
                ["EVENT_BUS_NAME"] = orderEventBus.EventBusName
            }
        });

        // Grant the publisher permission to put events on the bus
        orderEventBus.GrantPutEventsTo(orderPublisher);

        var fulfilmentFunction = new DotNetFunction(this, "FulfilmentFunction", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.EventBridge.FulfilmentFunction_HandleOrderPlaced_Generated::HandleOrderPlaced",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Handles fulfilment when an order is placed"
        });

        var notificationsFunction = new DotNetFunction(this, "NotificationsFunction", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.EventBridge.NotificationsFunction_HandleOrderPlaced_Generated::HandleOrderPlaced",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Sends notifications when an order is placed"
        });

        var analyticsFunction = new DotNetFunction(this, "AnalyticsFunction", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.EventBridge.AnalyticsFunction_HandleOrderPlaced_Generated::HandleOrderPlaced",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Records analytics when an order is placed"
        });

        // Each rule matches order.placed events and routes to one consumer.
        // All three rules fire simultaneously — true fan-out with no coupling.
        var orderPlacedPattern = new EventPattern
        {
            Source = ["order-service"],
            DetailType = ["order.placed"]
        };

        new Rule(this, "FulfilmentRule", new RuleProps
        {
            EventBus = orderEventBus,
            EventPattern = orderPlacedPattern,
            Targets = [new LambdaFunction(fulfilmentFunction)]
        });

        new Rule(this, "NotificationsRule", new RuleProps
        {
            EventBus = orderEventBus,
            EventPattern = orderPlacedPattern,
            Targets = [new LambdaFunction(notificationsFunction)]
        });

        new Rule(this, "AnalyticsRule", new RuleProps
        {
            EventBus = orderEventBus,
            EventPattern = orderPlacedPattern,
            Targets = [new LambdaFunction(analyticsFunction)]
        });

        // ---------------------------------------------------------------
        // BONUS: SQS IN FRONT OF EACH EVENTBRIDGE CONSUMER
        // Combines fan-out (EventBridge) with guaranteed processing + retry (SQS).
        // EventBridge routes the event to each SQS queue; each queue has
        // its own Lambda consumer with independent retry and scaling.
        // ---------------------------------------------------------------

        var fulfilmentQueue = new Queue(this, "FulfilmentQueue", new QueueProps
        {
            VisibilityTimeout = Duration.Seconds(180),
            DeadLetterQueue = new DeadLetterQueue
            {
                MaxReceiveCount = 3,
                Queue = new Queue(this, "FulfilmentDlq")
            }
        });

        var notificationsQueue = new Queue(this, "NotificationsQueue", new QueueProps
        {
            VisibilityTimeout = Duration.Seconds(180),
            DeadLetterQueue = new DeadLetterQueue
            {
                MaxReceiveCount = 3,
                Queue = new Queue(this, "NotificationsDlq")
            }
        });

        var analyticsQueue = new Queue(this, "AnalyticsQueue", new QueueProps
        {
            VisibilityTimeout = Duration.Seconds(180),
            DeadLetterQueue = new DeadLetterQueue
            {
                MaxReceiveCount = 3,
                Queue = new Queue(this, "AnalyticsDlq")
            }
        });

        // EventBridge routes to SQS queues (not directly to Lambda)
        new Rule(this, "FulfilmentQueueRule", new RuleProps
        {
            EventBus = orderEventBus,
            EventPattern = orderPlacedPattern,
            Targets = [new SqsQueue(fulfilmentQueue)]
        });

        new Rule(this, "NotificationsQueueRule", new RuleProps
        {
            EventBus = orderEventBus,
            EventPattern = orderPlacedPattern,
            Targets = [new SqsQueue(notificationsQueue)]
        });

        new Rule(this, "AnalyticsQueueRule", new RuleProps
        {
            EventBus = orderEventBus,
            EventPattern = orderPlacedPattern,
            Targets = [new SqsQueue(analyticsQueue)]
        });

        // Each queue feeds its own Lambda consumer with independent retry
        var fulfilmentQueueConsumer = new DotNetFunction(this, "FulfilmentQueueConsumer", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.EventBridge.FulfilmentFunction_HandleOrderPlaced_Generated::HandleOrderPlaced",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Fulfilment consumer — backed by SQS for guaranteed processing"
        });

        var notificationsQueueConsumer = new DotNetFunction(this, "NotificationsQueueConsumer", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.EventBridge.NotificationsFunction_HandleOrderPlaced_Generated::HandleOrderPlaced",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Notifications consumer — backed by SQS for guaranteed processing"
        });

        var analyticsQueueConsumer = new DotNetFunction(this, "AnalyticsQueueConsumer", new DotNetFunctionProps
        {
            ProjectDir = lambdaProjectDir,
            Handler = "SqsEventBridgeDemo::SqsEventBridgeDemo.EventBridge.AnalyticsFunction_HandleOrderPlaced_Generated::HandleOrderPlaced",
            MemorySize = 256,
            Timeout = Duration.Seconds(30),
            Description = "Analytics consumer — backed by SQS for guaranteed processing"
        });

        fulfilmentQueueConsumer.AddEventSource(new SqsEventSource(fulfilmentQueue, new SqsEventSourceProps
        {
            BatchSize = 10,
            ReportBatchItemFailures = true
        }));

        notificationsQueueConsumer.AddEventSource(new SqsEventSource(notificationsQueue, new SqsEventSourceProps
        {
            BatchSize = 10,
            ReportBatchItemFailures = true
        }));

        analyticsQueueConsumer.AddEventSource(new SqsEventSource(analyticsQueue, new SqsEventSourceProps
        {
            BatchSize = 10,
            ReportBatchItemFailures = true
        }));
    }
}
