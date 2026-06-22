using GithubIssueTriager.Api.Classification;
using GithubIssueTriager.Api.Labels;
using GithubIssueTriager.Api.Priority;
using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Services;

/// <summary>
/// Wires ingestion -> classification -> priority estimation -> label advice
/// -> storage together. The Api's minimal-API endpoints are thin wrappers
/// around this class; it is also what the xUnit tests exercise directly.
/// </summary>
public class TriageOrchestrator
{
    private readonly IIssueSource _issueSource;
    private readonly LexiconClassifier _classifier;
    private readonly PriorityEngine _priorityEngine;
    private readonly LabelAdvisor _labelAdvisor;
    private readonly ITriageStore _store;

    public TriageOrchestrator(
        IIssueSource issueSource,
        LexiconClassifier classifier,
        PriorityEngine priorityEngine,
        LabelAdvisor labelAdvisor,
        ITriageStore store)
    {
        _issueSource = issueSource;
        _classifier = classifier;
        _priorityEngine = priorityEngine;
        _labelAdvisor = labelAdvisor;
        _store = store;
        _store.Init();
    }

    public TriageResultDto TriageIssue(Issue issue, string repoLabel)
    {
        var classification = _classifier.Classify(issue.Title, issue.Body);
        var priority = _priorityEngine.Estimate(issue.Title, issue.Body, issue.Comments);
        var labels = _labelAdvisor.Suggest(classification.Category, priority.Priority, classification.Confidence);

        var alreadyTriaged = _store.WasAlreadyTriaged(repoLabel, issue.Number);
        _store.SaveDecision(repoLabel, issue, classification, priority, labels);

        return new TriageResultDto(issue, classification, priority, labels, alreadyTriaged);
    }

    public async Task<TriageResultDto> TriageFromJsonAsync(string filePath, string repoLabel, CancellationToken ct = default)
    {
        var issue = await _issueSource.LoadFromJsonAsync(filePath, ct);
        return TriageIssue(issue, repoLabel);
    }

    public async Task<TriageResultDto> TriageFromGitHubAsync(string owner, string repo, int number, string? token, CancellationToken ct = default)
    {
        var issue = await _issueSource.FetchFromGitHubAsync(owner, repo, number, token, ct);
        return TriageIssue(issue, $"{owner}/{repo}");
    }

    public async Task<List<TriageResultDto>> TriageDirectoryAsync(string directoryPath, string repoLabel, CancellationToken ct = default)
    {
        var issues = await _issueSource.LoadFromDirectoryAsync(directoryPath, ct);
        return issues.Select(issue => TriageIssue(issue, repoLabel)).ToList();
    }

    public List<TriageRecord> GetHistory(int limit = 50) => _store.ListAll(limit);

    /// <summary>
    /// Suggests the next issue number to triage for a repo: one past the highest
    /// number already triaged, or 1 when the repo has no history yet. Lets the UI
    /// pre-fill the field from stored history instead of a hand-set default.
    /// </summary>
    public int GetNextIssueNumber(string repo) => (_store.GetMaxIssueNumber(repo) ?? 0) + 1;

    public IReadOnlyList<string> ListFixtures(string directoryPath) => _issueSource.ListFixtureFiles(directoryPath);
}
