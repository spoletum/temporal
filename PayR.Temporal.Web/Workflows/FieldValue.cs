namespace PayR.Temporal.Web.Workflows;

/// <summary>
/// Backing model for a single dynamic form field. Blazor's <c>InputBase</c>
/// components require a property accessor (not an indexer) for their
/// <c>FieldIdentifier</c>, so each field gets its own model instance.
/// </summary>
public sealed class FieldValue
{
    public string? Value { get; set; }
}
