namespace GithubIssueTriager.Shared.Models;

public record PriorityResult(
    string Priority,
    int Score,
    List<string> Reasons
);
