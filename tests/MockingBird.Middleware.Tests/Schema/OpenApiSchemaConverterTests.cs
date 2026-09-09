using System.Text.Json.Nodes;
using MockingBird.Middleware.Schema;
using MockingBird.Middleware.Tests.TestSupport;

namespace MockingBird.Middleware.Tests.Schema;

public sealed class OpenApiSchemaConverterTests
{
    [Fact]
    public async Task Ref_to_component_schema_is_rewritten_to_defs()
    {
        var document = TestDocuments.BuildSampleDocument();
        var schema = TestDocuments.GetResponseSchema(document, "/v1/widgets/{id}", HttpMethod.Get);

        var result = await OpenApiSchemaConverter.ToStandaloneJsonSchemaAsync(schema, document);

        var obj = Assert.IsType<JsonObject>(result);
        Assert.Equal("#/$defs/Widget", obj["$ref"]!.GetValue<string>());
        Assert.NotNull(obj["$defs"]);
        Assert.NotNull(obj["$defs"]!["Widget"]);
        // The original OpenAPI-style ref must not survive the rewrite.
        Assert.DoesNotContain("#/components/schemas/", result.ToJsonString());
    }

    [Fact]
    public async Task Nested_array_item_ref_is_rewritten()
    {
        var document = TestDocuments.BuildSampleDocument();
        var schema = TestDocuments.GetResponseSchema(document, "/v1/widgets", HttpMethod.Get);

        var result = await OpenApiSchemaConverter.ToStandaloneJsonSchemaAsync(schema, document);

        var itemsRef = result["items"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/$defs/Widget", itemsRef);
        Assert.NotNull(result["$defs"]!["Widget"]);
    }

    [Fact]
    public async Task Recursive_schema_does_not_hang_and_keeps_self_reference()
    {
        var document = TestDocuments.BuildSampleDocument();
        var schema = TestDocuments.GetResponseSchema(document, "/v1/nodes/{id}", HttpMethod.Get);

        var result = await OpenApiSchemaConverter.ToStandaloneJsonSchemaAsync(schema, document)
            .WaitAsync(TimeSpan.FromSeconds(5));

        var nodeDef = result["$defs"]!["Node"]!;
        var childRef = nodeDef["properties"]!["children"]!["items"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/$defs/Node", childRef);
    }

    [Fact]
    public async Task Enum_values_survive_the_round_trip()
    {
        var document = TestDocuments.BuildSampleDocument();
        var schema = TestDocuments.GetResponseSchema(document, "/v1/widgets/{id}", HttpMethod.Get);

        var result = await OpenApiSchemaConverter.ToStandaloneJsonSchemaAsync(schema, document);

        var statusEnum = result["$defs"]!["Widget"]!["properties"]!["status"]!["enum"]!.AsArray();
        Assert.Contains(statusEnum, n => n!.GetValue<string>() == "active");
        Assert.Contains(statusEnum, n => n!.GetValue<string>() == "inactive");
    }

    [Fact]
    public async Task Required_fields_survive_the_round_trip()
    {
        var document = TestDocuments.BuildSampleDocument();
        var schema = TestDocuments.GetResponseSchema(document, "/v1/widgets/{id}", HttpMethod.Get);

        var result = await OpenApiSchemaConverter.ToStandaloneJsonSchemaAsync(schema, document);

        var required = result["$defs"]!["Widget"]!["required"]!.AsArray().Select(n => n!.GetValue<string>());
        Assert.Contains("id", required);
        Assert.Contains("name", required);
    }

    [Fact]
    public async Task Non_referenced_component_schemas_are_still_bundled_into_defs()
    {
        // Even a schema unrelated to the operation being converted should be reachable in $defs,
        // since the target schema might reference it transitively (e.g. Node -> Node).
        var document = TestDocuments.BuildSampleDocument();
        var schema = TestDocuments.GetResponseSchema(document, "/v1/widgets/{id}", HttpMethod.Get);

        var result = await OpenApiSchemaConverter.ToStandaloneJsonSchemaAsync(schema, document);

        Assert.NotNull(result["$defs"]!["Node"]);
    }
}
