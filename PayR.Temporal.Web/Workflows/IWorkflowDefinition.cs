namespace PayR.Temporal.Web.Workflows;

/// <summary>
/// UI metadata for a Temporal workflow. Implement once per workflow type
/// and register in DI; the Workflows page discovers all registered
/// definitions and renders an input form for each.
/// </summary>
public interface IWorkflowDefinition
{
    /// <summary>Stable identifier used in URLs and the registry. Lower-kebab-case.</summary>
    string Id { get; }

    /// <summary>Human-friendly name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>Short description shown under the name.</summary>
    string Description { get; }

    /// <summary>Temporal workflow type name the worker registers.</summary>
    string WorkflowType { get; }

    /// <summary>Temporal task queue the workflow should be started on.</summary>
    string TaskQueue { get; }

    /// <summary>Field descriptors used to render the input form dynamically.</summary>
    IReadOnlyList<WorkflowField> Fields { get; }

    /// <summary>
    /// Builds the input argument(s) for <c>StartWorkflowAsync</c> from form values.
    /// </summary>
    /// <param name="form">A dictionary of string field names to raw string values submitted by the form.</param>
    /// <returns>The workflow input. May be a single value, a tuple, or an array.</returns>
    object BuildInput(IReadOnlyDictionary<string, string?> form);

    /// <summary>Formats the workflow result for display.</summary>
    string FormatResult(object? result);
}

/// <summary>Describes a single input field rendered in the workflow form.</summary>
public sealed record WorkflowField(
    string Name,
    string Label,
    WorkflowFieldType Type,
    string? Placeholder = null,
    string? DefaultValue = null);

public enum WorkflowFieldType
{
    Text,
    Number,
    Email,
    Multiline,
}
