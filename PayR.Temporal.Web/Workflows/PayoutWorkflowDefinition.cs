using PayR.Temporal.Psp.Payout.Client;

namespace PayR.Temporal.Web.Workflows;

/// <summary>
/// UI adapter for the Payout workflow. Builds a <see cref="PayoutInput"/>
/// from form values and renders the result.
/// </summary>
public sealed class PayoutWorkflowDefinition : IWorkflowDefinition
{
    public string Id => "payout";
    public string DisplayName => "Payout";
    public string Description => "Executes a payout after running the validator workflow as a child.";
    public string WorkflowType => PayoutWorkflow.Name;
    public string TaskQueue => PayoutWorkflow.TaskQueue;

    public IReadOnlyList<WorkflowField> Fields { get; } =
    [
        new WorkflowField(
            Name: "fromAccount",
            Label: "From Account",
            Type: WorkflowFieldType.Text,
            Placeholder: "123456789",
            DefaultValue: "123456789"),
        new WorkflowField(
            Name: "currency",
            Label: "Currency",
            Type: WorkflowFieldType.Text,
            Placeholder: "USD",
            DefaultValue: "USD"),
        new WorkflowField(
            Name: "amount",
            Label: "Amount",
            Type: WorkflowFieldType.Number,
            Placeholder: "100.00",
            DefaultValue: "100.00"),
        new WorkflowField(
            Name: "beneficiaryName",
            Label: "Beneficiary Name",
            Type: WorkflowFieldType.Text,
            Placeholder: "Jane Doe",
            DefaultValue: "Jane Doe"),
        new WorkflowField(
            Name: "beneficiaryDocument",
            Label: "Beneficiary Document",
            Type: WorkflowFieldType.Text,
            Placeholder: "ABC123456",
            DefaultValue: "ABC123456"),
        new WorkflowField(
            Name: "beneficiaryAccount",
            Label: "Beneficiary Account",
            Type: WorkflowFieldType.Text,
            Placeholder: "987654321",
            DefaultValue: "987654321"),
    ];

    public object BuildInput(IReadOnlyDictionary<string, string?> form)
    {
        var fromAccount = Get(form, "fromAccount", "123456789");
        var currency = Get(form, "currency", "USD");
        var amount = decimal.TryParse(Get(form, "amount", "100.00"), out var a) ? a : 100m;
        var beneficiaryName = Get(form, "beneficiaryName", "Jane Doe");
        var beneficiaryDocument = Get(form, "beneficiaryDocument", "ABC123456");
        var beneficiaryAccount = Get(form, "beneficiaryAccount", "987654321");

        return new PayoutInput(
            fromAccount, currency, amount,
            beneficiaryName, beneficiaryDocument, beneficiaryAccount);
    }

    public string FormatResult(object? result) => result?.ToString() ?? "(no result)";

    private static string Get(IReadOnlyDictionary<string, string?> form, string key, string fallback) =>
        form.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v! : fallback;
}
