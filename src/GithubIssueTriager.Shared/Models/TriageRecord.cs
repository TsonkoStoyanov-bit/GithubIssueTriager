namespace GithubIssueTriager.Shared.Models;

/// <summary>One stored row of triage history, as returned by the Api.</summary>
public record TriageRecord(
    long Id,
    string Repo,
    int IssueNumber,
    string Title,
    string Category,
    double Confidence,
    string Priority,
    int PriorityScore,
    List<string> Labels,
    string NextStep,
    string Source,
    string TriagedAt
);
