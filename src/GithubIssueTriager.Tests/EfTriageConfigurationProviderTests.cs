using GithubIssueTriager.Api.Configuration;
using GithubIssueTriager.Api.Data;
using GithubIssueTriager.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GithubIssueTriager.Tests;

/// <summary>
/// Exercises the database-backed configuration source against the EF Core
/// in-memory provider, so the seed-on-empty and existing-row-wins behaviour is
/// covered without needing a real Postgres instance.
/// </summary>
public class EfTriageConfigurationProviderTests
{
    private static TriageOptions SampleSeed() => new()
    {
        IssueSource = "Remote",
        LocalJsonPath = "fixtures",
        GitHub = new GitHubOptions { Owner = "seed-owner", Repo = "seed-repo", IssueNumber = 7, Token = "seed-token" },
        PriorityThresholds = new PriorityThresholds { Critical = 9, High = 5, Medium = 2 },
        LowConfidenceReviewThreshold = 0.33,
    };

    private static DbContextOptions<TriageDbContext> InMemory(string name) =>
        new DbContextOptionsBuilder<TriageDbContext>().UseInMemoryDatabase(name).Options;

    private static EfTriageConfigurationProvider BuildProvider(string dbName, TriageOptions seed)
    {
        var source = new EfTriageConfigurationSource(opt => opt.UseInMemoryDatabase(dbName), seed);
        return (EfTriageConfigurationProvider)source.Build(new ConfigurationBuilder());
    }

    [Fact]
    public void Load_SeedsTheTable_WhenEmpty_AndSurfacesSeedValues()
    {
        var dbName = $"cfg-seed-{Guid.NewGuid():N}";
        var provider = BuildProvider(dbName, SampleSeed());

        provider.Load();

        Assert.True(provider.TryGet("Triage:IssueSource", out var src));
        Assert.Equal("Remote", src);
        Assert.True(provider.TryGet("Triage:GitHub:Owner", out var owner));
        Assert.Equal("seed-owner", owner);
        Assert.True(provider.TryGet("Triage:PriorityThresholds:Critical", out var crit));
        Assert.Equal("9", crit);
        Assert.True(provider.TryGet("Triage:LowConfidenceReviewThreshold", out var thr));
        Assert.Equal("0.33", thr);

        // The seed should have been written so it persists as the source of truth.
        using var db = new TriageDbContext(InMemory(dbName));
        Assert.Equal(1, db.AppSettings.Count());
    }

    [Fact]
    public void Load_UsesExistingRow_NotTheSeed_WhenTableAlreadyHasData()
    {
        var dbName = $"cfg-existing-{Guid.NewGuid():N}";

        // Pre-populate a row that differs from the seed (in-memory DBs are shared by name).
        using (var db = new TriageDbContext(InMemory(dbName)))
        {
            db.Database.EnsureCreated();
            db.AppSettings.Add(new AppSettingsEntity { Id = 1, GitHubOwner = "db-owner", IssueSource = "Local" });
            db.SaveChanges();
        }

        var provider = BuildProvider(dbName, SampleSeed());
        provider.Load();

        provider.TryGet("Triage:GitHub:Owner", out var owner);
        Assert.Equal("db-owner", owner);   // the stored row, not "seed-owner"
        provider.TryGet("Triage:IssueSource", out var src);
        Assert.Equal("Local", src);
    }
}
