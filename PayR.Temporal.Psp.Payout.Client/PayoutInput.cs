using System.Text.Json.Serialization;

namespace PayR.Temporal.Psp.Payout.Client;

/// <summary>Input for the Payout workflow.</summary>
public sealed record PayoutInput(
    [property: JsonPropertyName("fromAccount")] string FromAccount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("beneficiaryName")] string BeneficiaryName,
    [property: JsonPropertyName("beneficiaryDocument")] string BeneficiaryDocument,
    [property: JsonPropertyName("beneficiaryAccount")] string BeneficiaryAccount);
