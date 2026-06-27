namespace PayR.Temporal.SayHello.Client;

/// <summary>
/// Identity constants for the SayHello workflow. Both the worker
/// (registers the workflow) and callers (start it) reference these so
/// the type name and task queue can never drift apart.
/// </summary>
public static class SayHelloWorkflow
{
    /// <summary>Temporal workflow type name. Must match the worker's <c>[Workflow]</c> attribute.</summary>
    public const string Name = "PayRGreetingWorkflow";

    /// <summary>Task queue the worker polls for this workflow.</summary>
    public const string TaskQueue = "payr-task-queue";
}
