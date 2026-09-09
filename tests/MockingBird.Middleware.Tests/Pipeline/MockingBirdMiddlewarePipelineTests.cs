using System.Net;
using MockingBird.Middleware.Tests.TestSupport;

namespace MockingBird.Middleware.Tests.Pipeline;

public sealed class MockingBirdMiddlewarePipelineTests
{
    [Fact]
    public async Task Unflagged_request_passes_through_to_next_untouched()
    {
        await using var host = await PipelineTestHost.StartAsync();

        var response = await host.Client.GetAsync("/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("real-endpoint", await response.Content.ReadAsStringAsync());
        Assert.True(host.RealEndpointWasCalled);
        Assert.Equal(0, host.Provider.CallCount);
    }

    [Fact]
    public async Task Flagged_request_is_short_circuited_and_never_reaches_next()
    {
        await using var host = await PipelineTestHost.StartAsync();

        var response = await host.Client.GetAsync("/v1/widgets/123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(host.RealEndpointWasCalled);
        Assert.Equal(1, host.Provider.CallCount);
    }

    [Fact]
    public async Task Mock_response_uses_the_operations_status_code_and_content_type()
    {
        await using var host = await PipelineTestHost.StartAsync();

        var response = await host.Client.GetAsync("/v1/widgets/123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Mock_response_carries_the_mocked_header_by_default()
    {
        await using var host = await PipelineTestHost.StartAsync();

        var response = await host.Client.GetAsync("/v1/widgets/123");

        Assert.True(response.Headers.Contains("X-MockingBird-Mocked"));
    }

    [Fact]
    public async Task Mocked_header_can_be_disabled()
    {
        await using var host = await PipelineTestHost.StartAsync(o => o.EmitMockHeaders = false);

        var response = await host.Client.GetAsync("/v1/widgets/123");

        Assert.False(response.Headers.Contains("X-MockingBird-Mocked"));
    }

    [Fact]
    public async Task Repeated_requests_are_served_from_cache_without_calling_the_provider_again()
    {
        await using var host = await PipelineTestHost.StartAsync();

        var first = await host.Client.GetStringAsync("/v1/widgets/123");
        var second = await host.Client.GetStringAsync("/v1/widgets/123");

        Assert.Equal(first, second);
        Assert.Equal(1, host.Provider.CallCount);
    }

    [Fact]
    public async Task Different_route_values_are_cached_independently()
    {
        var provider = new FakeMockProvider(
            """{"id":"11111111-1111-1111-1111-111111111111","name":"First"}""",
            """{"id":"22222222-2222-2222-2222-222222222222","name":"Second"}""");
        await using var host = await PipelineTestHost.StartAsync(provider: provider);

        await host.Client.GetStringAsync("/v1/widgets/123");
        await host.Client.GetStringAsync("/v1/widgets/456");

        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task Refresh_header_bypasses_the_cache_and_regenerates()
    {
        var provider = new FakeMockProvider(
            """{"id":"11111111-1111-1111-1111-111111111111","name":"First"}""",
            """{"id":"22222222-2222-2222-2222-222222222222","name":"Second"}""");
        await using var host = await PipelineTestHost.StartAsync(provider: provider);

        var first = await host.Client.GetStringAsync("/v1/widgets/123");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/widgets/123");
        request.Headers.Add("X-MockingBird-Refresh", "true");
        var refreshedResponse = await host.Client.SendAsync(request);
        var refreshed = await refreshedResponse.Content.ReadAsStringAsync();

        Assert.NotEqual(first, refreshed);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task Invalid_response_is_retried_and_succeeds_on_the_second_attempt()
    {
        var provider = new FakeMockProvider(
            """{"name":"missing the required id field"}""",
            """{"id":"11111111-1111-1111-1111-111111111111","name":"Widget"}""");
        await using var host = await PipelineTestHost.StartAsync(provider: provider);

        var response = await host.Client.GetAsync("/v1/widgets/123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task Persistently_invalid_response_returns_502_instead_of_malformed_data()
    {
        var provider = new FakeMockProvider("""{"name":"still missing id"}""");
        await using var host = await PipelineTestHost.StartAsync(provider: provider);

        var response = await host.Client.GetAsync("/v1/widgets/123");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("validationErrors", body);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task Retry_prompt_includes_the_previous_attempt_and_its_errors()
    {
        var provider = new FakeMockProvider(
            """{"name":"missing the required id field"}""",
            """{"id":"11111111-1111-1111-1111-111111111111","name":"Widget"}""");
        await using var host = await PipelineTestHost.StartAsync(provider: provider);

        await host.Client.GetAsync("/v1/widgets/123");

        Assert.Equal(2, provider.Calls.Count);
        Assert.Null(provider.Calls[0].PreviousAttempt);
        Assert.NotNull(provider.Calls[1].PreviousAttempt);
        Assert.NotEmpty(provider.Calls[1].ValidationErrors!);
    }

    [Fact]
    public async Task Route_value_is_passed_through_to_the_provider_context()
    {
        var provider = new FakeMockProvider("""{"id":"123","name":"Widget"}""");
        await using var host = await PipelineTestHost.StartAsync(provider: provider);

        await host.Client.GetAsync("/v1/widgets/123");

        Assert.Equal("123", provider.Calls[0].RouteValues["id"]);
    }
}
