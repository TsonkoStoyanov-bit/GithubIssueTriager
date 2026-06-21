namespace GithubIssueTriager.Api.Data;

/// <summary>
/// EF Core entity for one stored triage decision. Kept separate from
/// <see cref="GithubIssueTriager.Shared.Models.TriageRecord"/> (the DTO the
/// Api returns) so the persistence shape can evolve — e.g. the Labels column
/// — without that leaking into the public API contract.
/// </summary>
public class TriageHistoryEntity
{
    public long Id { get; set; }
    public string Repo { get; set; } = "";
    public int IssueNumber { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public double Confidence { get; set; }
    public string Priority { get; set; } = "";
    public int PriorityScore { get; set; }
    public List<string> Labels { get; set; } = new();
    public string NextStep { get; set; } = "";
    public string Source { get; set; } = "";
    public string TriagedAt { get; set; } = "";
}
