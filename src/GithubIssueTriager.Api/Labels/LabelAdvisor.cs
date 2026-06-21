using GithubIssueTriager.Shared.Models;
using Microsoft.Extensions.Options;

namespace GithubIssueTriager.Api.Labels;

/// <summary>
/// Turns a classification + priority into the labels a maintainer would
/// actually apply, plus a short next-step recommendation. Unlike a plain
/// category-to-label lookup, this also factors in how *confident* the
/// classifier was: a low-confidence call gets an extra "needs-human-review"
/// label and a next step that says so, instead of presenting a guess as a
/// settled fact.
/// </summary>
public class LabelAdvisor
{
    private readonly IOptionsMonitor<TriageOptions> _options;

    public LabelAdvisor(IOptionsMonitor<TriageOptions> options)
    {
        _options = options;
    }

    private static readonly Dictionary<string, string> PrimaryLabel = new()
    {
        ["bug"] = "bug",
        ["feature"] = "enhancement",
        ["question"] = "question",
        ["documentation"] = "documentation",
    };

    private static readonly Dictionary<string, string> PriorityLabel = new()
    {
        ["critical"] = "priority: critical",
        ["high"] = "priority: high",
        ["medium"] = "priority: medium",
        ["low"] = "priority: low",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> NextStepAdvice = new()
    {
        ["bug"] = new()
        {
            ["critical"] = "Page whoever's on call — this looks like an active incident, not a backlog item.",
            ["high"] = "Get a maintainer to reproduce it this week and attach environment details.",
            ["medium"] = "File it with a clear repro and slot it into the next bugfix milestone.",
            ["low"] = "Worth fixing eventually; check for duplicates before it gets its own milestone slot.",
        },
        ["feature"] = new()
        {
            ["critical"] = "Surprisingly urgent for a feature ask — confirm with the reporter what's actually blocking them.",
            ["high"] = "Bring it to the next planning discussion; it sounds like it's blocking real usage.",
            ["medium"] = "Add to the roadmap backlog for prioritization against other requests.",
            ["low"] = "Let it collect reactions/votes before committing engineering time.",
        },
        ["question"] = new()
        {
            ["critical"] = "Respond directly and quickly — this reads like a support escalation, not a casual question.",
            ["high"] = "Answer soon, and consider whether it's really pointing at a missing feature or bug.",
            ["medium"] = "Point them at existing docs/discussions, or answer directly and close.",
            ["low"] = "Answer when convenient, or redirect to a community forum/discussion board.",
        },
        ["documentation"] = new()
        {
            ["critical"] = "Unusual for a docs issue — double-check this isn't a mislabeled bug report.",
            ["high"] = "Fix soon; wrong docs actively send people down the wrong path.",
            ["medium"] = "Good candidate to hand to a new contributor as a small, well-scoped PR.",
            ["low"] = "Batch it with the next docs cleanup pass.",
        },
    };

    public LabelResult Suggest(string category, string priority, double classificationConfidence)
    {
        var labels = new List<string> { PrimaryLabel.GetValueOrDefault(category, "needs-triage") };
        if (PriorityLabel.TryGetValue(priority, out var prioLabel))
            labels.Add(prioLabel);

        var nextStep = NextStepAdvice.TryGetValue(category, out var byPriority) && byPriority.TryGetValue(priority, out var advice)
            ? advice
            : "Classification was inconclusive — triage this one by hand.";

        var threshold = _options.CurrentValue.LowConfidenceReviewThreshold;
        if (classificationConfidence < threshold)
        {
            labels.Add("needs-human-review");
            nextStep = $"Low classification confidence ({classificationConfidence:0.00} < {threshold:0.00}) — " +
                       $"have a maintainer confirm the category before acting on: {nextStep}";
        }

        return new LabelResult(labels, nextStep);
    }
}
