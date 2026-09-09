using MockingBird.TestApi.Models;

namespace MockingBird.TestApi.Data;

/// <summary>In-memory backing store for the real (non-mocked) /v1/people endpoints.</summary>
public sealed class PeopleStore
{
    private readonly List<Person> _people =
    [
        new() { Id = 1, Name = "Ada Lovelace", Email = "ada@example.com", Department = "Engineering" },
        new() { Id = 2, Name = "Grace Hopper", Email = "grace@example.com", Department = "Engineering" },
        new() { Id = 3, Name = "Margaret Hamilton", Email = "margaret@example.com", Department = "Flight Software" },
    ];

    public IReadOnlyList<Person> GetAll() => _people;

    public Person? GetById(int id) => _people.FirstOrDefault(p => p.Id == id);
}
