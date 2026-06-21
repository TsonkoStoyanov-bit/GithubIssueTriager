using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Services;

/// <summary>
/// Reads the current Triage configuration and writes updates from the Settings
/// page back into appsettings.json on disk. Because the host's configuration
/// was built with reloadOnChange: true (the default for WebApplication.
/// CreateBuilder), writing the file triggers ASP.NET Core's own file watcher,
/// so IOptionsMonitor&lt;TriageOptions&gt; consumers pick up the change
/// automatically on their next read — no app restart needed.
/// </summary>
public class SettingsService
{
    private readonly string _appSettingsPath;
    private readonly IOptionsMonitor<TriageOptions> _options;

    public SettingsService(IWebHostEnvironment env, IOptionsMonitor<TriageOptions> options)
    {
        _appSettingsPath = Path.Combine(env.ContentRootPath, "appsettings.json");
        _options = options;
    }

    public TriageSettingsDto GetCurrent()
    {
        var o = _options.CurrentValue;
        return new TriageSettingsDto
        {
            IssueSource = o.IssueSource,
            LocalJsonPath = o.LocalJsonPath,
            GitHubOwner = o.GitHub.Owner,
            GitHubRepo = o.GitHub.Repo,
            GitHubIssueNumber = o.GitHub.IssueNumber,
            GitHubToken = MaskToken(o.GitHub.Token),
            PriorityCritical = o.PriorityThresholds.Critical,
            PriorityHigh = o.PriorityThresholds.High,
            PriorityMedium = o.PriorityThresholds.Medium,
            LowConfidenceReviewThreshold = o.LowConfidenceReviewThreshold,
        };
    }

    public async Task SaveAsync(TriageSettingsDto dto, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(_appSettingsPath, ct);
        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidOperationException("appsettings.json did not parse as a JSON object");

        // Keep the existing token unless the caller actually provided a new,
        // unmasked one (the GET endpoint never returns the real token).
        var existingToken = _options.CurrentValue.GitHub.Token;
        var tokenToStore = dto.GitHubToken == MaskToken(existingToken) ? existingToken : dto.GitHubToken;

        var triageNode = new JsonObject
        {
            ["IssueSource"] = dto.IssueSource,
            ["LocalJsonPath"] = dto.LocalJsonPath,
            ["GitHub"] = new JsonObject
            {
                ["Owner"] = dto.GitHubOwner,
                ["Repo"] = dto.GitHubRepo,
                ["IssueNumber"] = dto.GitHubIssueNumber,
                ["Token"] = tokenToStore,
            },
            ["PriorityThresholds"] = new JsonObject
            {
                ["Critical"] = dto.PriorityCritical,
                ["High"] = dto.PriorityHigh,
                ["Medium"] = dto.PriorityMedium,
            },
            ["LowConfidenceReviewThreshold"] = dto.LowConfidenceReviewThreshold,
        };

        root[TriageOptions.SectionName] = triageNode;

        var updatedJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_appSettingsPath, updatedJson, ct);
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return "";
        return token.Length <= 4 ? "****" : $"****{token[^4..]}";
    }
}
