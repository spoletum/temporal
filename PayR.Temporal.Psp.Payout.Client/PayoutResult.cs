using System.Text.Json.Serialization;
using PayR.Temporal.Psp.Validator.Client;

namespace PayR.Temporal.Psp.Payout.Client;

/// <summary>Final status of a payout.</summary>
public enum PayoutStatus
{
    Completed,
    CompletedWithWarning,
    Failed,
}

/// <summary>Result of the Payout workflow.</summary>
public sealed record PayoutResult(
    [property: JsonPropertyName("status")] PayoutStatus Status,
    [property: JsonPropertyName("payoutId")] string PayoutId,
    [property: JsonPropertyName("validation")] ValidatorResult? Validation,
    [property: JsonPropertyName("summary")] string Summary);
