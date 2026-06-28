using PayR.Temporal.Psp.TestData;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// POST /validate/account
// Body: { "accountNumber": "123456789" }
// Response: { "valid": true, "reason": "..." }
//
// If the account's outcome is Timeout, sleeps 60s (longer than the
// activity's StartToCloseTimeout) to simulate a hung external service.
app.MapPost("/validate/account", async (AccountRequest req) =>
{
    var entry = MockData.FindAccount(req.AccountNumber);

    // Unknown account → fail.
    if (entry is null)
    {
        return Results.Ok(new AccountResponse(false, $"Account '{req.AccountNumber}' not found."));
    }

    if (entry.Outcome == ValidationOutcome.Timeout)
    {
        // Sleep longer than the activity's StartToCloseTimeout (30s).
        await Task.Delay(TimeSpan.FromSeconds(60));
        return Results.Ok(new AccountResponse(true, entry.Reason));
    }

    return Results.Ok(new AccountResponse(entry.Outcome == ValidationOutcome.Success, entry.Reason));
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public sealed record AccountRequest(string AccountNumber);
public sealed record AccountResponse(bool Valid, string Reason);
