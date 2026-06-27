using PayR.Temporal.SayHello.Client;

namespace PayR.Temporal.Web.Workflows;

/// <summary>
/// UI adapter for the SayHello workflow. Builds a <see cref="SayHelloInput"/>
/// from form values and renders the result.
/// </summary>
public sealed class SayHelloWorkflowDefinition : IWorkflowDefinition
{
    public string Id => "say-hello";
    public string DisplayName => "Say Hello";
    public string Description => "Greets a name via the PayRGreetingWorkflow activity.";
    public string WorkflowType => SayHelloWorkflow.Name;
    public string TaskQueue => SayHelloWorkflow.TaskQueue;

    public IReadOnlyList<WorkflowField> Fields { get; } =
    [
        new WorkflowField(
            Name: "name",
            Label: "Name",
            Type: WorkflowFieldType.Text,
            Placeholder: "World",
            DefaultValue: "World"),
    ];

    public object BuildInput(IReadOnlyDictionary<string, string?> form)
    {
        var name = form.TryGetValue("name", out var n) && !string.IsNullOrWhiteSpace(n)
            ? n!
            : "World";
        return new SayHelloInput(name);
    }

    public string FormatResult(object? result) => result?.ToString() ?? "(no result)";
}
