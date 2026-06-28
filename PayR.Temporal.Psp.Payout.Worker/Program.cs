using PayR.Temporal.Psp.Payout.Client;
using PayR.Temporal.Psp.Validator.Client;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;

// The payout worker owns the PayoutWorkflow. It starts the ValidatorWorkflow
// as a child workflow, races it against a 30s timer, and proceeds with a
// warning if the validator doesn't complete in time. The child workflow
// continues running in the background (ParentClosePolicy.Abandon).

var temporalAddress = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";
var temporalNamespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default";

var workerOptions = new TemporalWorkerOptions(taskQueue: PayoutWorkflow.TaskQueue)
    .AddWorkflow<PayoutWorkflowImpl>();

using var worker = new TemporalWorker(
    await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(temporalAddress)
    {
        Namespace = temporalNamespace,
    }),
    workerOptions);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"PayR.Temporal.Psp.Payout.Worker starting on task queue '{PayoutWorkflow.TaskQueue}' (namespace '{temporalNamespace}', connecting to {temporalAddress})...");
await worker.ExecuteAsync(cts.Token);
Console.WriteLine("Payout worker stopped.");

/// <summary>
/// Payout workflow. Starts the ValidatorWorkflow as a child, races it
/// against a 30s timer. If the validator completes in time, the payout
/// proceeds (or fails if validation failed). If the timer wins, the payout
/// proceeds with a warning and the validator keeps running in the background.
/// </summary>
[Workflow(PayoutWorkflow.Name)]
public sealed class PayoutWorkflowImpl
{
    [WorkflowRun]
    public async Task<PayoutResult> RunAsync(PayoutInput input)
    {
        var payoutId = $"payout-{Workflow.Info.WorkflowId}";
        var validationInput = new ValidatorInput(
            input.FromAccount,
            input.Currency,
            input.Amount,
            input.BeneficiaryName,
            input.BeneficiaryDocument,
            input.BeneficiaryAccount);

        // Start the validator as a child workflow. Abandon on parent close
        // so it keeps running even if the payout workflow completes.
        var childHandle = await Workflow.StartChildWorkflowAsync(
            ValidatorWorkflow.Name,
            [validationInput],
            new ChildWorkflowOptions
            {
                TaskQueue = ValidatorWorkflow.TaskQueue,
                ParentClosePolicy = ParentClosePolicy.Abandon,
            });

        // Race the validator against a 30s timer.
        var validationTask = childHandle.GetResultAsync<ValidatorResult>();
        var timerTask = Workflow.DelayAsync(TimeSpan.FromSeconds(30));
        var winner = await Task.WhenAny(validationTask, timerTask);

        if (winner == validationTask)
        {
            // Validator completed in time.
            var validation = await validationTask;
            if (!validation.Valid)
            {
                return new PayoutResult(
                    PayoutStatus.Failed,
                    payoutId,
                    validation,
                    $"Payout rejected: {validation.Summary}");
            }

            // Validation passed — execute the payout.
            return new PayoutResult(
                PayoutStatus.Completed,
                payoutId,
                validation,
                $"Payout of {input.Amount} {input.Currency} to {input.BeneficiaryName} completed.");
        }

        // Timer won — validator is still running. Proceed with a warning.
        // The child workflow continues in the background (Abandon policy).
        // We don't await validationTask here; the payout completes now.
        return new PayoutResult(
            PayoutStatus.CompletedWithWarning,
            payoutId,
            null,
            $"Payout of {input.Amount} {input.Currency} to {input.BeneficiaryName} proceeded without " +
            $"timely validation. Validator is still running (child workflow id={childHandle.Id}).");
    }
}
