using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace MockingBird.Middleware.Tests.TestSupport;

/// <summary>
/// Hand-built <see cref="OpenApiDocument"/>s used across the test suite, standing in for what
/// Swashbuckle would generate, so route-matching and schema-conversion logic can be tested without
/// spinning up a web host.
/// </summary>
public static class TestDocuments
{
    /// <summary>
    /// A document with:
    /// - GET /v1/widgets/{id}  (flagged, single Widget)
    /// - GET /v1/widgets       (flagged, array of Widget)
    /// - GET /v1/widgets/count (flagged, literal segment that also structurally matches
    ///                          /v1/widgets/{id} — used to test specificity ordering)
    /// - GET /v1/nodes/{id}    (flagged, recursive Node schema)
    /// - GET /v1/health        (NOT flagged — pass-through control)
    /// - the whole /v1/legacy path is flagged at the path-item level, inherited by its GET operation
    /// </summary>
    public static OpenApiDocument BuildSampleDocument()
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test", Version = "1" },
            Components = new OpenApiComponents { Schemas = new Dictionary<string, IOpenApiSchema>() },
        };

        var widgetSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "id", "name" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["status"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [JsonValue.Create("active"), JsonValue.Create("inactive")],
                },
                ["tags"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            },
        };
        document.Components.Schemas["Widget"] = widgetSchema;

        var nodeSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "name" },
        };
        document.Components.Schemas["Node"] = nodeSchema;
        nodeSchema.Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["children"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchemaReference("Node", document),
            },
        };

        AddOperation(document, "/v1/widgets/{id}", HttpMethod.Get, flagPathItem: false, flagOperation: true,
            parameters: [PathParameter("id")],
            statusCode: "200", schema: new OpenApiSchemaReference("Widget", document));

        AddOperation(document, "/v1/widgets", HttpMethod.Get, flagPathItem: false, flagOperation: true,
            parameters: [],
            statusCode: "200",
            schema: new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchemaReference("Widget", document) });

        AddOperation(document, "/v1/widgets/count", HttpMethod.Get, flagPathItem: false, flagOperation: true,
            parameters: [],
            statusCode: "200", schema: new OpenApiSchema { Type = JsonSchemaType.Integer });

        AddOperation(document, "/v1/nodes/{id}", HttpMethod.Get, flagPathItem: false, flagOperation: true,
            parameters: [PathParameter("id")],
            statusCode: "200", schema: new OpenApiSchemaReference("Node", document));

        AddOperation(document, "/v1/health", HttpMethod.Get, flagPathItem: false, flagOperation: false,
            parameters: [],
            statusCode: "200", schema: new OpenApiSchema { Type = JsonSchemaType.String });

        AddOperation(document, "/v1/legacy", HttpMethod.Get, flagPathItem: true, flagOperation: false,
            parameters: [],
            statusCode: "200", schema: new OpenApiSchema { Type = JsonSchemaType.String });

        return document;
    }

    private static void AddOperation(
        OpenApiDocument document,
        string path,
        HttpMethod method,
        bool flagPathItem,
        bool flagOperation,
        IReadOnlyList<IOpenApiParameter> parameters,
        string statusCode,
        IOpenApiSchema schema)
    {
        if (!document.Paths.TryGetValue(path, out var existing) || existing is not OpenApiPathItem pathItem)
        {
            pathItem = new OpenApiPathItem();
            if (flagPathItem)
            {
                pathItem.Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-mockingbird"] = new JsonNodeExtension(JsonValue.Create(true)),
                };
            }
            document.Paths[path] = pathItem;
        }

        var operation = new OpenApiOperation
        {
            Parameters = [.. parameters],
            Responses = new OpenApiResponses
            {
                [statusCode] = new OpenApiResponse
                {
                    Description = "test response",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new() { Schema = schema },
                    },
                },
            },
        };

        if (flagOperation)
        {
            operation.Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-mockingbird"] = new JsonNodeExtension(JsonValue.Create(true)),
            };
        }

        pathItem.AddOperation(method, operation);
    }

    /// <summary>Navigates path -> method -> status code -> JSON content schema, for tests that need
    /// the schema an operation was built with.</summary>
    public static IOpenApiSchema GetResponseSchema(
        OpenApiDocument document, string path, HttpMethod method, string statusCode = "200") =>
        document.Paths[path].Operations![method].Responses![statusCode].Content!["application/json"].Schema!;

    public static OpenApiOperation GetOperation(OpenApiDocument document, string path, HttpMethod method) =>
        document.Paths[path].Operations![method];

    private static IOpenApiParameter PathParameter(string name) => new OpenApiParameter
    {
        Name = name,
        In = ParameterLocation.Path,
        Required = true,
        Schema = new OpenApiSchema { Type = JsonSchemaType.String },
    };
}
