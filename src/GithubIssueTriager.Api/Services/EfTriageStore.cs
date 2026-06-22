using GithubIssueTriager.Api.Data;
using GithubIssueTriager.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GithubIssueTriager.Api.Services;

/// <summary>
/// EF Core-backed implementation of ITriageStore, replacing the earlier
/// hand-written ADO.NET SQLite/Postgres pair with a single DbContext-based
/// store. Persists every triage decision keyed by repo + issue number, so a
/// maintainer can review history and re-triaging the same issue is visible
/// rather than silently duplicated.
/// </summary>
public class EfTriageStore : ITriageStore
{
    private readonly TriageDbContext _db;

    public EfTriageStore(TriageDbContext db)
    {
        _db = db;
    }

    public void Init()
    {
        // EnsureCreated (rather than Migrations) keeps this self-contained for
        // a small exam project — no separate `dotnet ef migrations` step or
        // generated migration code to keep in sync by hand.
        _db.Database.EnsureCreated();
    }

    public long SaveDecision(string repo, Issue issue, ClassificationResult classification, PriorityResult priority, LabelResult labels)
    {
        var entity = new TriageHistoryEntity
        {
            Repo = repo,
            IssueNumber = issue.Number,
            Title = issue.Title,
            Category = classification.Category,
            Confidence = classification.Confidence,
            Priority = priority.Priority,
            PriorityScore = priority.Score,
            Labels = labels.Labels,
            NextStep = labels.NextStep,
            Source = issue.Source,
            TriagedAt = DateTimeOffset.UtcNow.ToString("o"),
        };

        _db.TriageHistory.Add(entity);
        _db.SaveChanges();
        return entity.Id;
    }

    public List<TriageRecord> ListAll(int limit = 50) =>
        // Materialize entities first (ToList), then map to the DTO in memory.
        // ToRecord builds a TriageRecord via its positional constructor, which
        // EF Core's SQL translator cannot turn into a server-side projection —
        // doing .Select(ToRecord) directly against IQueryable would throw at
        // runtime. Bringing rows into memory first sidesteps that entirely.
        _db.TriageHistory
            .OrderByDescending(e => e.TriagedAt)
            .Take(limit)
            .ToList()
            .Select(ToRecord)
            .ToList();

    public List<TriageRecord> GetHistoryForIssue(string repo, int issueNumber) =>
        _db.TriageHistory
            .Where(e => e.Repo == repo && e.IssueNumber == issueNumber)
            .OrderBy(e => e.TriagedAt)
            .ToList()
            .Select(ToRecord)
            .ToList();

    public bool WasAlreadyTriaged(string repo, int issueNumber) =>
        _db.TriageHistory.Any(e => e.Repo == repo && e.IssueNumber == issueNumber);

    // Cast to int? so Max() yields null (not an exception / 0) when the repo has
    // no history yet — lets the caller tell "no rows" apart from "issue #0".
    public int? GetMaxIssueNumber(string repo) =>
        _db.TriageHistory
            .Where(e => e.Repo == repo)
            .Max(e => (int?)e.IssueNumber);

    private static TriageRecord ToRecord(TriageHistoryEntity e) => new(
        Id: e.Id,
        Repo: e.Repo,
        IssueNumber: e.IssueNumber,
        Title: e.Title,
        Category: e.Category,
        Confidence: e.Confidence,
        Priority: e.Priority,
        PriorityScore: e.PriorityScore,
        Labels: e.Labels,
        NextStep: e.NextStep,
        Source: e.Source,
        TriagedAt: e.TriagedAt
    );
}
