namespace GithubIssueTriager.Api.Data;

/// <summary>
/// Single-row table holding the runtime-editable Triage settings (the same set
/// the Settings page edits). Storing them in the database makes the values a
/// single source of truth across Api instances and survive restarts — unlike
/// the earlier approach of rewriting appsettings.json on disk, which only works
/// for one instance and is lost when a container is recreated. appsettings.json
/// now only *seeds* this row the first time the table is empty.
/// </summary>
public class AppSettingsEntity
{
    // There is only ever one settings row; pin it to a fixed key.
    public int Id { get; set; } = 1;

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
