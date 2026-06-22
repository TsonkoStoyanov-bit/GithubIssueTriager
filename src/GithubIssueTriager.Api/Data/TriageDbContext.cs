using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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

    /// <summary>Single-row table backing the editable Triage settings.</summary>
    public DbSet<AppSettingsEntity> AppSettings => Set<AppSettingsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSettingsEntity>(entity =>
        {
            entity.ToTable("app_settings");
            entity.HasKey(e => e.Id);
            // Never auto-generate the key — we manage the single row with a fixed Id.
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TriageHistoryEntity>(entity =>
        {
            entity.ToTable("triage_history");
            entity.HasKey(e => e.Id);

            // Stored as a JSON-encoded string column, same on-disk shape the
            // hand-written ADO.NET version used, just serialized/deserialized
            // automatically by EF Core instead of by hand in the store class.
            // The explicit ValueComparer tells EF how to detect changes and
            // snapshot this collection correctly -- without it, EF logs a
            // model-validation warning and falls back to reference equality,
            // which can't tell two structurally-identical lists apart.
            entity.Property(e => e.Labels)
                .HasConversion(
                    labels => JsonSerializer.Serialize(labels, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
                        labels => labels.Aggregate(0, (hash, label) => HashCode.Combine(hash, label.GetHashCode())),
                        labels => labels.ToList()));
        });
    }
}
