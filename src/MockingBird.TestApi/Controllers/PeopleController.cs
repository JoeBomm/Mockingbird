using Microsoft.AspNetCore.Mvc;
using MockingBird.TestApi.Data;
using MockingBird.TestApi.Models;

namespace MockingBird.TestApi.Controllers;

/// <summary>
/// Real, fully-implemented endpoints (the control group). MockingBird must leave these untouched.
/// </summary>
[ApiController]
[Route("v1/people")]
public sealed class PeopleController(PeopleStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IEnumerable<Person>>(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<Person>> GetAll() => Ok(store.GetAll());

    [HttpGet("{id:int}")]
    [ProducesResponseType<Person>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Person> GetById(int id)
    {
        var person = store.GetById(id);
        return person is null ? NotFound() : Ok(person);
    }
}
