using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Classification;

/// <summary>
/// Classifies an issue into bug / feature / question / documentation using a
/// small weighted-vocabulary model: each category owns a dictionary of words
/// and phrases worth a certain number of points, the title's words count for
/// more than the body's, and whichever category accumulates the most points
/// wins. It's a deliberately lightweight stand-in for a bag-of-words model —
/// explainable and dependency-free, but a genuinely different mechanism from
/// counting regex pattern hits.
/// </summary>
public class LexiconClassifier
{
    private const double TitleWeightMultiplier = 2.0;

    private readonly List<CategoryLexicon> _lexicons = new()
    {
        new CategoryLexicon(
            category: "bug",
            wordWeights: new Dictionary<string, double>
            {
                ["crash"] = 3, ["crashes"] = 3, ["crashed"] = 3, ["crashing"] = 3,
                ["error"] = 2, ["errors"] = 2, ["exception"] = 3, ["broken"] = 2,
                ["fails"] = 2, ["failing"] = 2, ["failed"] = 2, ["freeze"] = 2,
                ["freezes"] = 2, ["hangs"] = 2, ["corrupt"] = 3, ["corrupted"] = 3,
                ["glitch"] = 1, ["regression"] = 2, ["bug"] = 3,
            },
            phraseWeights: new Dictionary<string, double>
            {
                ["stack trace"] = 4, ["does not work"] = 2, ["doesn't work"] = 2,
                ["not working"] = 2, ["memory leak"] = 4, ["out of memory"] = 3,
            }),

        new CategoryLexicon(
            category: "feature",
            wordWeights: new Dictionary<string, double>
            {
                ["feature"] = 2, ["enhancement"] = 2, ["proposal"] = 2, ["idea"] = 1,
                ["wish"] = 1, ["allow"] = 1, ["enable"] = 1, ["support"] = 1,
            },
            phraseWeights: new Dictionary<string, double>
            {
                ["feature request"] = 4, ["would be nice"] = 3, ["would be great"] = 3,
                ["would be useful"] = 3, ["it would be cool"] = 3, ["please add"] = 3,
                ["can we add"] = 2, ["new feature"] = 3, ["can we have"] = 2,
            }),

        new CategoryLexicon(
            category: "question",
            wordWeights: new Dictionary<string, double>
            {
                ["question"] = 3, ["confused"] = 1, ["unclear"] = 1, ["help"] = 1,
            },
            phraseWeights: new Dictionary<string, double>
            {
                ["how do i"] = 4, ["how can i"] = 4, ["is it possible"] = 4,
                ["does anyone know"] = 3, ["any idea why"] = 3, ["what is the"] = 2,
                ["could someone explain"] = 3,
            }),

        new CategoryLexicon(
            category: "documentation",
            wordWeights: new Dictionary<string, double>
            {
                ["docs"] = 3, ["documentation"] = 3, ["readme"] = 3, ["typo"] = 3,
                ["wording"] = 2, ["outdated"] = 2,
            },
            phraseWeights: new Dictionary<string, double>
            {
                ["missing example"] = 3, ["out of date"] = 2, ["broken link"] = 3,
                ["contributing.md"] = 2,
            }),
    };

    public ClassificationResult Classify(string title, string body)
    {
        var titleTokens = Tokenizer.Tokenize(title);
        var bodyTokens = Tokenizer.Tokenize(body);
        var normalizedTitle = Tokenizer.NormalizeForPhrases(title);
        var normalizedBody = Tokenizer.NormalizeForPhrases(body);

        var scores = new Dictionary<string, double>();
        foreach (var lexicon in _lexicons)
        {
            var titleScore = lexicon.Score(titleTokens, normalizedTitle) * TitleWeightMultiplier;
            var bodyScore = lexicon.Score(bodyTokens, normalizedBody);
            scores[lexicon.Category] = titleScore + bodyScore;
        }

        var total = scores.Values.Sum();
        if (total <= 0)
            return new ClassificationResult("question", 0.0, scores);

        var winner = scores.OrderByDescending(kv => kv.Value).First();
        var confidence = Math.Round(winner.Value / total, 2);
        return new ClassificationResult(winner.Key, confidence, scores);
    }
}
