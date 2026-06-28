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

    public string MarkdownExplanation => @"
This is the simplest possible Temporal workflow: a single activity that
greets a name.

**What it demonstrates:**

- A workflow with one activity
- The round-trip from UI → Temporal → worker → activity → result
- The shared `SayHelloInput` contract between the worker and the UI
";

    public string MermaidDiagram => @"
sequenceDiagram
    participant UI as Web UI
    participant T as Temporal
    participant W as Worker
    participant A as SayHello Activity

    UI->>T: Start PayRGreetingWorkflow(name)
    T->>W: Schedule workflow task
    W->>A: ExecuteActivity SayHello(name)
    A-->>W: ""Hello, {name}!""
    W-->>T: Complete workflow
    T-->>UI: Result
";

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
