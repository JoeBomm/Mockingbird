using System.Text.Json;
using Json.Schema;

namespace MockingBird.Middleware.Schema;

/// <summary>
/// Validates candidate mock response bodies against the standalone JSON Schema produced by
/// <see cref="OpenApiSchemaConverter"/>.
/// </summary>
public static class MockResponseValidator
{
    public static SchemaValidationResult Validate(JsonSchema schema, string candidateJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(candidateJson);
        }
        catch (JsonException ex)
        {
            return new SchemaValidationResult(false, [$"Response was not valid JSON: {ex.Message}"]);
        }

        using (document)
        {
            var results = schema.Evaluate(document.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });

            if (results.IsValid)
            {
                return SchemaValidationResult.Success;
            }

            var errors = new List<string>();
            CollectErrors(results, errors);
            if (errors.Count == 0)
            {
                errors.Add("The response did not satisfy the schema.");
            }
            return new SchemaValidationResult(false, errors);
        }
    }

    private static void CollectErrors(EvaluationResults results, List<string> errors)
    {
        if (results.Errors is { Count: > 0 })
        {
            var location = results.InstanceLocation.ToString();
            var at = string.IsNullOrEmpty(location) ? "(root)" : location;
            foreach (var (keyword, message) in results.Errors)
            {
                errors.Add($"{at}: [{keyword}] {message}");
            }
        }

        if (results.Details is { Count: > 0 })
        {
            foreach (var detail in results.Details)
            {
                CollectErrors(detail, errors);
            }
        }
    }
}
