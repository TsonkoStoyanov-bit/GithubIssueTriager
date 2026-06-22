using System.Globalization;
using GithubIssueTriager.Api.Data;
using GithubIssueTriager.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GithubIssueTriager.Api.Configuration;

/// <summary>
/// Surfaces the database-backed Triage settings into the standard .NET
/// configuration system, layered on top of appsettings.json. Because the values
/// flow through <c>IConfiguration</c>, every existing
/// <c>IOptionsMonitor&lt;TriageOptions&gt;</c> consumer (PriorityEngine,
/// LabelAdvisor, the endpoints) reads them with no code change, and a
/// <c>Reload()</c> after a save propagates the new values live — the same
/// behaviour the old file-watch gave, but with the database as the source of
/// truth. The supplied <see cref="Seed"/> (taken from appsettings.json) is
/// written into the table the first time it is empty.
/// </summary>
public sealed class EfTriageConfigurationSource : IConfigurationSource
{
    public Action<DbContextOptionsBuilder> ConfigureDb { get; }
    public TriageOptions Seed { get; }

    public EfTriageConfigurationSource(Action<DbContextOptionsBuilder> configureDb, TriageOptions seed)
    {
        ConfigureDb = configureDb;
        Seed = seed;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new EfTriageConfigurationProvider(this);
}

public sealed class EfTriageConfigurationProvider : ConfigurationProvider
{
    private readonly EfTriageConfigurationSource _source;

    public EfTriageConfigurationProvider(EfTriageConfigurationSource source) => _source = source;

    public override void Load()
    {
        try
        {
            var options = new DbContextOptionsBuilder<TriageDbContext>();
            _source.ConfigureDb(options);
            using var db = new TriageDbContext(options.Options);

            // EnsureCreated is idempotent and matches how the triage store
            // bootstraps its schema — no separate migration step to keep in sync.
            db.Database.EnsureCreated();

            var row = db.AppSettings.SingleOrDefault();
            if (row is null)
            {
                // First run: seed the single row from appsettings.json so there is
                // always a sensible starting point and the table is the source of
                // truth from here on.
                row = ToEntity(_source.Seed);
                db.AppSettings.Add(row);
                db.SaveChanges();
            }

            Data = ToConfigData(row);
        }
        catch
        {
            // The database may be unreachable when configuration is first built
            // (running without Postgres, or in tests using the in-memory store).
            // Fall back to whatever the JSON providers already supplied rather
            // than failing app startup.
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static AppSettingsEntity ToEntity(TriageOptions o) => new()
    {
        Id = 1,
        IssueSource = o.IssueSource,
        LocalJsonPath = o.LocalJsonPath,
        GitHubOwner = o.GitHub.Owner,
        GitHubRepo = o.GitHub.Repo,
        GitHubIssueNumber = o.GitHub.IssueNumber,
        GitHubToken = o.GitHub.Token,
        PriorityCritical = o.PriorityThresholds.Critical,
        PriorityHigh = o.PriorityThresholds.High,
        PriorityMedium = o.PriorityThresholds.Medium,
        LowConfidenceReviewThreshold = o.LowConfidenceReviewThreshold,
    };

    private static Dictionary<string, string?> ToConfigData(AppSettingsEntity e)
    {
        var p = TriageOptions.SectionName; // "Triage"
        var inv = CultureInfo.InvariantCulture;
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [$"{p}:IssueSource"] = e.IssueSource,
            [$"{p}:LocalJsonPath"] = e.LocalJsonPath,
            [$"{p}:GitHub:Owner"] = e.GitHubOwner,
            [$"{p}:GitHub:Repo"] = e.GitHubRepo,
            [$"{p}:GitHub:IssueNumber"] = e.GitHubIssueNumber.ToString(inv),
            [$"{p}:GitHub:Token"] = e.GitHubToken,
            [$"{p}:PriorityThresholds:Critical"] = e.PriorityCritical.ToString(inv),
            [$"{p}:PriorityThresholds:High"] = e.PriorityHigh.ToString(inv),
            [$"{p}:PriorityThresholds:Medium"] = e.PriorityMedium.ToString(inv),
            [$"{p}:LowConfidenceReviewThreshold"] = e.LowConfidenceReviewThreshold.ToString(inv),
        };
    }
}

public static class EfTriageConfigurationExtensions
{
    /// <summary>
    /// Layers the database-backed Triage settings on top of the existing config,
    /// seeding the table from <paramref name="seed"/> (the appsettings.json
    /// values) the first time it is empty.
    /// </summary>
    public static IConfigurationBuilder AddEfTriageSettings(
        this IConfigurationBuilder builder, Action<DbContextOptionsBuilder> configureDb, TriageOptions seed) =>
        builder.Add(new EfTriageConfigurationSource(configureDb, seed));
}
