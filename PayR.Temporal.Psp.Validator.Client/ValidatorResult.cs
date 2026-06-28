using System.Text.Json.Serialization;

namespace PayR.Temporal.Psp.Validator.Client;

/// <summary>Result of a validation step.</summary>
public sealed record ValidationStepResult(
    [property: JsonPropertyName("step")] string Step,
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>Overall result of the Validator workflow.</summary>
public sealed record ValidatorResult(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("steps")] IReadOnlyList<ValidationStepResult> Steps,
    [property: JsonPropertyName("summary")] string Summary)
{
    public static ValidatorResult Success(IReadOnlyList<ValidationStepResult> steps) =>
        new(true, steps, "All validation steps passed.");

    public static ValidatorResult Failure(IReadOnlyList<ValidationStepResult> steps, string reason) =>
        new(false, steps, reason);
}
