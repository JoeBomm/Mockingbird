using System.Text.Json.Nodes;

namespace MockingBird.Middleware.Providers;

/// <summary>
/// Everything an <see cref="IMockProvider"/> needs to generate a response body for one request:
/// the target schema (self-contained, <c>$defs</c>-embedded JSON Schema), the operation's own
/// metadata, and the request's own route/query parameters so generated values stay consistent with
/// what was asked for (e.g. an <c>id</c> in the body matching the <c>{id}</c> in the route).
/// </summary>
public sealed class MockRequestContext
{
    public required string HttpMethod { get; init; }
    public required string PathTemplate { get; init; }
    public string? OperationSummary { get; init; }
    public string? OperationDescription { get; init; }
    public required JsonNode Schema { get; init; }
    public required IReadOnlyDictionary<string, string?> RouteValues { get; init; }
    public required IReadOnlyDictionary<string, string?> QueryValues { get; init; }

    /// <summary>Set on the retry attempt: the previously generated (invalid) response, so the
    /// provider can see and correct its own mistake instead of starting from scratch.</summary>
    public string? PreviousAttempt { get; init; }

    /// <summary>Set on the retry attempt: the schema-validation errors from
    /// <see cref="PreviousAttempt"/>.</summary>
    public IReadOnlyList<string>? ValidationErrors { get; init; }
}
