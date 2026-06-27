using Temporalio.Client;

namespace PayR.Temporal.Web.Workflows;

/// <summary>Result of a workflow execution started through the UI.</summary>
public sealed record WorkflowRunResult(
    string WorkflowId,
    string RunId,
    string Status,
    string? ResultText,
    string? Error);

/// <summary>Service that starts workflow executions and awaits their result.</summary>
public sealed class WorkflowRunner
{
    private readonly TemporalClientProvider _clientProvider;
    private readonly IEnumerable<IWorkflowDefinition> _definitions;
    private readonly ILogger<WorkflowRunner> _logger;

    public WorkflowRunner(
        TemporalClientProvider clientProvider,
        IEnumerable<IWorkflowDefinition> definitions,
        ILogger<WorkflowRunner> logger)
    {
        _clientProvider = clientProvider;
        _definitions = definitions;
        _logger = logger;
    }

    /// <summary>Lookup a definition by id. Returns null if not found.</summary>
    public IWorkflowDefinition? Find(string id) =>
        _definitions.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Start a workflow, await completion, and return a formatted result.</summary>
    public async Task<WorkflowRunResult> RunAsync(
        IWorkflowDefinition definition,
        IReadOnlyDictionary<string, string?> form,
        CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.GetClientAsync(cancellationToken).ConfigureAwait(false);

        var input = definition.BuildInput(form);
        var workflowId = $"{definition.Id}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}";

        _logger.LogInformation("Starting workflow {Type} id={Id} on queue {Queue}",
            definition.WorkflowType, workflowId, definition.TaskQueue);

        var handle = await client.StartWorkflowAsync(
            definition.WorkflowType,
            [input],
            new WorkflowOptions
            {
                Id = workflowId,
                TaskQueue = definition.TaskQueue,
            }).ConfigureAwait(false);

        try
        {
            var result = await handle.GetResultAsync<object?>(
                followRuns: true,
                new RpcOptions { CancellationToken = cancellationToken }).ConfigureAwait(false);
            return new WorkflowRunResult(
                WorkflowId: workflowId,
                RunId: handle.ResultRunId ?? "",
                Status: "Completed",
                ResultText: definition.FormatResult(result),
                Error: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow {Id} failed", workflowId);
            return new WorkflowRunResult(
                WorkflowId: workflowId,
                RunId: handle.ResultRunId ?? "",
                Status: "Failed",
                ResultText: null,
                Error: ex.Message);
        }
    }
}
