using System.Text.Json;
using Json.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockingBird.Middleware.Caching;
using MockingBird.Middleware.Configuration;
using MockingBird.Middleware.Providers;
using MockingBird.Middleware.Routing;
using MockingBird.Middleware.Schema;

namespace MockingBird.Middleware;

/// <summary>
/// Short-circuits requests to OpenAPI operations flagged with <c>x-mockingbird: true</c>, returning
/// an LLM-generated (and schema-validated) mock response with the operation's real status code and
/// content type. Every other request passes through to <c>next</c> untouched.
/// </summary>
public sealed class MockingBirdMiddleware(
    RequestDelegate next,
    MockOperationRegistrySource registrySource,
    MockResponseCache cache,
    IMockProvider provider,
    IOptions<MockingBirdOptions> options,
    ILogger<MockingBirdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var registry = await registrySource.GetAsync(context.RequestAborted).ConfigureAwait(false);
        var operation = registry.TryMatch(context.Request, out var matchedRouteValues);

        if (operation is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var opts = options.Value;
        var routeValues = ToStringDictionary(matchedRouteValues);
        var queryValues = ToStringDictionary(context.Request.Query);
        var bypassCache = context.Request.Headers.ContainsKey(opts.RefreshHeaderName);

        CachedMockResponse response;
        try
        {
            response = await cache.GetOrCreateAsync(
                operation, routeValues, queryValues, bypassCache,
                () => GenerateAsync(operation, routeValues, queryValues, context.RequestAborted)
            ).ConfigureAwait(false);
        }
        catch (MockGenerationFailedException ex)
        {
            logger.LogError(ex, "MockingBird failed to generate a valid response for {Method} {Path}",
                operation.Method, operation.PathTemplate);
            await WriteErrorAsync(context, operation, ex);
            return;
        }

        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = response.ContentType;
        if (opts.EmitMockHeaders)
        {
            context.Response.Headers["X-MockingBird-Mocked"] = "true";
        }
        await context.Response.WriteAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private async Task<CachedMockResponse> GenerateAsync(
        MockOperation operation,
        IReadOnlyDictionary<string, string?> routeValues,
        IReadOnlyDictionary<string, string?> queryValues,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var document = await registrySource.GetDocumentAsync(cancellationToken).ConfigureAwait(false);

        var schemaNode = operation.ResponseSchema is null
            ? throw new MockGenerationFailedException(
                $"Operation {operation.Method} {operation.PathTemplate} has no response schema to mock against.",
                [])
            : await OpenApiSchemaConverter.ToStandaloneJsonSchemaAsync(operation.ResponseSchema, document, cancellationToken)
                .ConfigureAwait(false);

        var jsonSchema = JsonSchema.FromText(schemaNode.ToJsonString());

        string? previousAttempt = null;
        IReadOnlyList<string>? previousErrors = null;
        var attempts = Math.Max(1, opts.MaxGenerationAttempts);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var requestContext = new MockRequestContext
            {
                HttpMethod = operation.Method.Method,
                PathTemplate = operation.PathTemplate,
                OperationSummary = operation.Operation.Summary,
                OperationDescription = operation.Operation.Description,
                Schema = schemaNode,
                RouteValues = routeValues,
                QueryValues = queryValues,
                PreviousAttempt = previousAttempt,
                ValidationErrors = previousErrors,
            };

            string candidate;
            try
            {
                candidate = await provider.GenerateAsync(requestContext, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Mock provider threw on attempt {Attempt} for {Method} {Path}",
                    attempt, operation.Method, operation.PathTemplate);
                previousAttempt = null;
                previousErrors = [$"The provider failed: {ex.Message}"];
                continue;
            }

            var validation = ValidateAgainst(jsonSchema, candidate);
            if (validation.IsValid)
            {
                return new CachedMockResponse(operation.ResponseStatusCode, operation.ContentType, candidate);
            }

            logger.LogInformation(
                "Mock response for {Method} {Path} failed schema validation on attempt {Attempt}: {Errors}",
                operation.Method, operation.PathTemplate, attempt, string.Join("; ", validation.Errors));

            previousAttempt = candidate;
            previousErrors = validation.Errors;
        }

        throw new MockGenerationFailedException(
            $"MockingBird could not generate a schema-valid response for {operation.Method} {operation.PathTemplate} " +
            $"after {attempts} attempt(s).",
            previousErrors ?? []);
    }

    private static SchemaValidationResult ValidateAgainst(JsonSchema schema, string candidate)
    {
        try
        {
            return MockResponseValidator.Validate(schema, candidate);
        }
        catch (JsonException ex)
        {
            return new SchemaValidationResult(false, [$"Invalid JSON: {ex.Message}"]);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, MockOperation operation, MockGenerationFailedException ex)
    {
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        context.Response.ContentType = "application/problem+json";
        var body = JsonSerializer.Serialize(new
        {
            title = "MockingBird could not generate a valid mock response",
            status = StatusCodes.Status502BadGateway,
            method = operation.Method.Method,
            path = operation.PathTemplate,
            detail = ex.Message,
            validationErrors = ex.ValidationErrors,
        });
        await context.Response.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
    }

    private static Dictionary<string, string?> ToStringDictionary(RouteValueDictionary values) =>
        values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string?> ToStringDictionary(IQueryCollection query) =>
        query.ToDictionary(kvp => kvp.Key, string? (kvp) => kvp.Value.ToString(), StringComparer.OrdinalIgnoreCase);
}

public sealed class MockGenerationFailedException(string message, IReadOnlyList<string> validationErrors)
    : Exception(message)
{
    public IReadOnlyList<string> ValidationErrors { get; } = validationErrors;
}
