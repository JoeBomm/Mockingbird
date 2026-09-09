using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.OpenApi;
using MockingBird.Middleware.Configuration;

namespace MockingBird.Middleware.Routing;

/// <summary>
/// Builds the (method, route) -> <see cref="MockOperation"/> lookup from an <see cref="OpenApiDocument"/>
/// by scanning every operation for the <c>x-mockingbird</c> vendor extension, and matches incoming
/// requests against it using ASP.NET Core's own route-template engine.
/// </summary>
public sealed class MockOperationRegistry
{
    private readonly List<MockOperation> _operations;

    public MockOperationRegistry(OpenApiDocument document, MockingBirdOptions options)
    {
        _operations = Build(document, options);
    }

    public IReadOnlyList<MockOperation> Operations => _operations;

    /// <summary>
    /// Finds the most specific flagged operation matching the request's method and path, if any,
    /// and extracts the route values bound by its template.
    /// </summary>
    public MockOperation? TryMatch(HttpRequest request, out RouteValueDictionary routeValues)
    {
        var method = HttpMethod.Parse(request.Method);
        // request.Path is already relative to PathBase (ASP.NET Core splits them at the point the
        // app is mounted), and OpenAPI path templates are written relative to that same base — so
        // it, not PathBase + Path, is what the templates match against.
        var path = request.Path;

        foreach (var candidate in _operations)
        {
            if (candidate.Method != method)
            {
                continue;
            }

            var values = new RouteValueDictionary();
            var matcher = new TemplateMatcher(candidate.RouteTemplate, new RouteValueDictionary());
            if (matcher.TryMatch(path, values))
            {
                routeValues = values;
                return candidate;
            }
        }

        routeValues = new RouteValueDictionary();
        return null;
    }

    private static List<MockOperation> Build(OpenApiDocument document, MockingBirdOptions options)
    {
        var operations = new List<MockOperation>();

        foreach (var (rawPath, pathItem) in document.Paths)
        {
            var pathFlagged = IsFlagged(pathItem.Extensions, options.ExtensionName);
            var template = TemplateParser.Parse(NormalizeTemplate(rawPath));

            foreach (var (method, operation) in pathItem.Operations ?? [])
            {
                if (!pathFlagged && !IsFlagged(operation.Extensions, options.ExtensionName))
                {
                    continue;
                }

                if (operation.Responses is not { } responses)
                {
                    continue;
                }

                var (statusCode, response) = SelectResponse(responses);
                if (response is null)
                {
                    continue;
                }

                var (contentType, schema) = SelectContent(response);

                operations.Add(new MockOperation
                {
                    PathTemplate = rawPath,
                    Method = method,
                    RouteTemplate = template,
                    Operation = operation,
                    ResponseStatusCode = statusCode,
                    Response = response,
                    ContentType = contentType,
                    ResponseSchema = schema,
                });
            }
        }

        // Most specific first: fewer route parameters wins so a literal segment
        // (/v1/orders/count) is tried before a parameterized one (/v1/orders/{id}).
        return [.. operations.OrderBy(o => o.Specificity).ThenBy(o => o.PathTemplate, StringComparer.Ordinal)];
    }

    private static string NormalizeTemplate(string openApiPath) =>
        openApiPath.StartsWith('/') ? openApiPath[1..] : openApiPath;

    private static bool IsFlagged(IDictionary<string, IOpenApiExtension>? extensions, string extensionName)
    {
        if (extensions is null || !extensions.TryGetValue(extensionName, out var extension))
        {
            return false;
        }

        if (extension is not JsonNodeExtension { Node: JsonValue value })
        {
            return false;
        }

        if (value.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        return value.TryGetValue<string>(out var stringValue) &&
               bool.TryParse(stringValue, out var parsed) && parsed;
    }

    private static (int StatusCode, IOpenApiResponse? Response) SelectResponse(OpenApiResponses responses)
    {
        string? bestKey = null;
        foreach (var key in responses.Keys)
        {
            if (key.Length == 3 && key[0] == '2' && char.IsDigit(key[1]) && char.IsDigit(key[2]))
            {
                if (bestKey is null || string.CompareOrdinal(key, bestKey) < 0)
                {
                    bestKey = key;
                }
            }
        }

        bestKey ??= responses.Keys.Contains("default") ? "default" : responses.Keys.FirstOrDefault();
        if (bestKey is null)
        {
            return (200, null);
        }

        var statusCode = int.TryParse(bestKey, out var parsed) ? parsed : 200;
        return (statusCode, responses[bestKey]);
    }

    private static (string ContentType, IOpenApiSchema? Schema) SelectContent(IOpenApiResponse response)
    {
        if (response.Content is not { Count: > 0 } content)
        {
            return ("application/json", null);
        }

        if (content.TryGetValue("application/json", out var exact))
        {
            return ("application/json", exact.Schema);
        }

        var jsonLike = content.FirstOrDefault(kvp => kvp.Key.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ||
                                                       kvp.Key.EndsWith("/json", StringComparison.OrdinalIgnoreCase));
        if (jsonLike.Key is not null)
        {
            return (jsonLike.Key, jsonLike.Value.Schema);
        }

        var first = content.First();
        return (first.Key, first.Value.Schema);
    }
}
