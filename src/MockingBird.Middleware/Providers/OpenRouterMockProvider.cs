using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockingBird.Middleware.Configuration;

namespace MockingBird.Middleware.Providers;

/// <summary>
/// Default <see cref="IMockProvider"/>: calls OpenRouter's chat completions endpoint, requesting
/// JSON-only output, and returns the raw model response text for the caller to validate.
/// </summary>
public sealed class OpenRouterMockProvider(
    HttpClient httpClient,
    IOptions<MockingBirdOptions> options,
    ILogger<OpenRouterMockProvider> logger) : IMockProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GenerateAsync(MockRequestContext context, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;

        var request = new ChatCompletionRequest
        {
            Model = opts.Model,
            ResponseFormat = new ResponseFormat { Type = "json_object" },
            Messages =
            [
                new ChatMessage("system", MockPromptBuilder.SystemPrompt),
                new ChatMessage("user", MockPromptBuilder.BuildUserPrompt(context)),
            ],
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request, options: SerializerOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.OpenRouterApiKey);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("OpenRouter request failed with {StatusCode}: {Body}", response.StatusCode, body);
            throw new MockProviderException(
                $"OpenRouter request failed with status {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        var completion = await response.Content
            .ReadFromJsonAsync<ChatCompletionResponse>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new MockProviderException("OpenRouter returned an empty completion.");
        }

        return JsonCompletionText.StripCodeFences(content);
    }

    private sealed record ChatCompletionRequest
    {
        public required string Model { get; init; }
        public required IReadOnlyList<ChatMessage> Messages { get; init; }

        [JsonPropertyName("response_format")]
        public required ResponseFormat ResponseFormat { get; init; }
    }

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ResponseFormat
    {
        public required string Type { get; init; }
    }

    private sealed record ChatCompletionResponse
    {
        public List<ChatChoice>? Choices { get; init; }
    }

    private sealed record ChatChoice
    {
        public ChatMessage? Message { get; init; }
    }
}

public sealed class MockProviderException(string message) : Exception(message);
