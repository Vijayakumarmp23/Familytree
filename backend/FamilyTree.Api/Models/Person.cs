using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Api.Models;

/// <summary>
/// A single member of the family. In the graph model a Person carries NO
/// foreign keys to other people - every connection (parent/child, marriage)
/// lives in the <see cref="Relationship"/> table. This is what lets the same
/// person participate in many relationships (e.g. a cousin marriage) without
/// ever being duplicated.
/// </summary>
public class Person
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Gender { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    /// <summary>Null while the person is living.</summary>
    public DateTime? DateOfDeath { get; set; }

    /// <summary>Living status flag (LivingStatus in the spec).</summary>
    public bool IsAlive { get; set; } = true;

    // ---- Navigation: relationships this person appears in ----
    // (populated by EF via the two FKs on Relationship)
    public ICollection<Relationship> RelationshipsAsPerson1 { get; set; } = new List<Relationship>();
    public ICollection<Relationship> RelationshipsAsPerson2 { get; set; } = new List<Relationship>();
}
