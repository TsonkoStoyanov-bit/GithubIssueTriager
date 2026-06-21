namespace GithubIssueTriager.Api.Priority.Heuristics;

/// <summary>
/// Rewards issues that bring real diagnostic evidence: a fenced code block,
/// explicit mention of logs/exceptions, or an HTTP-style 4xx/5xx status code.
/// More distinct kinds of evidence found => higher confidence the report is
/// actionable, not just a vague complaint.
/// </summary>
public class DiagnosticEvidenceHeuristic : IPriorityHeuristic
{
    public HeuristicVote Evaluate(string title, string body, int comments)
    {
        var text = $"{title}\n{body}";
        var lower = text.ToLowerInvariant();
        var evidenceFound = new List<string>();

        if (text.Contains("```", StringComparison.Ordinal))
            evidenceFound.Add("a fenced code block");

        if (lower.Contains("exception") || lower.Contains("traceback") || lower.Contains(" log ") || lower.EndsWith(" log"))
            evidenceFound.Add("an exception/log reference");

        if (ContainsHttpErrorStatusCode(lower))
            evidenceFound.Add("an HTTP error status code");

        if (evidenceFound.Count == 0)
            return new HeuristicVote(0, null);

        var weight = evidenceFound.Count; // 1, 2, or 3 distinct kinds of evidence
        return new HeuristicVote(weight, $"includes {string.Join(" and ", evidenceFound)}");
    }

    private static bool ContainsHttpErrorStatusCode(string lowerText)
    {
        foreach (var word in lowerText.Split(' ', '\n', '\t', ',', '.', '(', ')'))
        {
            if (word.Length == 3 && (word[0] == '4' || word[0] == '5') && word.All(char.IsDigit))
                return true;
        }
        return false;
    }
}
