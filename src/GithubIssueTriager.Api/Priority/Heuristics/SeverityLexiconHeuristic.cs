using GithubIssueTriager.Api.Classification;

namespace GithubIssueTriager.Api.Priority.Heuristics;

/// <summary>
/// Scores urgency wording using the same weighted-lexicon mechanism as the
/// classifier, but with one combined vocabulary where severe terms carry
/// positive weight and "this is minor" terms carry negative weight directly
/// — rather than keeping two separate keyword lists and subtracting later.
/// </summary>
public class SeverityLexiconHeuristic : IPriorityHeuristic
{
    private static readonly CategoryLexicon Severity = new(
        category: "severity",
        wordWeights: new Dictionary<string, double>
        {
            ["security"] = 4, ["vulnerability"] = 4, ["exploit"] = 4, ["breach"] = 4,
            ["outage"] = 4, ["corrupted"] = 3, ["corrupt"] = 3, ["urgent"] = 2,
            ["asap"] = 2, ["blocking"] = 2, ["critical"] = 2, ["severe"] = 2,
            ["cosmetic"] = -2, ["minor"] = -2, ["nit"] = -1, ["typo"] = -1, ["trivial"] = -2,
        },
        phraseWeights: new Dictionary<string, double>
        {
            ["data loss"] = 5, ["all users"] = 3, ["production down"] = 5,
            ["cannot log in"] = 3, ["nice to have"] = -2,
        });

    public HeuristicVote Evaluate(string title, string body, int comments)
    {
        var text = $"{title}\n{body}";
        var tokens = Tokenizer.Tokenize(text);
        var normalized = Tokenizer.NormalizeForPhrases(text);

        var score = Severity.Score(tokens, normalized);
        if (score == 0) return new HeuristicVote(0, null);

        var direction = score > 0 ? "elevated" : "reduced";
        return new HeuristicVote(score, $"wording suggests {direction} severity (lexicon score {score:0.#})");
    }
}
