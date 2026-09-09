namespace MockingBird.TestApi.Models;

/// <summary>A real, fully-implemented resource served from an in-memory store.</summary>
public sealed class Person
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Department { get; init; }
}
