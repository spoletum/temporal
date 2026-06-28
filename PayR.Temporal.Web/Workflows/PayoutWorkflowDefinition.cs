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

    public string MarkdownExplanation => @"
A PSP payout flow that runs a **validator workflow as a child** before
releasing funds. The validator calls two external mock services (account
and document validation) which can be slow.

**What it demonstrates:**

- A parent workflow starting a child workflow by name (no reference to the
  child's implementation — only its `.Client` contract)
- Racing a child workflow against a 30-second timer
- Proceeding with a warning when the validator is slow, while the child
  keeps running in the background (`ParentClosePolicy.Abandon`)
- Activities calling external HTTP services with a retry policy
  (3 attempts, 2s fixed interval)

**Try these test cases:**

| From Account | Beneficiary Document | Expected outcome |
|---|---|---|
| `123456789` | `ABC123456` | Completed (both validations pass) |
| `111111111` | `ABC123456` | Failed (account is closed) |
| `222222222` | `ABC123456` | CompletedWithWarning (account service times out) |
| `123456789` | `FAIL00001` | Failed (document is blacklisted) |
| `999999999` | `ABC123456` | Failed (account not found) |
";

    public string MermaidDiagram => @"
sequenceDiagram
    participant UI as Web UI
    participant T as Temporal
    participant PW as Payout Workflow
    participant VW as Validator Workflow
    participant AM as Account Mock
    participant DM as Document Mock

    UI->>T: Start PspPayoutWorkflow(input)
    T->>PW: Schedule workflow task
    PW->>T: StartChildWorkflow(ValidatorWorkflow)
    T->>VW: Schedule child workflow task
    par Account validation
        VW->>AM: POST /validate/account
        AM-->>VW: valid/invalid/timeout
    and Document validation
        VW->>DM: POST /validate/document
        DM-->>VW: valid/invalid/timeout
    end
    VW-->>T: ValidatorResult
    alt Validator completes within 30s
        T-->>PW: ValidatorResult
        PW-->>T: Payout Completed
    else 30s timer wins
        PW-->>T: Payout CompletedWithWarning
        Note over VW: still running in background
    end
    T-->>UI: PayoutResult
";

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
