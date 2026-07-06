using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Api.Models;

/// <summary>
/// A single edge in the family graph. Two people plus the kind of link.
///
///   ParentChild -> directed:  Person1 = parent, Person2 = child
///   Spouse      -> undirected: Person1 &amp; Person2 are married (order irrelevant)
///
/// Because relationships are their own records, a person can be the child of
/// one couple AND the spouse in another - which is exactly how cousin marriages
/// and reconnecting branches are represented without duplicate people.
/// </summary>
public class Relationship
{
    public int Id { get; set; }

    [Required]
    public int Person1Id { get; set; }

    [Required]
    public int Person2Id { get; set; }

    [Required]
    public RelationshipType RelationshipType { get; set; }

    // ---- Navigation ----
    public Person? Person1 { get; set; }
    public Person? Person2 { get; set; }
}
