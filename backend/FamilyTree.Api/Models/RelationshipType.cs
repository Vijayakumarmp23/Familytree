namespace FamilyTree.Api.Models;

/// <summary>
/// The only two edge kinds the genealogy graph needs. Everything else
/// (siblings, cousins, grandparents, aunts/uncles) is DERIVED from these by
/// traversing the graph - it is never stored.
/// </summary>
public enum RelationshipType
{
    /// <summary>Person1 is a parent of Person2 (directed edge parent -&gt; child).</summary>
    ParentChild,

    /// <summary>Person1 and Person2 are married/partners (undirected edge).</summary>
    Spouse
}
