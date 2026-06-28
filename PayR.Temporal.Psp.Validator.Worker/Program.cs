using System.Net.Http.Json;
using PayR.Temporal.Psp.Validator.Client;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Worker;
using Temporalio.Workflows;

// The validator worker owns the ValidatorWorkflow and its activities.
// The activities call external mock HTTP services (account-validation-mock,
// document-validation-mock) which bake in the timeout behaviour.

var temporalAddress = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";
var accountServiceUrl = Environment.GetEnvironmentVariable("ACCOUNT_VALIDATION_URL") ?? "http://localhost:8081";
var documentServiceUrl = Environment.GetEnvironmentVariable("DOCUMENT_VALIDATION_URL") ?? "http://localhost:8082";

var activities = new ValidatorActivities(accountServiceUrl, documentServiceUrl);
var workerOptions = new TemporalWorkerOptions(taskQueue: ValidatorWorkflow.TaskQueue)
    .AddAllActivities(activities)
    .AddWorkflow<ValidatorWorkflowImpl>();

using var worker = new TemporalWorker(
    await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(temporalAddress)),
    workerOptions);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"PayR.Temporal.Psp.Validator.Worker starting on task queue '{ValidatorWorkflow.TaskQueue}' (connecting to {temporalAddress})...");
await worker.ExecuteAsync(cts.Token);
Console.WriteLine("Validator worker stopped.");

/// <summary>
/// Activities that call the external mock validation services via HTTP.
/// Each activity has a 30s StartToCloseTimeout; the mock services sleep 60s
/// for the "timeout" test cases, so these activities will time out and the
/// workflow's retry policy (3 attempts, 2s interval) kicks in.
/// </summary>
public sealed class ValidatorActivities
{
    private readonly HttpClient _accountClient;
    private readonly HttpClient _documentClient;

    public ValidatorActivities(string accountServiceUrl, string documentServiceUrl)
    {
        _accountClient = new HttpClient { BaseAddress = new Uri(accountServiceUrl) };
        _documentClient = new HttpClient { BaseAddress = new Uri(documentServiceUrl) };
    }

    [Activity]
    public async Task<ValidationStepResult> ValidateAccountAsync(string accountNumber)
    {
        var resp = await _accountClient.PostAsJsonAsync("/validate/account", new { accountNumber });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AccountValidationResponse>()
                   ?? throw new InvalidOperationException("Empty response from account service.");
        return new ValidationStepResult("Account", body.Valid, body.Reason);
    }

    [Activity]
    public async Task<ValidationStepResult> ValidateDocumentAsync(string document)
    {
        var resp = await _documentClient.PostAsJsonAsync("/validate/document", new { document });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DocumentValidationResponse>()
                   ?? throw new InvalidOperationException("Empty response from document service.");
        return new ValidationStepResult("Document", body.Valid, body.Reason);
    }
}

public sealed record AccountValidationResponse(bool Valid, string Reason);
public sealed record DocumentValidationResponse(bool Valid, string Reason);

/// <summary>
/// Validator workflow. Runs account + document validation activities in
/// parallel, each with a retry policy (3 attempts, 2s fixed interval) and a
/// 30s StartToCloseTimeout. Returns a combined ValidatorResult.
/// </summary>
[Workflow(ValidatorWorkflow.Name)]
public sealed class ValidatorWorkflowImpl
{
    [WorkflowRun]
    public async Task<ValidatorResult> RunAsync(ValidatorInput input)
    {
        var retryOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(30),
            RetryPolicy = new RetryPolicy
            {
                InitialInterval = TimeSpan.FromSeconds(2),
                MaximumInterval = TimeSpan.FromSeconds(2),
                MaximumAttempts = 3,
            },
        };

        // Run both validations in parallel.
        var accountTask = Workflow.ExecuteActivityAsync(
            (ValidatorActivities a) => a.ValidateAccountAsync(input.FromAccount),
            retryOptions);

        var documentTask = Workflow.ExecuteActivityAsync(
            (ValidatorActivities a) => a.ValidateDocumentAsync(input.BeneficiaryDocument),
            retryOptions);

        var steps = new List<ValidationStepResult>();
        var failures = new List<string>();

        // Await each independently so a failure in one doesn't mask the other.
        try
        {
            steps.Add(await accountTask);
        }
        catch (Exception ex)
        {
            failures.Add($"Account validation failed: {ex.Message}");
            steps.Add(new ValidationStepResult("Account", false, ex.Message));
        }

        try
        {
            steps.Add(await documentTask);
        }
        catch (Exception ex)
        {
            failures.Add($"Document validation failed: {ex.Message}");
            steps.Add(new ValidationStepResult("Document", false, ex.Message));
        }

        if (failures.Count > 0)
        {
            return ValidatorResult.Failure(steps, string.Join("; ", failures));
        }
        return ValidatorResult.Success(steps);
    }
}
