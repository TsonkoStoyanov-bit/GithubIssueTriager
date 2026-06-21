using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Services;

/// <summary>
/// Issue ingestion: fetches a real issue from the GitHub REST API via HttpClient,
/// or loads one from a local mock JSON fixture. Both paths normalize to the same
/// Issue record so the rest of the pipeline never needs to know the origin.
/// </summary>
public class GitHubIssueSource : IIssueSource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubIssueSource> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GitHubIssueSource(HttpClient httpClient, ILogger<GitHubIssueSource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri("https://api.github.com");

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("mini-issue-triager", "1.0"));

        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<Issue> FetchFromGitHubAsync(string owner, string repo, int number, string? token, CancellationToken ct = default)
    {
        var requestUri = $"/repos/{owner}/{repo}/issues/{number}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Live GitHub API call failed for {Uri}", requestUri);
            throw new GitHubApiException($"could not reach GitHub API ({requestUri}): {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubApiException($"GitHub API returned HTTP {(int)response.StatusCode} for {requestUri}");
        }

        var raw = await response.Content.ReadFromJsonAsync<RawGitHubIssue>(JsonOptions, ct)
                  ?? throw new GitHubApiException($"GitHub API returned an empty response for {requestUri}");

        return Normalize(raw, source: "github-api");
    }

    public async Task<Issue> LoadFromJsonAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var raw = await JsonSerializer.DeserializeAsync<RawGitHubIssue>(stream, JsonOptions, ct)
                  ?? throw new InvalidDataException($"could not parse issue JSON: {filePath}");
        return Normalize(raw, source: "mock-json");
    }

    public async Task<List<Issue>> LoadFromDirectoryAsync(string directoryPath, CancellationToken ct = default)
    {
        var issues = new List<Issue>();
        foreach (var file in ListFixtureFiles(directoryPath))
        {
            issues.Add(await LoadFromJsonAsync(Path.Combine(directoryPath, file), ct));
        }
        return issues;
    }

    public IReadOnlyList<string> ListFixtureFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return Array.Empty<string>();

        return Directory.GetFiles(directoryPath, "*.json")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static Issue Normalize(RawGitHubIssue raw, string source) => new(
        Number: raw.Number,
        Title: raw.Title ?? "",
        Body: raw.Body ?? "",
        Url: raw.HtmlUrl ?? raw.Url ?? "",
        Author: raw.User?.Login ?? "unknown",
        State: raw.State ?? "open",
        Labels: raw.Labels?.Select(l => l.Name ?? "").ToList() ?? new List<string>(),
        Comments: raw.Comments,
        CreatedAt: raw.CreatedAt ?? "",
        Source: source
    );

    // Shape mirrors the real GitHub REST API "issue" object closely enough for our needs.
    private sealed class RawGitHubIssue
    {
        public int Number { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
        public string? Url { get; set; }
        public RawUser? User { get; set; }
        public string? State { get; set; }
        public List<RawLabel>? Labels { get; set; }
        public int Comments { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }

    private sealed class RawUser
    {
        public string? Login { get; set; }
    }

    private sealed class RawLabel
    {
        public string? Name { get; set; }
    }
}
