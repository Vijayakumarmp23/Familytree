using FamilyTree.Api.Models;

namespace FamilyTree.Api.DTOs;

/// <summary>Flat person shape returned by GET /persons and GET /persons/{id}.</summary>
public record PersonDto(
    int Id,
    string FullName,
    string Gender,
    DateTime? DateOfBirth,
    DateTime? DateOfDeath,
    bool IsAlive);

/// <summary>Lightweight reference to a related person (used inside PersonDetailDto).</summary>
public record PersonRefDto(int Id, string FullName, string Gender, bool IsAlive);

/// <summary>
/// Rich view for GET /persons/{id}. Parents / spouses / children / siblings are
/// all DERIVED from the relationship graph at request time - none of them are
/// stored on the person.
/// </summary>
public record PersonDetailDto(
    int Id,
    string FullName,
    string Gender,
    DateTime? DateOfBirth,
    DateTime? DateOfDeath,
    bool IsAlive,
    IReadOnlyList<PersonRefDto> Parents,
    IReadOnlyList<PersonRefDto> Spouses,
    IReadOnlyList<PersonRefDto> Children,
    IReadOnlyList<PersonRefDto> Siblings,
    bool IsAdopted);

/// <summary>An edge as returned by GET /relationships and POST /relationships.</summary>
public record RelationshipDto(
    int Id,
    int Person1Id,
    int Person2Id,
    string RelationshipType,
    bool IsAdoptive);

/// <summary>
/// Body for POST /persons. The core fields create the node; the optional
/// FatherId / MotherId / SpouseId are a convenience that make the API create
/// the matching relationship edges in the SAME request, so the UI can add and
/// link a person atomically without ever creating duplicates.
/// </summary>
public class CreatePersonDto
{
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public DateTime? DateOfDeath { get; set; }
    public bool IsAlive { get; set; } = true;

    public int? FatherId { get; set; }
    public int? MotherId { get; set; }
    public int? SpouseId { get; set; }

    /// <summary>When true, the father/mother links created here are adoptive.</summary>
    public bool ParentsAreAdoptive { get; set; }
}

/// <summary>Body for POST /relationships. RelationshipType is "ParentChild" or "Spouse".</summary>
public class CreateRelationshipDto
{
    public int Person1Id { get; set; }
    public int Person2Id { get; set; }
    public RelationshipType RelationshipType { get; set; }

    /// <summary>Set true (ParentChild only) to mark Person2 as an adopted child.</summary>
    public bool IsAdoptive { get; set; }
}
