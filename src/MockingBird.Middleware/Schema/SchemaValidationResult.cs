namespace MockingBird.Middleware.Schema;

public sealed record SchemaValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static readonly SchemaValidationResult Success = new(true, Array.Empty<string>());
}
