using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;

namespace MockingBird.Middleware.Tests.Providers;

/// <summary>
/// A canned-response <see cref="IAmazonBedrockRuntime"/> for testing <see cref="MockingBird.Middleware.Providers.BedrockMockProvider"/>
/// without a real AWS call. Overrides only <see cref="ConverseAsync(ConverseRequest, CancellationToken)"/>,
/// the sole operation the provider uses; every other member is inherited unused from the real client.
/// </summary>
public sealed class FakeBedrockRuntimeClient() : AmazonBedrockRuntimeClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1)
{
    private Func<ConverseRequest, ConverseResponse>? _responseFactory;
    private Exception? _exceptionToThrow;

    public ConverseRequest? LastRequest { get; private set; }

    public void ReturnsText(string text) =>
        _responseFactory = _ => new ConverseResponse
        {
            Output = new ConverseOutput
            {
                Message = new Message { Content = [new ContentBlock { Text = text }] },
            },
        };

    public void Throws(Exception exception) => _exceptionToThrow = exception;

    public override Task<ConverseResponse> ConverseAsync(
        ConverseRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(_responseFactory?.Invoke(request) ?? new ConverseResponse());
    }
}
