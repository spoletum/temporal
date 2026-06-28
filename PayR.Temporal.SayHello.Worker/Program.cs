using PayR.Temporal.SayHello.Client;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;

// The worker owns the workflow and activity *implementations*. The input
// contract and workflow identity (name + task queue) live in
// PayR.Temporal.SayHello.Client, shared with callers.

var temporalAddress = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";
var temporalNamespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default";

var activities = new PayRActivities();
var workerOptions = new TemporalWorkerOptions(taskQueue: SayHelloWorkflow.TaskQueue)
    .AddAllActivities(activities)
    .AddWorkflow<PayRGreetingWorkflow>();

using var worker = new TemporalWorker(
    await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(temporalAddress)
    {
        Namespace = temporalNamespace,
    }),
    workerOptions);

// Run until cancelled (Ctrl+C).
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"PayR.Temporal.SayHello.Worker starting on task queue '{SayHelloWorkflow.TaskQueue}' (namespace '{temporalNamespace}', connecting to {temporalAddress})...");
await worker.ExecuteAsync(cts.Token);
Console.WriteLine("Worker stopped.");

/// <summary>Activities invoked by <see cref="PayRGreetingWorkflow"/>.</summary>
public sealed class PayRActivities
{
    [Activity]
    public string SayHello(string name) => $"Hello, {name}! — from PayR.Temporal.SayHello.Worker";
}

/// <summary>
/// Sample greeting workflow. Accepts a <see cref="SayHelloInput"/>, calls the
/// <see cref="PayRActivities.SayHello"/> activity, and returns the greeting.
/// </summary>
[Workflow(SayHelloWorkflow.Name)]
public sealed class PayRGreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(SayHelloInput input)
    {
        var message = await Workflow.ExecuteActivityAsync(
            (PayRActivities a) => a.SayHello(input.Name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) });

        return message;
    }
}
