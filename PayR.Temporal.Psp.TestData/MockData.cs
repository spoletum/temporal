namespace PayR.Temporal.Psp.TestData;

/// <summary>Outcome of a validation step against a mock service.</summary>
public enum ValidationOutcome
{
    Success,
    Failure,
    Timeout,
}

/// <summary>
/// A canned account/document with a known validation outcome. Used by the
/// mock validation services to drive deterministic test cases.
/// </summary>
public sealed record MockEntry(string Value, ValidationOutcome Outcome, string Reason);

/// <summary>
/// Mock data for the PSP validation services. Each entry maps an input
/// (account number or document number) to a known outcome. Inputs not in
/// the list return a <see cref="ValidationOutcome.Failure"/>.
/// </summary>
public static class MockData
{
    // --- Account validation -------------------------------------------------

    /// <summary>
    /// Known accounts. The validator returns Success for these unless the
    /// outcome says otherwise.
    /// </summary>
    public static readonly IReadOnlyList<MockEntry> Accounts =
    [
        new("123456789", ValidationOutcome.Success, "Account exists and is active."),
        new("987654321", ValidationOutcome.Success, "Account exists and is active."),
        new("111111111", ValidationOutcome.Failure, "Account is closed."),
        new("222222222", ValidationOutcome.Timeout, "Account service is slow (simulated)."),
    ];

    // --- Document validation -----------------------------------------------

    /// <summary>
    /// Known beneficiary documents. The validator returns Success for these
    /// unless the outcome says otherwise.
    /// </summary>
    public static readonly IReadOnlyList<MockEntry> Documents =
    [
        new("ABC123456", ValidationOutcome.Success, "Document is valid."),
        new("XYZ987654", ValidationOutcome.Success, "Document is valid."),
        new("FAIL00001", ValidationOutcome.Failure, "Document is blacklisted."),
        new("SLOW00001", ValidationOutcome.Timeout, "Document service is slow (simulated)."),
    ];

    /// <summary>Looks up an account entry by number. Returns null if not found.</summary>
    public static MockEntry? FindAccount(string accountNumber) =>
        Accounts.FirstOrDefault(a => string.Equals(a.Value, accountNumber, StringComparison.OrdinalIgnoreCase));

    /// <summary>Looks up a document entry by number. Returns null if not found.</summary>
    public static MockEntry? FindDocument(string document) =>
        Documents.FirstOrDefault(d => string.Equals(d.Value, document, StringComparison.OrdinalIgnoreCase));
}
