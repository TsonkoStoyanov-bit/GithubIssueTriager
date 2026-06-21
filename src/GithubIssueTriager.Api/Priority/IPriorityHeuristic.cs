namespace GithubIssueTriager.Api.Priority;

/// <summary>A single named opinion about how urgent an issue is.</summary>
public record HeuristicVote(double Weight, string? Reason);

/// <summary>
/// One independent "voter" in the priority engine. Each heuristic looks at
/// the issue from one angle (wording, evidence quality, community reaction,
/// ...) and casts a weight contribution plus an optional human-readable
/// reason. The engine just sums every heuristic's vote — new signals can be
/// added by writing a new class, not by editing a big if/else chain.
/// </summary>
public interface IPriorityHeuristic
{
    HeuristicVote Evaluate(string title, string body, int comments);
}
