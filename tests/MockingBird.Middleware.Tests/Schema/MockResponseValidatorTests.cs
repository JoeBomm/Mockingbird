using Json.Schema;
using MockingBird.Middleware.Schema;

namespace MockingBird.Middleware.Tests.Schema;

public sealed class MockResponseValidatorTests
{
    private static readonly JsonSchema PersonSchema = JsonSchema.FromText("""
        {
          "type": "object",
          "properties": {
            "id": { "type": "string" },
            "age": { "type": "integer" },
            "role": { "type": "string", "enum": ["admin", "member"] }
          },
          "required": ["id", "age"]
        }
        """);

    [Fact]
    public void Valid_document_passes()
    {
        var result = MockResponseValidator.Validate(PersonSchema, """{"id":"1","age":30,"role":"admin"}""");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_required_property_fails_with_a_useful_message()
    {
        var result = MockResponseValidator.Validate(PersonSchema, """{"id":"1"}""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Wrong_type_fails_with_a_useful_message()
    {
        var result = MockResponseValidator.Validate(PersonSchema, """{"id":"1","age":"thirty"}""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Out_of_enum_value_fails_with_a_useful_message()
    {
        var result = MockResponseValidator.Validate(PersonSchema, """{"id":"1","age":30,"role":"superuser"}""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("enum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Malformed_json_fails_without_throwing()
    {
        var result = MockResponseValidator.Validate(PersonSchema, "{not json");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }
}
