using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Services;

/// <summary>Thrown when the live GitHub REST API can't be reached or returns an error.</summary>
public class GitHubApiException : Exception
{
    public GitHubApiException(string message) : base(message) { }
    public GitHubApiException(string message, Exception inner) : base(message, inner) { }
}

public interface IIssueSource
{
    Task<Issue> FetchFromGitHubAsync(string owner, string repo, int number, string? token, CancellationToken ct = default);
    Task<Issue> LoadFromJsonAsync(string filePath, CancellationToken ct = default);
    Task<List<Issue>> LoadFromDirectoryAsync(string directoryPath, CancellationToken ct = default);
    IReadOnlyList<string> ListFixtureFiles(string directoryPath);
}
