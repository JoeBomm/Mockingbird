using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace MockingBird.Middleware.Providers;

/// <summary>
/// A schema-driven <see cref="IMockProvider"/> that fabricates values locally instead of calling an
/// LLM. Values are seeded from the request so the same request keeps producing the same data.
/// Used automatically when no OpenRouter API key is configured, which keeps the TestApi demo and
/// the whole test suite runnable fully offline, and doubles as proof that the provider abstraction
/// is genuinely swappable.
/// </summary>
public sealed class DeterministicMockProvider : IMockProvider
{
    public Task<string> GenerateAsync(MockRequestContext context, CancellationToken cancellationToken = default)
    {
        var seed = ComputeSeed(context);
        var random = new Random(seed);
        var value = GenerateValue(context.Schema, random, context, depth: 0);
        return Task.FromResult(value?.ToJsonString() ?? "null");
    }

    private static int ComputeSeed(MockRequestContext context)
    {
        var key = string.Join('|', new[] { context.HttpMethod, context.PathTemplate }
            .Concat(context.RouteValues.Select(kvp => $"{kvp.Key}={kvp.Value}"))
            .Concat(context.QueryValues.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt32(hash, 0);
    }

    private static JsonNode? GenerateValue(JsonNode schemaNode, Random random, MockRequestContext context, int depth)
    {
        var schema = Resolve(schemaNode, context.Schema);
        if (schema is not JsonObject obj)
        {
            return JsonValue.Create(true);
        }

        if (obj.TryGetPropertyValue("enum", out var enumNode) && enumNode is JsonArray { Count: > 0 } enumValues)
        {
            return enumValues[random.Next(enumValues.Count)]?.DeepClone();
        }

        if (obj.TryGetPropertyValue("const", out var constNode))
        {
            return constNode?.DeepClone();
        }

        if (TryGetComposedSchema(obj, out var composed))
        {
            return GenerateValue(composed, random, context, depth);
        }

        var type = SelectType(obj);

        return type switch
        {
            "object" => GenerateObject(obj, random, context, depth),
            "array" => GenerateArray(obj, random, context, depth),
            "integer" => JsonValue.Create(random.Next(1, 1000)),
            "number" => JsonValue.Create(Math.Round(random.NextDouble() * 1000, 2)),
            "boolean" => JsonValue.Create(random.Next(2) == 0),
            "null" => null,
            "string" or null => JsonValue.Create(GenerateString(obj, random)),
            _ => JsonValue.Create(GenerateString(obj, random)),
        };
    }

    /// <summary>Reads the schema's "type" keyword, which JSON Schema 2020-12 allows to be either a
    /// single string (<c>"string"</c>) or an array of strings (<c>["string","null"]</c> for a
    /// nullable property). Prefers a concrete, non-null type when several are listed.</summary>
    private static string? SelectType(JsonObject obj)
    {
        if (!obj.TryGetPropertyValue("type", out var typeNode) || typeNode is null)
        {
            return null;
        }

        if (typeNode is JsonValue single)
        {
            return single.GetValue<string>();
        }

        if (typeNode is JsonArray array)
        {
            var names = array.Select(n => n?.GetValue<string>()).Where(n => n is not null).ToList();
            return names.FirstOrDefault(n => n != "null") ?? names.FirstOrDefault();
        }

        return null;
    }

    private static bool TryGetComposedSchema(JsonObject obj, out JsonNode composed)
    {
        foreach (var keyword in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (obj.TryGetPropertyValue(keyword, out var node) && node is JsonArray { Count: > 0 } array &&
                array[0] is not null)
            {
                composed = array[0]!;
                return true;
            }
        }
        composed = null!;
        return false;
    }

    private static JsonObject GenerateObject(JsonObject schema, Random random, MockRequestContext context, int depth)
    {
        var result = new JsonObject();
        if (depth > 6)
        {
            return result;
        }

        if (schema.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject props)
        {
            var required = schema.TryGetPropertyValue("required", out var reqNode) && reqNode is JsonArray reqArray
                ? reqArray.Select(n => n?.GetValue<string>()).Where(s => s is not null).ToHashSet()
                : new HashSet<string?>(props.Select(p => p.Key));

            foreach (var (name, propertySchema) in props)
            {
                if (propertySchema is null || !required.Contains(name))
                {
                    continue;
                }

                if (TryUseRequestValue(name, context, out var overrideValue))
                {
                    result[name] = overrideValue;
                    continue;
                }

                result[name] = GenerateValue(propertySchema, random, context, depth + 1);
            }
        }

        return result;
    }

    private static bool TryUseRequestValue(string propertyName, MockRequestContext context, out JsonNode? value)
    {
        foreach (var source in new[] { context.RouteValues, context.QueryValues })
        {
            if (source.TryGetValue(propertyName, out var raw) && raw is not null)
            {
                value = JsonValue.Create(raw);
                return true;
            }
        }
        value = null;
        return false;
    }

    private static JsonArray GenerateArray(JsonObject schema, Random random, MockRequestContext context, int depth)
    {
        var array = new JsonArray();
        if (depth > 6 || !schema.TryGetPropertyValue("items", out var itemsNode) || itemsNode is null)
        {
            return array;
        }

        var count = random.Next(1, 4);
        for (var i = 0; i < count; i++)
        {
            array.Add(GenerateValue(itemsNode, random, context, depth + 1));
        }
        return array;
    }

    private static string GenerateString(JsonObject schema, Random random)
    {
        var format = schema.TryGetPropertyValue("format", out var formatNode) ? formatNode?.GetValue<string>() : null;
        return format switch
        {
            "date-time" => DateTimeOffset.UnixEpoch.AddDays(random.Next(0, 20000)).AddSeconds(random.Next(0, 86400))
                .ToString("O"),
            "date" => DateOnly.FromDateTime(DateTime.UnixEpoch.AddDays(random.Next(0, 20000))).ToString("O"),
            "uuid" => Guid.NewGuid().ToString(),
            "email" => $"user{random.Next(1000, 9999)}@example.com",
            "uri" or "url" => $"https://example.com/resource/{random.Next(1, 1000)}",
            _ => GeneratePlaceholderWord(random),
        };
    }

    private static readonly string[] Words =
    [
        "amber", "cedar", "harbor", "lumen", "quartz", "willow", "delta", "ember",
        "granite", "meadow", "opal", "ridge", "sable", "tundra", "violet", "zephyr",
    ];

    private static string GeneratePlaceholderWord(Random random) =>
        $"{Words[random.Next(Words.Length)]}-{Words[random.Next(Words.Length)]}-{random.Next(10, 99)}";

    /// <summary>Resolves a possible <c>$ref</c> node against the root schema's <c>$defs</c>. The
    /// ref may appear alongside sibling keywords (e.g. the root schema is itself
    /// <c>{ "$ref": "#/$defs/Order", "$defs": {...} }</c>), so this doesn't require the ref to be
    /// the object's only property.</summary>
    private static JsonNode? Resolve(JsonNode node, JsonNode root)
    {
        if (node is not JsonObject obj || !obj.TryGetPropertyValue("$ref", out var refNode) ||
            refNode?.GetValue<string>() is not { } refString || !refString.StartsWith("#/$defs/", StringComparison.Ordinal))
        {
            return node;
        }

        var name = refString["#/$defs/".Length..];
        if (root is JsonObject rootObj && rootObj.TryGetPropertyValue("$defs", out var defsNode) &&
            defsNode is JsonObject defs && defs.TryGetPropertyValue(name, out var target) && target is not null)
        {
            return target;
        }

        return node;
    }
}
