namespace GithubIssueTriager.Shared.Models;

/// <summary>
/// Bound from the "Triage" section of appsettings.json. Drives the configuration
/// form in the Web UI: where issues are read from (Local JSON vs the remote GitHub
/// API), GitHub auth, and the priority-scoring thresholds.
/// </summary>
public class TriageOptions
{
    public const string SectionName = "Triage";

    /// <summary>"Local" or "Remote".</summary>
    public string IssueSource { get; set; } = "Local";

    /// <summary>Folder of mock *.json issue fixtures, used when IssueSource == "Local".</summary>
    public string LocalJsonPath { get; set; } = "fixtures";

    public GitHubOptions GitHub { get; set; } = new();

    public PriorityThresholds PriorityThresholds { get; set; } = new();

    /// <summary>
    /// If the classifier's confidence falls below this (0.0-1.0), the label
    /// advisor adds a "needs-human-review" label instead of trusting the
    /// automatic category outright.
    /// </summary>
    public double LowConfidenceReviewThreshold { get; set; } = 0.4;
}

public class GitHubOptions
{
    public string Owner { get; set; } = "";
    public string Repo { get; set; } = "";
    public int IssueNumber { get; set; }

    /// <summary>Optional personal access token, for higher API rate limits on private repos.</summary>
    public string Token { get; set; } = "";
}

/// <summary>
/// Score thresholds that map the priority estimator's raw integer score onto a
/// low/medium/high/critical bucket. Configurable from the Settings page instead
/// of being hardcoded, so a maintainer can tune sensitivity per project.
/// </summary>
public class PriorityThresholds
{
    public int Critical { get; set; } = 8;
    public int High { get; set; } = 4;
    public int Medium { get; set; } = 1;
}
