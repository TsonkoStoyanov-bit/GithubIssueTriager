namespace GithubIssueTriager.Shared.Models;

public record LabelResult(
    List<string> Labels,
    string NextStep
);
