using Amazon;
using Amazon.BedrockRuntime;
using Amazon.Runtime;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockingBird.Middleware.Caching;
using MockingBird.Middleware.Configuration;
using MockingBird.Middleware.Providers;
using MockingBird.Middleware.Routing;

namespace MockingBird.Middleware;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers MockingBird's services. Call before <c>builder.Build()</c>; pair with
    /// <c>app.UseMockingBird()</c> in the pipeline.
    /// </summary>
    public static IServiceCollection AddMockingBird(
        this IServiceCollection services,
        Action<MockingBirdOptions>? configure = null)
    {
        services.Configure<MockingBirdOptions>(o => configure?.Invoke(o));
        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<MockingBirdOptions>>().Value);

        services.AddMemoryCache();
        services.TryAddSingleton<MockResponseCache>();
        services.TryAddSingleton<MockOperationRegistrySource>();

        services.AddHttpClient<OpenRouterMockProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<MockingBirdOptions>();
            client.BaseAddress = new Uri(options.OpenRouterBaseAddress);
        });
        services.TryAddSingleton<DeterministicMockProvider>();

        services.TryAddSingleton<IAmazonBedrockRuntime>(sp =>
        {
            var bedrock = sp.GetRequiredService<MockingBirdOptions>().Bedrock;
            var region = RegionEndpoint.GetBySystemName(bedrock.Region);

            return string.IsNullOrWhiteSpace(bedrock.AccessKeyId)
                ? new AmazonBedrockRuntimeClient(region)
                : new AmazonBedrockRuntimeClient(
                    string.IsNullOrWhiteSpace(bedrock.SessionToken)
                        ? new BasicAWSCredentials(bedrock.AccessKeyId, bedrock.SecretAccessKey)
                        : new SessionAWSCredentials(bedrock.AccessKeyId, bedrock.SecretAccessKey, bedrock.SessionToken),
                    region);
        });
        services.TryAddSingleton<BedrockMockProvider>();

        services.TryAddSingleton<IMockProvider>(sp =>
        {
            var options = sp.GetRequiredService<MockingBirdOptions>();

            if (options.Provider == MockProviderKind.Bedrock)
            {
                return sp.GetRequiredService<BedrockMockProvider>();
            }

            if (!string.IsNullOrWhiteSpace(options.OpenRouterApiKey))
            {
                return sp.GetRequiredService<OpenRouterMockProvider>();
            }

            sp.GetRequiredService<ILoggerFactory>().CreateLogger("MockingBird").LogWarning(
                "No provider configured (MockingBirdOptions.Provider is OpenRouter but " +
                "OpenRouterApiKey is not set); falling back to the deterministic offline mock " +
                "provider. Mock data will be schema-valid but not LLM-generated.");
            return sp.GetRequiredService<DeterministicMockProvider>();
        });

        return services;
    }
}
