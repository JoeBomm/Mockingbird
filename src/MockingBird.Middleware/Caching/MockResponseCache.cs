using Microsoft.Extensions.Caching.Memory;
using MockingBird.Middleware.Configuration;
using MockingBird.Middleware.Routing;

namespace MockingBird.Middleware.Caching;

public sealed record CachedMockResponse(int StatusCode, string ContentType, string Body);

/// <summary>
/// Caches generated mock responses in-memory, keyed by method, route template, and normalized
/// route/query parameters, so repeated requests during a dev session return consistent data
/// instead of re-rolling on every call.
/// </summary>
public sealed class MockResponseCache(IMemoryCache memoryCache, MockingBirdOptions options)
{
    /// <summary>
    /// Returns the cached response for this request if one exists and the caller isn't forcing a
    /// refresh; otherwise generates one via <paramref name="factory"/>, caches it, and returns it.
    /// Concurrent first-hits for the same key share a single in-flight generation.
    /// </summary>
    public async Task<CachedMockResponse> GetOrCreateAsync(
        MockOperation operation,
        IReadOnlyDictionary<string, string?> routeValues,
        IReadOnlyDictionary<string, string?> queryValues,
        bool bypassCache,
        Func<Task<CachedMockResponse>> factory)
    {
        if (!options.CacheEnabled || bypassCache)
        {
            return await factory().ConfigureAwait(false);
        }

        var key = BuildKey(operation, routeValues, queryValues);

        var task = memoryCache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = options.CacheDuration;
            return factory();
        })!;

        try
        {
            return await task.ConfigureAwait(false);
        }
        catch
        {
            // Don't poison the cache with a failed generation attempt.
            memoryCache.Remove(key);
            throw;
        }
    }

    public void Invalidate(MockOperation operation, IReadOnlyDictionary<string, string?> routeValues,
        IReadOnlyDictionary<string, string?> queryValues) =>
        memoryCache.Remove(BuildKey(operation, routeValues, queryValues));

    private static string BuildKey(
        MockOperation operation,
        IReadOnlyDictionary<string, string?> routeValues,
        IReadOnlyDictionary<string, string?> queryValues)
    {
        var route = string.Join(',', routeValues.OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));
        var query = string.Join(',', queryValues.OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"mockingbird:{operation.Method}:{operation.PathTemplate}:route[{route}]:query[{query}]";
    }
}
