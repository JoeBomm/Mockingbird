namespace MockingBird.Middleware.Configuration;

/// <summary>
/// Selects which <see cref="Providers.IMockProvider"/> backend <c>AddMockingBird</c> registers.
/// </summary>
public enum MockProviderKind
{
    /// <summary>Calls OpenRouter's chat completions API. Falls back to the deterministic offline
    /// provider when <see cref="MockingBirdOptions.OpenRouterApiKey"/> is not set.</summary>
    OpenRouter,

    /// <summary>Calls Amazon Bedrock's Converse API using <see cref="MockingBirdOptions.Bedrock"/>.</summary>
    Bedrock,
}
