namespace MockingBird.Middleware.Providers;

/// <summary>
/// Generates a JSON response body for a mocked OpenAPI operation. Implementations are free to call
/// out to an LLM, a rule-based generator, or anything else — the middleware only depends on this
/// abstraction, never on a specific backend.
/// </summary>
public interface IMockProvider
{
    /// <summary>
    /// Returns a JSON document (as text) intended to satisfy <see cref="MockRequestContext.Schema"/>.
    /// The caller validates the result and may call this again with
    /// <see cref="MockRequestContext.PreviousAttempt"/> populated if it doesn't.
    /// </summary>
    Task<string> GenerateAsync(MockRequestContext context, CancellationToken cancellationToken = default);
}
