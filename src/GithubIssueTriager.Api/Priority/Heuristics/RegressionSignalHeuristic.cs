namespace GithubIssueTriager.Api.Priority.Heuristics;

/// <summary>
/// Flags issues explicitly described as something that used to work — a
/// regression usually deserves a closer look than a feature gap, since it
/// means existing users are newly broken rather than just missing something.
/// </summary>
public class RegressionSignalHeuristic : IPriorityHeuristic
{
    private static readonly string[] Phrases =
    {
        "used to work", "worked before", "since the last release",
        "since the latest update", "after upgrading", "regression",
    };

    public HeuristicVote Evaluate(string title, string body, int comments)
    {
        var lower = $"{title}\n{body}".ToLowerInvariant();
        var hit = Phrases.FirstOrDefault(phrase => lower.Contains(phrase, StringComparison.Ordinal));

        return hit is null
            ? new HeuristicVote(0, null)
            : new HeuristicVote(2, $"described as a regression (\"{hit}\")");
    }
}
