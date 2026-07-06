using FamilyTree.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Api.Data;

/// <summary>
/// EF Core context for the graph model: a table of <see cref="Person"/> nodes
/// and a table of <see cref="Relationship"/> edges. Cascade delete is disabled
/// so removing a person never silently destroys unrelated edges.
/// </summary>
public class FamilyTreeContext : DbContext
{
    public FamilyTreeContext(DbContextOptions<FamilyTreeContext> options) : base(options) { }

    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Relationship> Relationships => Set<Relationship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var rel = modelBuilder.Entity<Relationship>();

        rel.HasOne(r => r.Person1)
           .WithMany(p => p.RelationshipsAsPerson1)
           .HasForeignKey(r => r.Person1Id)
           .OnDelete(DeleteBehavior.Restrict);

        rel.HasOne(r => r.Person2)
           .WithMany(p => p.RelationshipsAsPerson2)
           .HasForeignKey(r => r.Person2Id)
           .OnDelete(DeleteBehavior.Restrict);

        // Store the enum as a readable string in SQLite ("ParentChild"/"Spouse").
        rel.Property(r => r.RelationshipType)
           .HasConversion<string>()
           .HasMaxLength(20);

        // Speeds up the graph lookups the API does constantly.
        rel.HasIndex(r => r.Person1Id);
        rel.HasIndex(r => r.Person2Id);
    }
}
