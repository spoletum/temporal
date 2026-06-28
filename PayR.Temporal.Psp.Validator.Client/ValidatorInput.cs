using System.Text.Json.Serialization;

namespace PayR.Temporal.Psp.Validator.Client;

/// <summary>Input for the Validator workflow.</summary>
public sealed record ValidatorInput(
    [property: JsonPropertyName("fromAccount")] string FromAccount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("beneficiaryName")] string BeneficiaryName,
    [property: JsonPropertyName("beneficiaryDocument")] string BeneficiaryDocument,
    [property: JsonPropertyName("beneficiaryAccount")] string BeneficiaryAccount);
