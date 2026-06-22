using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Services;

public interface ITriageStore
{
    void Init();
    long SaveDecision(string repo, Issue issue, ClassificationResult classification, PriorityResult priority, LabelResult labels);
    List<TriageRecord> ListAll(int limit = 50);
    List<TriageRecord> GetHistoryForIssue(string repo, int issueNumber);
    bool WasAlreadyTriaged(string repo, int issueNumber);

    /// <summary>Highest issue number triaged so far for <paramref name="repo"/>, or null if none.</summary>
    int? GetMaxIssueNumber(string repo);
}
