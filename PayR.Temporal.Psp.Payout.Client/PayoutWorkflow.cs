namespace PayR.Temporal.Psp.Payout.Client;

/// <summary>
/// Identity constants for the Payout workflow. Both the worker (which
/// implements the workflow) and callers (the Web UI, future API) reference
/// these so the type name and task queue can never drift apart.
/// </summary>
public static class PayoutWorkflow
{
    /// <summary>Temporal workflow type name. Must match the worker's [Workflow] attribute.</summary>
    public const string Name = "PspPayoutWorkflow";

    /// <summary>Task queue the payout worker polls.</summary>
    public const string TaskQueue = "psp-payout-task-queue";
}
