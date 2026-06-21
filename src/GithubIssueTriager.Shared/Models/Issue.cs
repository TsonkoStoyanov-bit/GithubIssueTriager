namespace GithubIssueTriager.Shared.Models;

/// <summary>
/// Normalized issue shape used everywhere downstream of ingestion, regardless of
/// whether the issue came from the live GitHub REST API or a local mock JSON file.
/// </summary>
public record Issue(
    int Number,
    string Title,
    string Body,
    string Url,
    string Author,
    string State,
    List<string> Labels,
    int Comments,
    string CreatedAt,
    string Source // "github-api" or "mock-json"
);
