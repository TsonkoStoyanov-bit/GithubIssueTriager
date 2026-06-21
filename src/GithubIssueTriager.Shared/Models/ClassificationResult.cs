namespace GithubIssueTriager.Shared.Models;

/// <summary>Scores is a per-category raw lexicon weight, not a hit count — see LexiconClassifier.</summary>
public record ClassificationResult(
    string Category,
    double Confidence,
    Dictionary<string, double> Scores
);
