using FamilyTree.Api.Data;
using FamilyTree.Api.DTOs;
using FamilyTree.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // -> /api/relationships
public class RelationshipsController : ControllerBase
{
    private readonly FamilyTreeContext _db;
    private readonly GenealogyService _graph;

    public RelationshipsController(FamilyTreeContext db, GenealogyService graph)
    {
        _db = db;
        _graph = graph;
    }

    /// <summary>GET /api/relationships - every edge in the family graph.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RelationshipDto>>> GetAll()
    {
        var edges = await _db.Relationships.ToListAsync();
        return Ok(edges.Select(GenealogyService.ToDto));
    }

    /// <summary>
    /// POST /api/relationships - link two EXISTING people. This is how two
    /// members already in the tree get married (cousin marriage), or how a
    /// parent/child link is added, without ever duplicating a person.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RelationshipDto>> Create([FromBody] CreateRelationshipDto dto)
    {
        var (created, error) = await _graph.CreateRelationshipAsync(
            dto.Person1Id, dto.Person2Id, dto.RelationshipType, dto.IsAdoptive);

        if (error is not null) return BadRequest(error);
        return CreatedAtAction(nameof(GetAll), new { id = created!.Id }, created);
    }

    /// <summary>DELETE /api/relationships/{id} - unlink two people (remove one edge).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var removed = await _graph.DeleteRelationshipAsync(id);
        return removed ? NoContent() : NotFound();
    }
}
