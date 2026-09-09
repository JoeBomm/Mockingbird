namespace MockingBird.Middleware.Providers;

/// <summary>
/// Shared helpers for cleaning up raw LLM completion text before it's handed off for schema
/// validation, used by every <see cref="IMockProvider"/> that calls out to a chat-style model.
/// </summary>
internal static class JsonCompletionText
{
    /// <summary>Strips a leading/trailing ``` code fence (with an optional language tag) that some
    /// models wrap JSON output in, despite being asked not to.</summary>
    public static string StripCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var withoutOpeningFence = trimmed[(firstNewline + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0
            ? withoutOpeningFence[..closingFenceIndex].Trim()
            : withoutOpeningFence.Trim();
    }
}
