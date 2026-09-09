using MockingBird.Middleware.Providers;

namespace MockingBird.Middleware.Tests.TestSupport;

/// <summary>
/// A canned-response <see cref="IMockProvider"/> for testing the rest of the pipeline (routing,
/// caching, validation, retry, error handling) without depending on OpenRouter.
/// </summary>
public sealed class FakeMockProvider : IMockProvider
{
    private readonly Queue<string> _responses;
    private string _lastResponse;

    public FakeMockProvider(params string[] responses)
    {
        _responses = new Queue<string>(responses);
        _lastResponse = responses.Length > 0 ? responses[^1] : "{}";
    }

    public int CallCount { get; private set; }
    public List<MockRequestContext> Calls { get; } = [];

    public Task<string> GenerateAsync(MockRequestContext context, CancellationToken cancellationToken = default)
    {
        CallCount++;
        Calls.Add(context);
        if (_responses.Count > 0)
        {
            _lastResponse = _responses.Dequeue();
        }
        return Task.FromResult(_lastResponse);
    }
}
