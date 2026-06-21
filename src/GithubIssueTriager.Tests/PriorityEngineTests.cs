using GithubIssueTriager.Api.Priority;
using GithubIssueTriager.Shared.Models;
using GithubIssueTriager.Tests.TestHelpers;
using Xunit;

namespace GithubIssueTriager.Tests;

public class PriorityEngineTests
{
    private readonly PriorityEngine _engine =
        new(new TestOptionsMonitor<TriageOptions>(new TriageOptions()));

    [Fact]
    public void SecurityVulnerability_ScoresAsCritical()
    {
        var result = _engine.Estimate(
            "Auth middleware accepts expired JWTs under high request load",
            "This is a security vulnerability that affects all users.",
            comments: 14);

        Assert.Equal("critical", result.Priority);
    }

    [Fact]
    public void CosmeticWording_ScoresAsLow()
    {
        var result = _engine.Estimate("Minor wording nit", "This is a cosmetic, trivial typo, nothing urgent.");

        Assert.Equal("low", result.Priority);
    }

    [Fact]
    public void FencedCodeBlockPlusLogReference_OutscoresPlainText()
    {
        var withEvidence = _engine.Estimate("Crash on startup", "```\nexception in initializer\n```");
        var withoutEvidence = _engine.Estimate("Crash on startup", "It just doesn't start, no details.");

        Assert.True(withEvidence.Score > withoutEvidence.Score);
    }

    [Fact]
    public void HighCommentCount_OutscoresLowCommentCount()
    {
        var quiet = _engine.Estimate("Something is off", "Not sure why.", comments: 1);
        var busy = _engine.Estimate("Something is off", "Not sure why.", comments: 25);

        Assert.True(busy.Score > quiet.Score);
    }

    [Fact]
    public void RegressionPhrase_AddsAReason()
    {
        var result = _engine.Estimate("It broke", "This used to work before the last release.");

        Assert.Contains(result.Reasons, r => r.Contains("regression"));
    }

    [Fact]
    public void CustomThresholds_ChangeTheBucketForTheSameScore()
    {
        var strictOptions = new TriageOptions
        {
            PriorityThresholds = new PriorityThresholds { Critical = 100, High = 50, Medium = 25 }
        };
        var strictEngine = new PriorityEngine(new TestOptionsMonitor<TriageOptions>(strictOptions));

        var result = strictEngine.Estimate(
            "Auth middleware accepts expired JWTs",
            "This is a security vulnerability that affects all users.",
            comments: 14);

        Assert.NotEqual("critical", result.Priority);
    }
}
