using System.Text.Json.Serialization;

namespace PayR.Temporal.SayHello.Client;

/// <summary>
/// Input for the SayHello workflow. This is the contract shared between
/// the worker (which implements the workflow) and any caller (worker host,
/// Web UI, tests, CLI) that starts it.
/// </summary>
/// <remarks>
/// <see cref="JsonPropertyNameAttribute"/> pins the wire field name to
/// <c>name</c> so the contract is stable regardless of the caller's JSON
/// serialization policy (camelCase vs PascalCase).
/// </remarks>
public sealed record SayHelloInput(
    [property: JsonPropertyName("name")] string Name);
