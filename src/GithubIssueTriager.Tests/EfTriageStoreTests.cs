using GithubIssueTriager.Api.Data;
using GithubIssueTriager.Api.Services;
using GithubIssueTriager.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GithubIssueTriager.Tests;

/// <summary>
/// Exercises EfTriageStore against the EF Core in-memory provider — a fresh,
/// uniquely-named database per test instance, so tests never depend on a
/// real Postgres connection and can't bleed state into each other.
/// </summary>
public class EfTriageStoreTests : IDisposable
{
    private readonly TriageDbContext _db;
    private readonly EfTriageStore _store;

    public EfTriageStoreTests()
    {
        var options = new DbContextOptionsBuilder<TriageDbContext>()
            .UseInMemoryDatabase(databaseName: $"triager-tests-{Guid.NewGuid():N}")
            .Options;

        _db = new TriageDbContext(options);
        _store = new EfTriageStore(_db);
        _store.Init();
    }

    public void Dispose() => _db.Dispose();

    private static Issue SampleIssue(int number = 1) => new(
        Number: number, Title: "Sample issue", Body: "Body", Url: "https://example.com",
        Author: "tester", State: "open", Labels: new List<string>(), Comments: 0,
        CreatedAt: DateTimeOffset.UtcNow.ToString("o"), Source: "mock-json");

    private static (ClassificationResult, PriorityResult, LabelResult) SampleDecision() => (
        new ClassificationResult("bug", 1.0, new Dictionary<string, double> { ["bug"] = 3 }),
        new PriorityResult("high", 5, new List<string> { "test reason" }),
        new LabelResult(new List<string> { "bug", "priority: high" }, "Fix it.")
    );

    [Fact]
    public void SaveAndListAll_ReturnsSavedRecord()
    {
        var (classification, priority, labels) = SampleDecision();
        _store.SaveDecision("org/repo", SampleIssue(), classification, priority, labels);

        var all = _store.ListAll();

        Assert.Single(all);
        Assert.Equal("bug", all[0].Category);
    }

    [Fact]
    public void WasAlreadyTriaged_ReflectsSavedHistory()
    {
        Assert.False(_store.WasAlreadyTriaged("org/repo", 1));

        var (classification, priority, labels) = SampleDecision();
        _store.SaveDecision("org/repo", SampleIssue(1), classification, priority, labels);

        Assert.True(_store.WasAlreadyTriaged("org/repo", 1));
    }

    [Fact]
    public void History_IsScopedPerIssue()
    {
        var (classification, priority, labels) = SampleDecision();
        _store.SaveDecision("org/repo", SampleIssue(1), classification, priority, labels);
        _store.SaveDecision("org/repo", SampleIssue(2), classification, priority, labels);

        var historyForIssue1 = _store.GetHistoryForIssue("org/repo", 1);

        Assert.Single(historyForIssue1);
        Assert.Equal(1, historyForIssue1[0].IssueNumber);
    }

    [Fact]
    public void GetMaxIssueNumber_IsNull_WhenRepoHasNoHistory()
    {
        Assert.Null(_store.GetMaxIssueNumber("org/repo"));
    }

    [Fact]
    public void GetMaxIssueNumber_ReturnsHighest_ScopedToRepo()
    {
        var (classification, priority, labels) = SampleDecision();
        _store.SaveDecision("org/repo", SampleIssue(3), classification, priority, labels);
        _store.SaveDecision("org/repo", SampleIssue(7), classification, priority, labels);
        // A different repo's higher number must not leak into org/repo's max.
        _store.SaveDecision("other/repo", SampleIssue(99), classification, priority, labels);

        Assert.Equal(7, _store.GetMaxIssueNumber("org/repo"));
    }
}
