using Microsoft.AspNetCore.Builder;

namespace MockingBird.Middleware;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Inserts MockingBird into the pipeline. Requests to operations flagged
    /// <c>x-mockingbird: true</c> in the app's OpenAPI document are answered with a generated mock
    /// response instead of reaching the rest of the pipeline; every other request passes through.
    /// Place after routing (if used) and before endpoint execution.
    /// </summary>
    public static IApplicationBuilder UseMockingBird(this IApplicationBuilder app) =>
        app.UseMiddleware<MockingBirdMiddleware>();
}
