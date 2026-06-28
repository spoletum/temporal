namespace PayR.Temporal.Psp.Validator.Client;

/// <summary>
/// Identity constants for the Validator workflow. Both the worker (which
/// implements the workflow) and callers (the payout workflow) reference
/// these so the type name and task queue can never drift apart.
/// </summary>
public static class ValidatorWorkflow
{
    /// <summary>Temporal workflow type name. Must match the worker's [Workflow] attribute.</summary>
    public const string Name = "PspValidatorWorkflow";

    /// <summary>Task queue the validator worker polls.</summary>
    public const string TaskQueue = "psp-validator-task-queue";
}
