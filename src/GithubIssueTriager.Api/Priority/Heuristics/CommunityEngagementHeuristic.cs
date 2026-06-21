namespace GithubIssueTriager.Api.Priority.Heuristics;

/// <summary>
/// Treats comment count as a (rough) proxy for how many people are affected,
/// using three graduated tiers rather than one single threshold.
/// </summary>
public class CommunityEngagementHeuristic : IPriorityHeuristic
{
    public HeuristicVote Evaluate(string title, string body, int comments)
    {
        return comments switch
        {
            >= 20 => new HeuristicVote(3, $"very high community engagement ({comments} comments)"),
            >= 8 => new HeuristicVote(2, $"high community engagement ({comments} comments)"),
            >= 3 => new HeuristicVote(1, $"some community engagement ({comments} comments)"),
            _ => new HeuristicVote(0, null),
        };
    }
}
