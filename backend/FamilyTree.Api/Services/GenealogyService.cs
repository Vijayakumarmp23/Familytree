using FamilyTree.Api.Data;
using FamilyTree.Api.DTOs;
using FamilyTree.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Api.Services;

/// <summary>
/// All the graph reasoning the API needs. Nothing here mutates the schema of a
/// person - relatives are always DERIVED by walking the Relationship edges, so
/// a person who appears in many relationships (cousin marriage, reconnecting
/// branches) is still a single record.
/// </summary>
public class GenealogyService
{
    private readonly FamilyTreeContext _db;

    public GenealogyService(FamilyTreeContext db) => _db = db;

    /// <summary>Build the rich detail view (parents/spouses/children/siblings) for one person.</summary>
    public async Task<PersonDetailDto?> GetDetailAsync(int id)
    {
        var person = await _db.Persons.FindAsync(id);
        if (person is null) return null;

        // Pull every edge touching this person in one round-trip.
        var edges = await _db.Relationships
            .Where(r => r.Person1Id == id || r.Person2Id == id)
            .ToListAsync();

        // Parents: edges where this person is the CHILD (Person2) of a ParentChild.
        var parentIds = edges
            .Where(r => r.RelationshipType == RelationshipType.ParentChild && r.Person2Id == id)
            .Select(r => r.Person1Id)
            .ToHashSet();

        // Children: edges where this person is the PARENT (Person1).
        var childIds = edges
            .Where(r => r.RelationshipType == RelationshipType.ParentChild && r.Person1Id == id)
            .Select(r => r.Person2Id)
            .ToHashSet();

        // Spouses: undirected, so grab whichever side isn't us.
        var spouseIds = edges
            .Where(r => r.RelationshipType == RelationshipType.Spouse)
            .Select(r => r.Person1Id == id ? r.Person2Id : r.Person1Id)
            .ToHashSet();

        // Siblings: anyone who shares at least one parent with us (minus ourselves).
        var siblingIds = new HashSet<int>();
        if (parentIds.Count > 0)
        {
            siblingIds = (await _db.Relationships
                    .Where(r => r.RelationshipType == RelationshipType.ParentChild
                                && parentIds.Contains(r.Person1Id))
                    .Select(r => r.Person2Id)
                    .ToListAsync())
                .ToHashSet();
            siblingIds.Remove(id);
        }

        var neededIds = parentIds.Union(childIds).Union(spouseIds).Union(siblingIds).ToList();
        var people = await _db.Persons
            .Where(p => neededIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        List<PersonRefDto> Refs(IEnumerable<int> ids) => ids
            .Where(people.ContainsKey)
            .Select(i => people[i])
            .OrderBy(p => p.DateOfBirth ?? DateTime.MaxValue)
            .Select(p => new PersonRefDto(p.Id, p.FullName, p.Gender, p.IsAlive))
            .ToList();

        return new PersonDetailDto(
            person.Id, person.FullName, person.Gender,
            person.DateOfBirth, person.DateOfDeath, person.IsAlive,
            Parents: Refs(parentIds),
            Spouses: Refs(spouseIds),
            Children: Refs(childIds),
            Siblings: Refs(siblingIds));
    }

    /// <summary>
    /// Create an edge, but only after validating both people exist, the edge is
    /// not a self-loop, and an equivalent edge doesn't already exist. Spouse
    /// edges are treated as undirected for the duplicate check.
    /// Returns (dto, error) - exactly one is non-null.
    /// </summary>
    public async Task<(RelationshipDto? dto, string? error)> CreateRelationshipAsync(
        int person1Id, int person2Id, RelationshipType type)
    {
        if (person1Id == person2Id)
            return (null, "A person cannot have a relationship with themselves.");

        var p1 = await _db.Persons.FindAsync(person1Id);
        var p2 = await _db.Persons.FindAsync(person2Id);
        if (p1 is null || p2 is null)
            return (null, "Both Person1Id and Person2Id must reference existing people.");

        if (await EdgeExistsAsync(person1Id, person2Id, type))
            return (null, "That relationship already exists.");

        var rel = new Relationship
        {
            Person1Id = person1Id,
            Person2Id = person2Id,
            RelationshipType = type
        };
        _db.Relationships.Add(rel);
        await _db.SaveChangesAsync();

        return (ToDto(rel), null);
    }

    /// <summary>Duplicate guard. Spouse edges are order-independent; ParentChild is directed.</summary>
    public async Task<bool> EdgeExistsAsync(int person1Id, int person2Id, RelationshipType type)
    {
        if (type == RelationshipType.Spouse)
        {
            return await _db.Relationships.AnyAsync(r =>
                r.RelationshipType == RelationshipType.Spouse &&
                ((r.Person1Id == person1Id && r.Person2Id == person2Id) ||
                 (r.Person1Id == person2Id && r.Person2Id == person1Id)));
        }

        return await _db.Relationships.AnyAsync(r =>
            r.RelationshipType == RelationshipType.ParentChild &&
            r.Person1Id == person1Id && r.Person2Id == person2Id);
    }

    public static RelationshipDto ToDto(Relationship r) =>
        new(r.Id, r.Person1Id, r.Person2Id, r.RelationshipType.ToString());

    public static PersonDto ToDto(Person p) =>
        new(p.Id, p.FullName, p.Gender, p.DateOfBirth, p.DateOfDeath, p.IsAlive);
}
