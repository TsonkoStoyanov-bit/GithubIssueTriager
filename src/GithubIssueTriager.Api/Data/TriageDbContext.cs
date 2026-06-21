using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GithubIssueTriager.Api.Data;

/// <summary>
/// EF Core context for triage history. <see cref="Program"/> registers this
/// against PostgreSQL via Npgsql; tests register it against the EF Core
/// in-memory provider instead, so neither the unit tests nor the API
/// integration tests need a real Postgres instance running.
/// </summary>
public class TriageDbContext : DbContext
{
    public TriageDbContext(DbContextOptions<TriageDbContext> options) : base(options)
    {
    }

    public DbSet<TriageHistoryEntity> TriageHistory => Set<TriageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TriageHistoryEntity>(entity =>
        {
            entity.ToTable("triage_history");
            entity.HasKey(e => e.Id);

            // Stored as a JSON-encoded string column, same on-disk shape the
            // hand-written ADO.NET version used, just serialized/deserialized
            // automatically by EF Core instead of by hand in the store class.
            entity.Property(e => e.Labels)
                .HasConversion(
                    labels => JsonSerializer.Serialize(labels, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>());
        });
    }
}
