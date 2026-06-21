namespace GithubIssueTriager.Api.Classification;

/// <summary>
/// A scored vocabulary for one triage category: single-word weights plus a
/// handful of multi-word phrase bonuses. Scoring an issue against a lexicon
/// is just "sum the weight of every word/phrase that shows up" — closer to a
/// tiny bag-of-words model than to pattern matching.
/// </summary>
public class CategoryLexicon
{
    public string Category { get; }
    private readonly Dictionary<string, double> _wordWeights;
    private readonly Dictionary<string, double> _phraseWeights;

    public CategoryLexicon(string category, Dictionary<string, double> wordWeights, Dictionary<string, double>? phraseWeights = null)
    {
        Category = category;
        _wordWeights = wordWeights;
        _phraseWeights = phraseWeights ?? new Dictionary<string, double>();
    }

    public double Score(IReadOnlyCollection<string> tokens, string normalizedText)
    {
        var score = 0.0;

        foreach (var token in tokens)
        {
            if (_wordWeights.TryGetValue(token, out var weight))
                score += weight;
        }

        foreach (var (phrase, weight) in _phraseWeights)
        {
            if (normalizedText.Contains(phrase, StringComparison.Ordinal))
                score += weight;
        }

        return score;
    }
}
