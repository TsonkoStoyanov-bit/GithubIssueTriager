using System.Text;

namespace GithubIssueTriager.Api.Classification;

/// <summary>Tiny hand-rolled tokenizer: lowercase words of 2+ letters, no regex.</summary>
public static class Tokenizer
{
    public static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                FlushToken(current, tokens);
            }
        }
        FlushToken(current, tokens);

        return tokens;
    }

    private static void FlushToken(StringBuilder current, List<string> tokens)
    {
        if (current.Length >= 2)
            tokens.Add(current.ToString());
        current.Clear();
    }

    /// <summary>Lowercased, whitespace-collapsed text, used for the lexicon's phrase lookups.</summary>
    public static string NormalizeForPhrases(string text)
    {
        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var ch in text.ToLowerInvariant())
        {
            var isSpaceLike = char.IsWhiteSpace(ch) || ch is '\n' or '\r' or '\t';
            if (isSpaceLike)
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }
}
