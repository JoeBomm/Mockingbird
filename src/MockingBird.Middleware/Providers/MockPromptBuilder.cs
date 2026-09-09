using System.Text;
using System.Text.Json;

namespace MockingBird.Middleware.Providers;

/// <summary>
/// Builds the system/user prompt pair sent to an LLM-backed <see cref="IMockProvider"/>.
/// </summary>
public static class MockPromptBuilder
{
    private static readonly JsonSerializerOptions SchemaFormatting = new() { WriteIndented = true };

    public const string SystemPrompt =
        "You generate realistic example data for a mock API server. " +
        "You will be given a JSON Schema and the parameters of the HTTP request that triggered it. " +
        "Respond with a single JSON value that validates against the schema. " +
        "Use realistic, varied, human-plausible values (names, dates, ids, etc.) rather than " +
        "placeholders like \"string\" or \"foo\". Where a request parameter (route or query) " +
        "corresponds to a field in the schema (e.g. an \"id\" field and an {id} route value), the " +
        "generated value MUST match that request parameter exactly. " +
        "Respond with ONLY the JSON value: no prose, no explanation, no markdown code fences.";

    public static string BuildUserPrompt(MockRequestContext context)
    {
        var sb = new StringBuilder();
        sb.Append("HTTP ").Append(context.HttpMethod).Append(' ').Append(context.PathTemplate).AppendLine();

        if (!string.IsNullOrWhiteSpace(context.OperationSummary))
        {
            sb.Append("Summary: ").AppendLine(context.OperationSummary);
        }

        if (!string.IsNullOrWhiteSpace(context.OperationDescription))
        {
            sb.Append("Description: ").AppendLine(context.OperationDescription);
        }

        AppendParameters(sb, "Route parameters", context.RouteValues);
        AppendParameters(sb, "Query parameters", context.QueryValues);

        sb.AppendLine().AppendLine("JSON Schema the response must satisfy:");
        sb.AppendLine(context.Schema.ToJsonString(SchemaFormatting));

        if (context.PreviousAttempt is not null)
        {
            sb.AppendLine().AppendLine("Your previous attempt was invalid. Fix it. Previous attempt:");
            sb.AppendLine(context.PreviousAttempt);
            sb.AppendLine("Validation errors:");
            foreach (var error in context.ValidationErrors ?? [])
            {
                sb.Append("- ").AppendLine(error);
            }
        }

        return sb.ToString();
    }

    private static void AppendParameters(StringBuilder sb, string label, IReadOnlyDictionary<string, string?> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        sb.Append(label).Append(": ");
        sb.AppendLine(string.Join(", ", values.Select(kvp => $"{kvp.Key}={kvp.Value}")));
    }
}
