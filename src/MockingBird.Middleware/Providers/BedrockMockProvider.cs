using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockingBird.Middleware.Configuration;

namespace MockingBird.Middleware.Providers;

/// <summary>
/// <see cref="IMockProvider"/> that calls Amazon Bedrock's Converse API, requesting JSON-only
/// output, and returns the raw model response text for the caller to validate.
/// </summary>
public sealed class BedrockMockProvider(
    IAmazonBedrockRuntime client,
    IOptions<MockingBirdOptions> options,
    ILogger<BedrockMockProvider> logger) : IMockProvider
{
    public async Task<string> GenerateAsync(MockRequestContext context, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;

        var request = new ConverseRequest
        {
            ModelId = opts.Bedrock.ModelId,
            System = [new SystemContentBlock { Text = MockPromptBuilder.SystemPrompt }],
            Messages =
            [
                new Message
                {
                    Role = ConversationRole.User,
                    Content = [new ContentBlock { Text = MockPromptBuilder.BuildUserPrompt(context) }],
                },
            ],
        };

        ConverseResponse response;
        try
        {
            response = await client.ConverseAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonBedrockRuntimeException ex)
        {
            logger.LogWarning(ex, "Bedrock Converse request failed for model {ModelId}", opts.Bedrock.ModelId);
            throw new MockProviderException($"Bedrock Converse request failed: {ex.Message}");
        }

        var content = response.Output?.Message?.Content?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new MockProviderException("Bedrock returned an empty completion.");
        }

        return JsonCompletionText.StripCodeFences(content);
    }
}
