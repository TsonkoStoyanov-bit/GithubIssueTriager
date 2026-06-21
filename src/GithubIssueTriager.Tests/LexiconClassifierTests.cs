using GithubIssueTriager.Api.Classification;
using Xunit;

namespace GithubIssueTriager.Tests;

public class LexiconClassifierTests
{
    private readonly LexiconClassifier _classifier = new();

    [Fact]
    public void MemoryLeakReport_IsClassifiedAsBug()
    {
        var result = _classifier.Classify(
            "Worker process gets OOM-killed after a few hours",
            "Heap snapshot shows a growing number of retained objects -- looks like a memory leak in the request pipeline.");

        Assert.Equal("bug", result.Category);
        Assert.True(result.Confidence > 0);
    }

    [Fact]
    public void NicelyWordedAsk_IsClassifiedAsFeature()
    {
        var result = _classifier.Classify(
            "Allow exporting dashboards as PDF",
            "It would be great to have a proper PDF export option, similar to the reports section.");

        Assert.Equal("feature", result.Category);
    }

    [Fact]
    public void HowToPhrasing_IsClassifiedAsQuestion()
    {
        var result = _classifier.Classify(
            "What is the recommended way to paginate large result sets?",
            "Is it possible to use cursor-based pagination instead of page numbers?");

        Assert.Equal("question", result.Category);
    }

    [Fact]
    public void BrokenDocsLink_IsClassifiedAsDocumentation()
    {
        var result = _classifier.Classify(
            "CONTRIBUTING.md links to a style guide that 404s",
            "The link is now a broken link since the docs reorg moved the page.");

        Assert.Equal("documentation", result.Category);
    }

    [Fact]
    public void TextWithNoLexiconHits_DefaultsToQuestionWithZeroConfidence()
    {
        // Deliberately avoids every word in every category's lexicon.
        var result = _classifier.Classify("Update dependency", "Bumping the build tool to a newer release.");

        Assert.Equal("question", result.Category);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void SophisticatedReportWithoutCommonBugWords_HasLowConfidence()
    {
        // The auth-bypass fixture deliberately reads like a real security
        // report without ever saying "bug", "crash", "error", etc. -- this
        // is the known limitation that LabelAdvisor's review-flag exists
        // to catch (see LabelAdvisorTests).
        var result = _classifier.Classify(
            "Auth middleware accepts expired JWTs under high request load",
            "We suspect a race condition in the token-expiry cache check. This is a security vulnerability " +
            "since a stolen-but-expired token can keep working intermittently.");

        Assert.True(result.Confidence < 0.4);
    }
}
