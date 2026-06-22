using GithubIssueTriager.Api.Data;
using GithubIssueTriager.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GithubIssueTriager.Api.Services;

/// <summary>
/// Reads the current Triage configuration and persists updates from the Settings
/// page to the database (the single <c>app_settings</c> row). The values reach
/// the rest of the app through <see cref="EfTriageConfigurationSource"/>, which
/// layers the DB row into <c>IConfiguration</c>; after a save we
/// <c>Reload()</c> the configuration so <c>IOptionsMonitor&lt;TriageOptions&gt;</c>
/// consumers pick up the change immediately — no restart, and no rewriting of
/// appsettings.json on disk.
/// </summary>
public class SettingsService
{
    private readonly TriageDbContext _db;
    private readonly IOptionsMonitor<TriageOptions> _options;
    private readonly IConfigurationRoot _configurationRoot;

    public SettingsService(TriageDbContext db, IOptionsMonitor<TriageOptions> options, IConfiguration configuration)
    {
        _db = db;
        _options = options;
        // The host's configuration is always an IConfigurationRoot; we need Reload()
        // to re-read the DB-backed source after a save.
        _configurationRoot = (IConfigurationRoot)configuration;
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
        // Keep the existing token unless the caller actually provided a new,
        // unmasked one (the GET endpoint never returns the real token).
        var existingToken = _options.CurrentValue.GitHub.Token;
        var tokenToStore = dto.GitHubToken == MaskToken(existingToken) ? existingToken : dto.GitHubToken;

        var row = await _db.AppSettings.SingleOrDefaultAsync(ct);
        if (row is null)
        {
            row = new AppSettingsEntity { Id = 1 };
            _db.AppSettings.Add(row);
        }

        row.IssueSource = dto.IssueSource;
        row.LocalJsonPath = dto.LocalJsonPath;
        row.GitHubOwner = dto.GitHubOwner;
        row.GitHubRepo = dto.GitHubRepo;
        row.GitHubIssueNumber = dto.GitHubIssueNumber;
        row.GitHubToken = tokenToStore;
        row.PriorityCritical = dto.PriorityCritical;
        row.PriorityHigh = dto.PriorityHigh;
        row.PriorityMedium = dto.PriorityMedium;
        row.LowConfidenceReviewThreshold = dto.LowConfidenceReviewThreshold;

        await _db.SaveChangesAsync(ct);

        // Re-read the DB-backed configuration source so the new values take effect
        // immediately for IOptionsMonitor<TriageOptions> consumers.
        _configurationRoot.Reload();
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return "";
        return token.Length <= 4 ? "****" : $"****{token[^4..]}";
    }
}
