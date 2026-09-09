namespace MockingBird.Middleware.Configuration;

/// <summary>
/// Configuration for <see cref="MockProviderKind.Bedrock"/>, used when
/// <see cref="MockingBirdOptions.Provider"/> is set to <see cref="MockProviderKind.Bedrock"/>.
/// </summary>
public sealed class BedrockOptions
{
    /// <summary>AWS access key id. When null/empty, the AWS SDK's default credential provider
    /// chain is used instead (environment variables, shared credentials file, instance/task role,
    /// etc.) — set this only when you want to supply credentials explicitly.</summary>
    public string? AccessKeyId { get; set; }

    /// <summary>AWS secret access key. Required when <see cref="AccessKeyId"/> is set.</summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>Optional AWS session token, for temporary credentials.</summary>
    public string? SessionToken { get; set; }

    /// <summary>AWS region the Bedrock Runtime client connects to.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Bedrock model id (or inference profile id) to invoke, e.g.
    /// "anthropic.claude-3-5-haiku-20241022-v1:0". Some models/regions require a region-specific
    /// inference profile id instead of the base model id — check your AWS account's model access.</summary>
    public string ModelId { get; set; } = "anthropic.claude-3-5-haiku-20241022-v1:0";
}
