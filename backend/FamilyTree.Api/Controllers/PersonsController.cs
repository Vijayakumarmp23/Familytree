using FamilyTree.Api.Data;
using FamilyTree.Api.DTOs;
using FamilyTree.Api.Models;
using FamilyTree.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // -> /api/persons
public class PersonsController : ControllerBase
{
    private readonly FamilyTreeContext _db;
    private readonly GenealogyService _graph;

    public PersonsController(FamilyTreeContext db, GenealogyService graph)
    {
        _db = db;
        _graph = graph;
    }

    /// <summary>GET /api/persons - every person as a flat list (the graph nodes).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonDto>>> GetAll()
    {
        var people = await _db.Persons
            .OrderBy(p => p.DateOfBirth ?? DateTime.MaxValue)
            .ToListAsync();
        return Ok(people.Select(GenealogyService.ToDto));
    }

    /// <summary>GET /api/persons/{id} - person plus derived parents/spouses/children/siblings.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PersonDetailDto>> GetById(int id)
    {
        var detail = await _graph.GetDetailAsync(id);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>GET /api/persons/search?name= - partial name match.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<PersonDto>>> Search([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Ok(Array.Empty<PersonDto>());
        var term = name.Trim().ToLower();
        var people = await _db.Persons
            .Where(p => p.FullName.ToLower().Contains(term))
            .OrderBy(p => p.FullName)
            .ToListAsync();
        return Ok(people.Select(GenealogyService.ToDto));
    }

    /// <summary>
    /// POST /api/persons - create a person node. If FatherId / MotherId / SpouseId
    /// are supplied, the matching relationship edges are created in the same
    /// request. Existing people are LINKED, never copied.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PersonDto>> Create([FromBody] CreatePersonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("FullName is required.");

        // Validate any referenced relatives up-front.
        foreach (var refId in new[] { dto.FatherId, dto.MotherId, dto.SpouseId })
        {
            if (refId is int id && !await _db.Persons.AnyAsync(p => p.Id == id))
                return BadRequest($"Referenced person {id} does not exist.");
        }

        var person = new Person
        {
            FullName = dto.FullName.Trim(),
            Gender = dto.Gender,
            DateOfBirth = dto.DateOfBirth,
            DateOfDeath = dto.DateOfDeath,
            IsAlive = dto.IsAlive
        };

        _db.Persons.Add(person);
        await _db.SaveChangesAsync(); // person.Id now populated

        // Wire up the optional relationship edges (deduped by the service).
        if (dto.FatherId is int fatherId)
            await _graph.CreateRelationshipAsync(fatherId, person.Id, RelationshipType.ParentChild, dto.ParentsAreAdoptive);
        if (dto.MotherId is int motherId)
            await _graph.CreateRelationshipAsync(motherId, person.Id, RelationshipType.ParentChild, dto.ParentsAreAdoptive);
        if (dto.SpouseId is int spouseId)
            await _graph.CreateRelationshipAsync(person.Id, spouseId, RelationshipType.Spouse);

        var result = GenealogyService.ToDto(person);
        return CreatedAtAction(nameof(GetById), new { id = person.Id }, result);
    }

    /// <summary>
    /// PUT /api/persons/{id} - update a person's own attributes. Relationships
    /// are edited through the relationships endpoints, not here.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<PersonDto>> Update(int id, [FromBody] CreatePersonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("FullName is required.");

        var person = await _db.Persons.FindAsync(id);
        if (person is null) return NotFound();

        person.FullName = dto.FullName.Trim();
        person.Gender = dto.Gender;
        person.DateOfBirth = dto.DateOfBirth;
        person.DateOfDeath = dto.IsAlive ? null : dto.DateOfDeath;
        person.IsAlive = dto.IsAlive;

        await _db.SaveChangesAsync();
        return Ok(GenealogyService.ToDto(person));
    }

    /// <summary>
    /// DELETE /api/persons/{id} - remove a person and every relationship edge
    /// that touches them (so no dangling edges remain).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var removed = await _graph.DeletePersonAsync(id);
        return removed ? NoContent() : NotFound();
    }
}
