using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using MockingBird.TestApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MockingBird.TestApi.Swagger;

/// <summary>
/// Injects the "phantom" /v1/orders endpoints into the generated OpenAPI document: they exist only
/// in the spec (no controller implements them), and are flagged <c>x-mockingbird: true</c> so
/// MockingBird intercepts and mocks them. This is the one source of truth for their shape — they
/// show up in Swagger UI exactly like a real endpoint.
/// </summary>
public sealed class MockOnlyEndpointsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        var orderSchema = context.SchemaGenerator.GenerateSchema(typeof(Order), context.SchemaRepository);
        var orderListSchema = context.SchemaGenerator.GenerateSchema(typeof(List<Order>), context.SchemaRepository);
        var createOrderRequestSchema =
            context.SchemaGenerator.GenerateSchema(typeof(CreateOrderRequest), context.SchemaRepository);

        document.Paths["/v1/orders/{id}"] = BuildPathItem(
            (HttpMethod.Get, BuildOperation(
                summary: "Get an order by id",
                description: "Mock-only endpoint: no controller implements this yet.",
                parameters: [PathParameter("id", "The order id.")],
                responseStatusCode: "200",
                responseDescription: "The requested order.",
                responseSchema: orderSchema)));

        document.Paths["/v1/orders"] = BuildPathItem(
            (HttpMethod.Get, BuildOperation(
                summary: "List orders",
                description: "Mock-only endpoint: no controller implements this yet.",
                parameters: [QueryParameter("status", "Filter by order status.")],
                responseStatusCode: "200",
                responseDescription: "A page of orders.",
                responseSchema: orderListSchema)),
            (HttpMethod.Post, BuildOperation(
                summary: "Create an order",
                description: "Mock-only endpoint: no controller implements this yet.",
                parameters: [],
                requestBodySchema: createOrderRequestSchema,
                responseStatusCode: "201",
                responseDescription: "The created order.",
                responseSchema: orderSchema)));
    }

    private static IOpenApiPathItem BuildPathItem(params (HttpMethod Method, OpenApiOperation Operation)[] operations)
    {
        var pathItem = new OpenApiPathItem
        {
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-mockingbird"] = new JsonNodeExtension(JsonValue.Create(true)),
            },
        };

        foreach (var (method, operation) in operations)
        {
            pathItem.AddOperation(method, operation);
        }

        return pathItem;
    }

    private static OpenApiOperation BuildOperation(
        string summary,
        string description,
        IReadOnlyList<IOpenApiParameter> parameters,
        string responseStatusCode,
        string responseDescription,
        IOpenApiSchema responseSchema,
        IOpenApiSchema? requestBodySchema = null)
    {
        var operation = new OpenApiOperation
        {
            Summary = summary,
            Description = description,
            Parameters = [.. parameters],
            Responses = new OpenApiResponses
            {
                [responseStatusCode] = new OpenApiResponse
                {
                    Description = responseDescription,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new() { Schema = responseSchema },
                    },
                },
            },
        };

        if (requestBodySchema is not null)
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = requestBodySchema },
                },
            };
        }

        return operation;
    }

    private static IOpenApiParameter PathParameter(string name, string description) => new OpenApiParameter
    {
        Name = name,
        In = ParameterLocation.Path,
        Required = true,
        Description = description,
        Schema = new OpenApiSchema { Type = JsonSchemaType.String },
    };

    private static IOpenApiParameter QueryParameter(string name, string description) => new OpenApiParameter
    {
        Name = name,
        In = ParameterLocation.Query,
        Required = false,
        Description = description,
        Schema = new OpenApiSchema { Type = JsonSchemaType.String },
    };
}
