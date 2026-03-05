using System.Diagnostics;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;

// ---------------------------------------------------------------------------
// SQS Load Generator
// Floods one or both queues simultaneously to compare worker service vs Lambda
// behaviour under load.
//
// Usage:
//   dotnet run -- --worker-queue-url <url> --lambda-queue-url <url> [--count <n>]
//
// You can target either queue individually:
//   dotnet run -- --worker-queue-url <url> --count 500
//   dotnet run -- --lambda-queue-url <url> --count 500
//
// Or set env vars and omit the flags:
//   WORKER_QUEUE_URL=... LAMBDA_QUEUE_URL=... dotnet run -- --count 1000
// ---------------------------------------------------------------------------

var workerQueueUrl = GetArg(args, "--worker-queue-url")
    ?? Environment.GetEnvironmentVariable("WORKER_QUEUE_URL");

var lambdaQueueUrl = GetArg(args, "--lambda-queue-url")
    ?? Environment.GetEnvironmentVariable("LAMBDA_QUEUE_URL");

var totalMessages = int.Parse(GetArg(args, "--count") ?? "100");
var concurrency   = int.Parse(GetArg(args, "--concurrency") ?? "20");

var targets = new List<QueueTarget>();
if (workerQueueUrl is not null) targets.Add(new QueueTarget("Worker Service", workerQueueUrl));
if (lambdaQueueUrl is not null) targets.Add(new QueueTarget("Lambda         ", lambdaQueueUrl));

if (targets.Count == 0)
{
    Console.Error.WriteLine("Error: provide at least one queue URL.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  --worker-queue-url <url>   or   WORKER_QUEUE_URL=<url>");
    Console.Error.WriteLine("  --lambda-queue-url <url>   or   LAMBDA_QUEUE_URL=<url>");
    Environment.Exit(1);
}

// ---- Print config -------------------------------------------------------

Console.WriteLine();
Console.WriteLine($"  Messages per queue : {totalMessages:N0}");
Console.WriteLine($"  Concurrency        : {concurrency} concurrent batch sends per queue");
Console.WriteLine($"  Queues             : {targets.Count}");
Console.WriteLine();

foreach (var t in targets)
    Console.WriteLine($"  [{t.Label}]  {t.QueueUrl}");

Console.WriteLine();

// ---- Reserve progress lines and capture start row -----------------------

// Print a placeholder line for each target — we'll overwrite these in-place
foreach (var t in targets)
    Console.WriteLine($"  [{t.Label}]  Starting...");

var progressRow = Console.CursorTop - targets.Count;
var displayLock = new object();

void RedrawProgress()
{
    lock (displayLock)
    {
        var savedTop  = Console.CursorTop;
        var savedLeft = Console.CursorLeft;

        for (var i = 0; i < targets.Count; i++)
        {
            var t   = targets[i];
            var pct = totalMessages > 0 ? (double)t.Sent / totalMessages * 100 : 0;
            var bar = BuildBar(t.Sent, totalMessages, width: 30);

            Console.SetCursorPosition(0, progressRow + i);
            Console.Write($"  [{t.Label}]  {bar}  {t.Sent,6:N0}/{totalMessages:N0}  {pct,5:F1}%  ({t.Failed} failed)  ");
        }

        Console.SetCursorPosition(savedLeft, savedTop);
    }
}

// ---- Start a background refresh loop ------------------------------------

using var cts = new CancellationTokenSource();

var refreshTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        RedrawProgress();
        await Task.Delay(150, cts.Token).ConfigureAwait(false);
    }
}, cts.Token);

// ---- Flood all queues simultaneously ------------------------------------

var sqsClient = new AmazonSQSClient();
var stopwatch = Stopwatch.StartNew();

await Task.WhenAll(targets.Select(t => FloodQueueAsync(sqsClient, t, totalMessages, concurrency)));

stopwatch.Stop();

// Stop the refresh loop and do one final redraw
cts.Cancel();
try { await refreshTask; } catch (OperationCanceledException) { }
RedrawProgress();

// ---- Print summary below the progress lines -----------------------------

Console.SetCursorPosition(0, progressRow + targets.Count);
Console.WriteLine();
Console.WriteLine($"  Completed in {stopwatch.Elapsed.TotalSeconds:F1}s");
Console.WriteLine();

foreach (var t in targets)
{
    var rate = t.Sent / stopwatch.Elapsed.TotalSeconds;
    Console.WriteLine($"  [{t.Label}]  {t.Sent:N0} sent  |  {t.Failed:N0} failed  |  {rate:N0} msg/s");
}

Console.WriteLine();
Console.WriteLine("  Open the AWS console and compare:");
Console.WriteLine("  - Lambda queue  → Monitor tab → Concurrent executions climbing automatically");
Console.WriteLine("  - Worker queue  → Approximate number of messages visible growing");
Console.WriteLine();

// =========================================================================

static async Task FloodQueueAsync(
    IAmazonSQS sqsClient,
    QueueTarget target,
    int totalMessages,
    int concurrency)
{
    var semaphore = new SemaphoreSlim(concurrency);

    var batches = Enumerable
        .Range(0, totalMessages)
        .Chunk(10)
        .Select(async batchIndices =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var entries = batchIndices.Select(i => new SendMessageBatchRequestEntry
                {
                    Id       = i.ToString(),
                    MessageBody = JsonSerializer.Serialize(CreateOrderMessage(i))
                }).ToList();

                var response = await sqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
                {
                    QueueUrl = target.QueueUrl,
                    Entries  = entries
                }).ConfigureAwait(false);

                Interlocked.Add(ref target.Sent,   response.Successful?.Count ?? 0);
                Interlocked.Add(ref target.Failed, response.Failed?.Count ?? 0);
            }
            finally
            {
                semaphore.Release();
            }
        });

    await Task.WhenAll(batches).ConfigureAwait(false);
}

static string BuildBar(int sent, int total, int width)
{
    if (total == 0) return new string(' ', width + 2);
    var filled = (int)Math.Round((double)sent / total * width);
    return "[" + new string('#', filled) + new string('-', width - filled) + "]";
}

static OrderMessage CreateOrderMessage(int index)
{
    var customers = new[] { "cust-001", "cust-042", "cust-099", "cust-314", "cust-271" };
    var products  = new[] { "prod-widget", "prod-gadget", "prod-thingamajig", "prod-doohickey" };

    return new OrderMessage(
        OrderId:    $"order-{Guid.NewGuid():N}",
        CustomerId: customers[index % customers.Length],
        ProductId:  products[index % products.Length],
        Quantity:   (index % 5) + 1,
        TotalAmount: Math.Round(9.99m + (index % 50) * 2.5m, 2));
}

static string? GetArg(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

// =========================================================================

class QueueTarget(string label, string queueUrl)
{
    public string Label    { get; } = label;
    public string QueueUrl { get; } = queueUrl;
    public int    Sent;
    public int    Failed;
}

record OrderMessage(
    string  OrderId,
    string  CustomerId,
    string  ProductId,
    int     Quantity,
    decimal TotalAmount);
