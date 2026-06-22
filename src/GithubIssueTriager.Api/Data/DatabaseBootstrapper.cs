using Microsoft.EntityFrameworkCore;

namespace GithubIssueTriager.Api.Data;

/// <summary>
/// Brings the database schema up to date. On Npgsql this applies EF Core
/// migrations — including a one-time baseline step for databases that were
/// originally created by the old EnsureCreated path (they have the tables but
/// InitialCreate isn't recorded, so a plain Migrate() would try to recreate them
/// and fail with "relation already exists"). The in-memory provider used by
/// tests doesn't support migrations, so it falls back to EnsureCreated.
/// </summary>
public static class DatabaseBootstrapper
{
    // Must match the InitialCreate migration's id (the file name without extension).
    private const string InitialCreateMigrationId = "20260622110740_InitialCreate";

    public static void Bootstrap(TriageDbContext db)
    {
        // Gate on the configured provider (IsNpgsql), not IsRelational: the
        // integration-test host registers both the Npgsql and in-memory providers,
        // which can make IsRelational report true for the in-memory context and send
        // it down the relational Migrate path — throwing "Relational-specific methods
        // can only be used...". IsNpgsql reflects the actual provider.
        if (!db.Database.IsNpgsql())
        {
            db.Database.EnsureCreated();
            return;
        }

        BaselineLegacyDatabaseIfNeeded(db);
        db.Database.Migrate();
    }

    /// <summary>
    /// If the database already holds the schema but InitialCreate isn't recorded as
    /// applied, mark it applied — after making sure both of its tables exist — so
    /// Migrate() skips it instead of failing on "already exists". This covers a
    /// database created by EnsureCreated, including the state left behind by an
    /// earlier failed Migrate() (an empty __EFMigrationsHistory table plus the old
    /// triage_history table).
    /// </summary>
    private static void BaselineLegacyDatabaseIfNeeded(TriageDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        connection.Open();
        try
        {
            bool Scalar(string sql)
            {
                using var check = connection.CreateCommand();
                check.CommandText = sql;
                return (bool)check.ExecuteScalar()!;
            }

            // No legacy schema → fresh database: let Migrate() create everything.
            if (!Scalar("SELECT to_regclass('public.triage_history') IS NOT NULL;"))
                return;

            var historyExists = Scalar("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;");
            var initialApplied = historyExists && Scalar(
                $"SELECT EXISTS(SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{InitialCreateMigrationId}');");

            // Already migrated/baselined → nothing to do; Migrate() applies any newer ones.
            if (initialApplied)
                return;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                -- app_settings may be missing (oldest databases, or a rolled-back failed
                -- migration); create it to match the InitialCreate schema before declaring
                -- that migration applied.
                CREATE TABLE IF NOT EXISTS app_settings (
                    "Id" integer NOT NULL,
                    "IssueSource" text NOT NULL,
                    "LocalJsonPath" text NOT NULL,
                    "GitHubOwner" text NOT NULL,
                    "GitHubRepo" text NOT NULL,
                    "GitHubIssueNumber" integer NOT NULL,
                    "GitHubToken" text NOT NULL,
                    "PriorityCritical" integer NOT NULL,
                    "PriorityHigh" integer NOT NULL,
                    "PriorityMedium" integer NOT NULL,
                    "LowConfidenceReviewThreshold" double precision NOT NULL,
                    CONSTRAINT "PK_app_settings" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );

                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{InitialCreateMigrationId}', '10.0.0')
                ON CONFLICT ("MigrationId") DO NOTHING;
                """;
            cmd.ExecuteNonQuery();
        }
        finally
        {
            connection.Close();
        }
    }
}
