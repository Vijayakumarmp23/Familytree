using FamilyTree.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Api.Data;

/// <summary>
/// Seeds a four-generation family that deliberately contains a COUSIN MARRIAGE
/// so the graph renderer can be exercised on a reconnecting-branch case:
///
///   Gen 1                  George Smith  x  Margaret Smith
///                                          |
///              +---------------------------+---------------------------+
///              |                                                       |
///   Gen 2   John Smith x Linda (in)                       Susan Smith x Robert Brown (in)
///              |                                                       |
///        +-----+------+                                         +------+------+
///        |            |                                         |             |
///   Gen 3 Michael   Emily                                     David        Sarah
///           \                                                              /
///            \____________________ MARRIED ___________________________ /
///                                     |                (first cousins)
///   Gen 4                          Oliver Smith
///
/// Michael (grandchild via John) marries Sarah (grandchild via Susan): the two
/// branches reconnect, yet every person is still a single node.
/// </summary>
public static class SeedData
{
    public static async Task InitializeAsync(FamilyTreeContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // Additive migration for databases created before adoption support:
        // EnsureCreated won't alter an existing table, so add the column here.
        // Throws "duplicate column" on already-migrated / fresh DBs -> ignore.
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Relationships ADD COLUMN IsAdoptive INTEGER NOT NULL DEFAULT 0;");
        }
        catch { /* column already exists */ }

        if (await db.Persons.AnyAsync()) return; // already seeded

        // ---------- People (nodes) ----------
        var george   = P("George Smith",   "Male",   1940, 3, 12, deathYear: 2015);
        var margaret = P("Margaret Smith", "Female", 1942, 7,  5, deathYear: 2018);

        var john   = P("John Smith",   "Male",   1965, 1, 20);
        var linda  = P("Linda Smith",  "Female", 1967, 4, 18);   // married in
        var susan  = P("Susan Smith",  "Female", 1968, 9, 30);
        var robert = P("Robert Brown", "Male",   1966, 11, 2);   // married in

        var michael = P("Michael Smith", "Male",   1990, 6, 14);
        var emily   = P("Emily Smith",   "Female", 1992, 12, 1);
        var david   = P("David Brown",   "Male",   1994, 8, 22);
        var sarah   = P("Sarah Brown",   "Female", 1996, 2, 9);

        var oliver  = P("Oliver Smith",  "Male",   2020, 5, 3);

        db.Persons.AddRange(george, margaret, john, linda, susan, robert,
                            michael, emily, david, sarah, oliver);
        await db.SaveChangesAsync(); // Ids assigned

        // ---------- Relationships (edges) ----------
        // Marriages (Spouse - undirected)
        Marry(db, george, margaret);
        Marry(db, john, linda);
        Marry(db, susan, robert);
        Marry(db, michael, sarah); // <-- the cousin marriage

        // Parent -> child (ParentChild - directed)
        Parents(db, george, margaret, john);
        Parents(db, george, margaret, susan);

        Parents(db, john, linda, michael);
        Parents(db, john, linda, emily);

        Parents(db, susan, robert, david);
        Parents(db, susan, robert, sarah);

        Parents(db, michael, sarah, oliver); // child of the cousin marriage

        await db.SaveChangesAsync();
    }

    private static Person P(string name, string gender, int y, int m, int d, int? deathYear = null) => new()
    {
        FullName = name,
        Gender = gender,
        DateOfBirth = new DateTime(y, m, d),
        DateOfDeath = deathYear is int dy ? new DateTime(dy, 1, 1) : null,
        IsAlive = deathYear is null
    };

    private static void Marry(FamilyTreeContext db, Person a, Person b) =>
        db.Relationships.Add(new Relationship
        {
            Person1Id = a.Id,
            Person2Id = b.Id,
            RelationshipType = RelationshipType.Spouse
        });

    private static void Parents(FamilyTreeContext db, Person father, Person mother, Person child)
    {
        db.Relationships.Add(new Relationship { Person1Id = father.Id, Person2Id = child.Id, RelationshipType = RelationshipType.ParentChild });
        db.Relationships.Add(new Relationship { Person1Id = mother.Id, Person2Id = child.Id, RelationshipType = RelationshipType.ParentChild });
    }
}
