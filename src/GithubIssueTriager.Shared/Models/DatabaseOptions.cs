namespace GithubIssueTriager.Shared.Models;

/// <summary>
/// Bound from the "Database" section of appsettings.json. The triage history
/// store is PostgreSQL via EF Core — this just carries the connection string.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>e.g. "Host=localhost;Port=5432;Database=issue_triager;Username=postgres;Password=postgres".</summary>
    public string ConnectionString { get; set; } = "";
}
