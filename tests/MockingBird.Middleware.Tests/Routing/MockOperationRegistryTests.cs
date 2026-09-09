using Microsoft.AspNetCore.Http;
using MockingBird.Middleware.Configuration;
using MockingBird.Middleware.Routing;
using MockingBird.Middleware.Tests.TestSupport;

namespace MockingBird.Middleware.Tests.Routing;

public sealed class MockOperationRegistryTests
{
    private readonly MockOperationRegistry _registry =
        new(TestDocuments.BuildSampleDocument(), new MockingBirdOptions());

    private static HttpContext RequestFor(string method, string path, string? queryString = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (queryString is not null)
        {
            context.Request.QueryString = new QueryString(queryString);
        }
        return context;
    }

    [Fact]
    public void Unflagged_operations_are_never_registered()
    {
        Assert.DoesNotContain(_registry.Operations, o => o.PathTemplate == "/v1/health");
    }

    [Fact]
    public void Flagged_operation_matches_and_binds_route_values()
    {
        var request = RequestFor("GET", "/v1/widgets/123").Request;

        var match = _registry.TryMatch(request, out var routeValues);

        Assert.NotNull(match);
        Assert.Equal("/v1/widgets/{id}", match!.PathTemplate);
        Assert.Equal("123", routeValues["id"]);
    }

    [Fact]
    public void Path_item_level_flag_is_inherited_by_its_operation()
    {
        var request = RequestFor("GET", "/v1/legacy").Request;

        var match = _registry.TryMatch(request, out _);

        Assert.NotNull(match);
        Assert.Equal("/v1/legacy", match!.PathTemplate);
    }

    [Fact]
    public void Unflagged_route_never_matches_even_when_path_would_align()
    {
        var request = RequestFor("GET", "/v1/health").Request;

        var match = _registry.TryMatch(request, out _);

        Assert.Null(match);
    }

    [Fact]
    public void Near_miss_path_does_not_match()
    {
        var request = RequestFor("GET", "/v1/widgets/123/extra").Request;

        var match = _registry.TryMatch(request, out _);

        Assert.Null(match);
    }

    [Fact]
    public void Method_mismatch_does_not_match()
    {
        var request = RequestFor("POST", "/v1/widgets/123").Request;

        var match = _registry.TryMatch(request, out _);

        Assert.Null(match);
    }

    [Fact]
    public void More_specific_literal_route_wins_over_parameterized_route()
    {
        // /v1/widgets/count is a literal path, but /v1/widgets/{id} would ALSO structurally match
        // "/v1/widgets/count" by binding id="count". The literal, more specific route must win.
        var request = RequestFor("GET", "/v1/widgets/count").Request;

        var match = _registry.TryMatch(request, out var routeValues);

        Assert.NotNull(match);
        Assert.Equal("/v1/widgets/count", match!.PathTemplate);
        Assert.Empty(routeValues);
    }

    [Fact]
    public void Parameterized_route_still_matches_other_values()
    {
        var request = RequestFor("GET", "/v1/widgets/abc-123").Request;

        var match = _registry.TryMatch(request, out var routeValues);

        Assert.NotNull(match);
        Assert.Equal("/v1/widgets/{id}", match!.PathTemplate);
        Assert.Equal("abc-123", routeValues["id"]);
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var request = RequestFor("get", "/V1/WIDGETS/123").Request;

        var match = _registry.TryMatch(request, out var routeValues);

        Assert.NotNull(match);
        Assert.Equal("123", routeValues["id"]);
    }

    [Fact]
    public void Collection_route_without_parameters_matches_exactly()
    {
        var request = RequestFor("GET", "/v1/widgets").Request;

        var match = _registry.TryMatch(request, out var routeValues);

        Assert.NotNull(match);
        Assert.Equal("/v1/widgets", match!.PathTemplate);
        Assert.Empty(routeValues);
    }
}
