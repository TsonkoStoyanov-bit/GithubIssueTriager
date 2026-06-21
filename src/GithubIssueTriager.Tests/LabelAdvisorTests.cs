using GithubIssueTriager.Api.Labels;
using GithubIssueTriager.Shared.Models;
using GithubIssueTriager.Tests.TestHelpers;
using Xunit;

namespace GithubIssueTriager.Tests;

public class LabelAdvisorTests
{
    private readonly LabelAdvisor _advisor =
        new(new TestOptionsMonitor<TriageOptions>(new TriageOptions()));

    [Fact]
    public void ConfidentBugClassification_GetsBugAndPriorityLabelsOnly()
    {
        var result = _advisor.Suggest("bug", "high", classificationConfidence: 0.9);

        Assert.Contains("bug", result.Labels);
        Assert.Contains("priority: high", result.Labels);
        Assert.DoesNotContain("needs-human-review", result.Labels);
    }

    [Fact]
    public void LowConfidenceClassification_AddsNeedsHumanReviewLabel()
    {
        // Mirrors the auth-bypass fixture: the engine is confident about
        // priority (critical) but not about category (low confidence).
        var result = _advisor.Suggest("question", "critical", classificationConfidence: 0.0);

        Assert.Contains("needs-human-review", result.Labels);
        Assert.Contains("Low classification confidence", result.NextStep);
    }

    [Fact]
    public void ConfidenceExactlyAtThreshold_DoesNotTriggerReview()
    {
        var defaultThreshold = new TriageOptions().LowConfidenceReviewThreshold;
        var result = _advisor.Suggest("feature", "low", classificationConfidence: defaultThreshold);

        Assert.DoesNotContain("needs-human-review", result.Labels);
    }

    [Fact]
    public void UnknownCategory_FallsBackToNeedsTriageLabel()
    {
        var result = _advisor.Suggest("unknown-category", "medium", classificationConfidence: 0.9);

        Assert.Contains("needs-triage", result.Labels);
    }

    [Fact]
    public void CustomReviewThreshold_IsRespected()
    {
        var lenientOptions = new TriageOptions { LowConfidenceReviewThreshold = 0.0 };
        var lenientAdvisor = new LabelAdvisor(new TestOptionsMonitor<TriageOptions>(lenientOptions));

        // Even a zero-confidence call should NOT get flagged once the
        // threshold itself is turned down to 0.0, proving the value comes
        // from configuration rather than being hardcoded.
        var result = lenientAdvisor.Suggest("question", "low", classificationConfidence: 0.0);

        Assert.DoesNotContain("needs-human-review", result.Labels);
    }
}
