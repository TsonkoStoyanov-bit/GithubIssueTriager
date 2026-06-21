using GithubIssueTriager.Api.Services;
using Xunit;

namespace GithubIssueTriager.Tests;

public class GitHubIssueSourceTests
{
    private static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "fixtures");

    private static GitHubIssueSource CreateSource(HttpClient? httpClient = null) =>
        new(httpClient ?? new HttpClient(), Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubIssueSource>.Instance);

    [Fact]
    public async Task LoadFromJson_ParsesFixtureCorrectly()
    {
        var source = CreateSource();
        var path = Path.Combine(FixturesDir, "issue_201_memory_leak.json");

        var issue = await source.LoadFromJsonAsync(path);

        Assert.Equal(201, issue.Number);
        Assert.Contains("OOM-killed", issue.Title);
        Assert.Equal("mock-json", issue.Source);
        Assert.Equal(11, issue.Comments);
    }

    [Fact]
    public async Task LoadFromDirectory_LoadsAllFiveFixtures()
    {
        var source = CreateSource();

        var issues = await source.LoadFromDirectoryAsync(FixturesDir);

        Assert.Equal(5, issues.Count);
        Assert.Equal(new[] { 201, 202, 203, 204, 205 }, issues.Select(i => i.Number).OrderBy(n => n));
    }

    [Fact]
    public async Task FetchFromGitHub_UnreachableHost_ThrowsGitHubApiException()
    {
        // Point the HttpClient at a base address that cannot resolve, regardless
        // of whatever network access the host running the tests does or doesn't
        // have -- this keeps the test deterministic either way.
        var httpClient = new HttpClient { BaseAddress = new Uri("https://this-host-does-not-exist.invalid"), Timeout = TimeSpan.FromSeconds(5) };
        var source = CreateSource(httpClient);

        await Assert.ThrowsAsync<GitHubApiException>(
            () => source.FetchFromGitHubAsync("owner", "repo", 1, token: null));
    }
}
