using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MockingBird.Middleware.Configuration;
using MockingBird.Middleware.Providers;

namespace MockingBird.Middleware.Tests.Providers;

public sealed class BedrockMockProviderTests
{
    private static MockRequestContext Context() => new()
    {
        HttpMethod = "GET",
        PathTemplate = "/v1/orders/{id}",
        Schema = new JsonObject { ["type"] = "object" },
        RouteValues = new Dictionary<string, string?> { ["id"] = "42" },
        QueryValues = new Dictionary<string, string?>(),
    };

    private static BedrockMockProvider CreateProvider(FakeBedrockRuntimeClient client, string modelId = "anthropic.claude-3-5-haiku-20241022-v1:0")
    {
        var options = Options.Create(new MockingBirdOptions { Bedrock = new BedrockOptions { ModelId = modelId } });
        return new BedrockMockProvider(client, options, NullLogger<BedrockMockProvider>.Instance);
    }

    [Fact]
    public async Task Sends_the_configured_model_id_and_built_prompts()
    {
        var client = new FakeBedrockRuntimeClient();
        client.ReturnsText("""{"id":"42"}""");
        var provider = CreateProvider(client, modelId: "anthropic.claude-3-haiku-test");

        await provider.GenerateAsync(Context());

        Assert.Equal("anthropic.claude-3-haiku-test", client.LastRequest!.ModelId);
        Assert.Equal(MockPromptBuilder.SystemPrompt, client.LastRequest.System[0].Text);
        Assert.Contains("42", client.LastRequest.Messages[0].Content[0].Text);
    }

    [Fact]
    public async Task Returns_the_completion_text_with_code_fences_stripped()
    {
        var client = new FakeBedrockRuntimeClient();
        client.ReturnsText("```json\n{\"id\":\"42\"}\n```");
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(Context());

        Assert.Equal("""{"id":"42"}""", result);
    }

    [Fact]
    public async Task Throws_when_the_completion_is_empty()
    {
        var client = new FakeBedrockRuntimeClient();
        client.ReturnsText("");
        var provider = CreateProvider(client);

        await Assert.ThrowsAsync<MockProviderException>(() => provider.GenerateAsync(Context()));
    }

    [Fact]
    public async Task Wraps_a_failed_call_in_a_MockProviderException()
    {
        var client = new FakeBedrockRuntimeClient();
        client.Throws(new Amazon.BedrockRuntime.AmazonBedrockRuntimeException("boom"));
        var provider = CreateProvider(client);

        var ex = await Assert.ThrowsAsync<MockProviderException>(() => provider.GenerateAsync(Context()));
        Assert.Contains("boom", ex.Message);
    }
}
