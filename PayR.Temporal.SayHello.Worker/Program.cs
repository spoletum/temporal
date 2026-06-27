using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;

// A trivial workflow + activity so the worker has something to register.
// Replace these with real PayR workflows as the project grows.

var temporalAddress = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";

var activities = new PayRActivities();
var workerOptions = new TemporalWorkerOptions(taskQueue: "payr-task-queue")
    .AddAllActivities(activities)
    .AddWorkflow<PayRGreetingWorkflow>();

using var worker = new TemporalWorker(
    await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(temporalAddress)),
    workerOptions);

// Run until cancelled (Ctrl+C).
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"PayR.Temporal.SayHello.Worker starting on task queue 'payr-task-queue' (connecting to {temporalAddress})...");
await worker.ExecuteAsync(cts.Token);
Console.WriteLine("Worker stopped.");

public sealed class PayRActivities
{
    [Activity]
    public string SayHello(string name) => $"Hello, {name}! — from PayR.Temporal.SayHello.Worker";
}

[Workflow]
public sealed class PayRGreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        var message = await Workflow.ExecuteActivityAsync(
            (PayRActivities a) => a.SayHello(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) });

        return message;
    }
}
