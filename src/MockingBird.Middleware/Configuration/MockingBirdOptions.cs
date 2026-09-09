using Microsoft.OpenApi;

namespace MockingBird.Middleware.Configuration;

/// <summary>
/// Configuration surface for MockingBird, set via <c>AddMockingBird(configure)</c>.
/// </summary>
public sealed class MockingBirdOptions
{
    /// <summary>Which <see cref="Providers.IMockProvider"/> backend to use. Defaults to
    /// <see cref="MockProviderKind.OpenRouter"/>, which itself falls back to the deterministic
    /// offline provider when <see cref="OpenRouterApiKey"/> is not set.</summary>
    public MockProviderKind Provider { get; set; } = MockProviderKind.OpenRouter;

    /// <summary>OpenRouter API key. When null/empty (and <see cref="Provider"/> is
    /// <see cref="MockProviderKind.OpenRouter"/>), MockingBird falls back to a deterministic,
    /// offline mock provider instead of calling OpenRouter.</summary>
    public string? OpenRouterApiKey { get; set; }

    /// <summary>OpenRouter model id to request, e.g. "anthropic/claude-3.5-haiku".</summary>
    public string Model { get; set; } = "anthropic/claude-3.5-haiku";

    /// <summary>Base address for the OpenRouter API. Overridable for testing.</summary>
    public string OpenRouterBaseAddress { get; set; } = "https://openrouter.ai/api/v1/";

    /// <summary>Configuration used when <see cref="Provider"/> is <see cref="MockProviderKind.Bedrock"/>.</summary>
    public BedrockOptions Bedrock { get; set; } = new();

    /// <summary>The name of the Swashbuckle document to read operations from, matching the name
    /// passed to <c>SwaggerDoc(name, ...)</c>. Relevant only when more than one document is registered.</summary>
    public string DocumentName { get; set; } = "v1";

    /// <summary>Whether generated responses are cached in-memory, keyed by route/method/params.</summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>How long a cached mock response is retained.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Request header that, when present (any value), bypasses the cache and forces
    /// regeneration of a mock response.</summary>
    public string RefreshHeaderName { get; set; } = "X-MockingBird-Refresh";

    /// <summary>When true, mocked responses carry an "X-MockingBird-Mocked: true" response header
    /// so it's obvious during development which responses are synthetic. Does not affect the body,
    /// status code, or content type.</summary>
    public bool EmitMockHeaders { get; set; } = true;

    /// <summary>Maximum number of generation attempts (initial + retries) before giving up and
    /// returning an error response. Must be at least 1.</summary>
    public int MaxGenerationAttempts { get; set; } = 2;

    /// <summary>Optional override for sourcing the <see cref="OpenApiDocument"/> MockingBird reads
    /// operations from, instead of resolving it from the app's registered Swashbuckle provider.</summary>
    public Func<IServiceProvider, CancellationToken, Task<OpenApiDocument>>? DocumentFactory { get; set; }

    /// <summary>The extension name checked on operations (and path items) to determine whether
    /// MockingBird should intercept them.</summary>
    public string ExtensionName { get; set; } = "x-mockingbird";
}
