using GithubIssueTriager.Api.Priority.Heuristics;
using GithubIssueTriager.Shared.Models;
using Microsoft.Extensions.Options;

namespace GithubIssueTriager.Api.Priority;

/// <summary>
/// Aggregates every registered IPriorityHeuristic's vote into one score, then
/// maps that score onto low/medium/high/critical using thresholds pulled
/// live from TriageOptions (editable from the Settings page). Adding a new
/// signal means writing a new IPriorityHeuristic, not editing this class.
/// </summary>
public class PriorityEngine
{
    private readonly IReadOnlyList<IPriorityHeuristic> _heuristics;
    private readonly IOptionsMonitor<TriageOptions> _options;

    public PriorityEngine(IOptionsMonitor<TriageOptions> options)
    {
        _options = options;
        _heuristics = new List<IPriorityHeuristic>
        {
            new SeverityLexiconHeuristic(),
            new DiagnosticEvidenceHeuristic(),
            new CommunityEngagementHeuristic(),
            new RegressionSignalHeuristic(),
        };
    }

    public PriorityResult Estimate(string title, string body, int comments = 0)
    {
        var totalScore = 0.0;
        var reasons = new List<string>();

        foreach (var heuristic in _heuristics)
        {
            var vote = heuristic.Evaluate(title, body, comments);
            if (vote.Weight == 0 && vote.Reason is null) continue;

            totalScore += vote.Weight;
            if (vote.Reason is not null)
                reasons.Add(vote.Reason);
        }

        var roundedScore = (int)Math.Round(totalScore, MidpointRounding.AwayFromZero);
        var thresholds = _options.CurrentValue.PriorityThresholds;

        string priority;
        if (roundedScore >= thresholds.Critical) priority = "critical";
        else if (roundedScore >= thresholds.High) priority = "high";
        else if (roundedScore >= thresholds.Medium) priority = "medium";
        else
        {
            priority = "low";
            if (reasons.Count == 0)
                reasons.Add("no heuristic found a strong urgency signal");
        }

        return new PriorityResult(priority, roundedScore, reasons);
    }
}
