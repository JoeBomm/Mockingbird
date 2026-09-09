using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MockingBird.Middleware.Tests.EndToEnd;

/// <summary>
/// Runs the actual MockingBird.TestApi in-memory (no OpenRouter key set, so it falls back to the
/// deterministic provider) to prove the whole stack works together: Swashbuckle generates the
/// document, MockingBird reads it, real controllers are left alone, and mock-only endpoints return
/// schema-valid data.
/// </summary>
public sealed class TestApiEndToEndTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Real_endpoint_returns_data_from_the_actual_controller()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/people/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ada Lovelace", body);
        Assert.False(response.Headers.Contains("X-MockingBird-Mocked"));
    }

    [Fact]
    public async Task Real_endpoint_404_is_untouched_by_mockingbird()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/people/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Mock_only_get_by_id_returns_schema_shaped_data()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/orders/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("X-MockingBird-Mocked"));

        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("42", doc.RootElement.GetProperty("id").GetString());
        Assert.True(doc.RootElement.TryGetProperty("status", out _));
        Assert.True(doc.RootElement.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Mock_only_list_endpoint_returns_an_array()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task Mock_only_post_returns_created_status()
    {
        using var client = factory.CreateClient();
        var content = new StringContent(
            """{"items":[{"productName":"Widget","quantity":1,"unitPrice":9.99}]}""",
            System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/v1/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_document_lists_both_real_and_mock_only_paths()
    {
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/v1/people", json);
        Assert.Contains("/v1/orders", json);
        Assert.Contains("x-mockingbird", json);
    }
}
