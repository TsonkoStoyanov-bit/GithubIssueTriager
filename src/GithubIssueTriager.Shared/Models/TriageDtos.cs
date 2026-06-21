namespace GithubIssueTriager.Shared.Models;

/// <summary>Request body for POST /api/triage/json.</summary>
public record TriageLocalRequest(string FileName, string? Repo);

/// <summary>Request body for POST /api/triage/github.</summary>
public record TriageGitHubRequest(string Owner, string Repo, int Number);

/// <summary>Request body for POST /api/triage/batch.</summary>
public record TriageBatchRequest(string? Repo);

/// <summary>Full triage outcome for one issue, as returned by the Api to the Web UI.</summary>
public record TriageResultDto(
    Issue Issue,
    ClassificationResult Classification,
    PriorityResult Priority,
    LabelResult Labels,
    bool AlreadyTriagedBefore
);

/// <summary>Generic error payload returned by the Api on failure (e.g. GitHub unreachable).</summary>
public record ApiErrorResponse(string Error, string? Hint);

/// <summary>
/// Settings payload exchanged with GET/PUT /api/settings (token is masked on read).
/// A mutable class (not a record) on purpose: the Blazor Settings page two-way
/// binds Fluent UI inputs directly to its properties, which requires settable
/// properties rather than init-only ones.
/// </summary>
public class TriageSettingsDto
{
    public string IssueSource { get; set; } = "Local";
    public string LocalJsonPath { get; set; } = "fixtures";
    public string GitHubOwner { get; set; } = "";
    public string GitHubRepo { get; set; } = "";
    public int GitHubIssueNumber { get; set; }
    public string GitHubToken { get; set; } = "";
    public int PriorityCritical { get; set; } = 8;
    public int PriorityHigh { get; set; } = 4;
    public int PriorityMedium { get; set; } = 1;
    public double LowConfidenceReviewThreshold { get; set; } = 0.4;
}
