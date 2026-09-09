using Microsoft.AspNetCore.Routing.Template;
using Microsoft.OpenApi;

namespace MockingBird.Middleware.Routing;

/// <summary>
/// A single OpenAPI operation flagged for mocking, with everything the pipeline needs to match a
/// request against it and build a response.
/// </summary>
public sealed class MockOperation
{
    public required string PathTemplate { get; init; }
    public required HttpMethod Method { get; init; }
    public required RouteTemplate RouteTemplate { get; init; }
    public required OpenApiOperation Operation { get; init; }
    public required int ResponseStatusCode { get; init; }
    public required IOpenApiResponse Response { get; init; }
    public required string ContentType { get; init; }
    public IOpenApiSchema? ResponseSchema { get; init; }

    /// <summary>Parameter count in the route template — used to prefer more specific routes
    /// (fewer/no parameters) over less specific ones when several templates could match.</summary>
    public int Specificity => RouteTemplate.Parameters.Count;
}
