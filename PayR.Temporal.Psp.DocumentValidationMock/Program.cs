using PayR.Temporal.Psp.TestData;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// POST /validate/document
// Body: { "document": "ABC123456" }
// Response: { "valid": true, "reason": "..." }
//
// If the document's outcome is Timeout, sleeps 60s (longer than the
// activity's StartToCloseTimeout) to simulate a hung external service.
app.MapPost("/validate/document", async (DocumentRequest req) =>
{
    var entry = MockData.FindDocument(req.Document);

    // Unknown document → fail.
    if (entry is null)
    {
        return Results.Ok(new DocumentResponse(false, $"Document '{req.Document}' not found."));
    }

    if (entry.Outcome == ValidationOutcome.Timeout)
    {
        // Sleep longer than the activity's StartToCloseTimeout (30s).
        await Task.Delay(TimeSpan.FromSeconds(60));
        return Results.Ok(new DocumentResponse(true, entry.Reason));
    }

    return Results.Ok(new DocumentResponse(entry.Outcome == ValidationOutcome.Success, entry.Reason));
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public sealed record DocumentRequest(string Document);
public sealed record DocumentResponse(bool Valid, string Reason);
