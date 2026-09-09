using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace MockingBird.Middleware.Schema;

/// <summary>
/// Converts an OpenAPI 3.1 schema (which resolves <c>$ref</c>s against
/// <c>#/components/schemas/...</c>) into a self-contained JSON Schema 2020-12 document that embeds
/// every referenced component schema under <c>$defs</c>. The result is usable directly by
/// JsonSchema.Net for validation and is compact enough to drop into an LLM prompt.
/// </summary>
public static class OpenApiSchemaConverter
{
    public static async Task<JsonNode> ToStandaloneJsonSchemaAsync(
        IOpenApiSchema targetSchema,
        OpenApiDocument document,
        CancellationToken cancellationToken = default)
    {
        var targetJson = await SerializeAsync(targetSchema, cancellationToken).ConfigureAwait(false);

        var defs = new JsonObject();
        if (document.Components?.Schemas is { Count: > 0 } componentSchemas)
        {
            foreach (var (name, schema) in componentSchemas)
            {
                var schemaJson = await SerializeAsync(schema, cancellationToken).ConfigureAwait(false);
                RewriteComponentRefs(schemaJson);
                defs[name] = schemaJson;
            }
        }

        if (targetJson is JsonObject targetObject)
        {
            RewriteComponentRefs(targetObject);
            if (defs.Count > 0)
            {
                targetObject["$defs"] = defs;
            }
            return targetObject;
        }

        // A non-object schema (e.g. a bare "true"/"false" schema) can't carry $defs; wrap it.
        var wrapper = new JsonObject { ["allOf"] = new JsonArray(targetJson) };
        if (defs.Count > 0)
        {
            wrapper["$defs"] = defs;
        }
        return wrapper;
    }

    private static async Task<JsonNode> SerializeAsync(IOpenApiSchema schema, CancellationToken cancellationToken)
    {
        var json = await schema.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, cancellationToken)
            .ConfigureAwait(false);
        return JsonNode.Parse(json) ?? new JsonObject();
    }

    /// <summary>
    /// Rewrites every <c>"$ref": "#/components/schemas/X"</c> found anywhere in the tree to
    /// <c>"$ref": "#/$defs/X"</c>. Walks structurally rather than string-replacing so it can't
    /// mis-rewrite a ref that happens to appear inside a string value or example.
    /// </summary>
    private static void RewriteComponentRefs(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.TryGetPropertyValue("$ref", out var refNode) && refNode is JsonValue refValue &&
                    refValue.TryGetValue<string>(out var refString) &&
                    refString.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                {
                    var name = refString["#/components/schemas/".Length..];
                    obj["$ref"] = $"#/$defs/{name}";
                }

                foreach (var property in obj.ToList())
                {
                    RewriteComponentRefs(property.Value);
                }
                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    RewriteComponentRefs(item);
                }
                break;
        }
    }
}
