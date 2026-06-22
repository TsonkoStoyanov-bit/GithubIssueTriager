using Microsoft.EntityFrameworkCore;

namespace GithubIssueTriager.Api.Data;

/// <summary>
/// Brings the database schema up to date. On a relational provider (Npgsql) this
/// applies any pending EF Core migrations — which is what lets a newly added
/// table or column reach an *existing* database without hand-written DDL. The
/// in-memory provider used by the tests doesn't support migrations, so it falls
/// back to EnsureCreated there.
/// </summary>
public static class DatabaseBootstrapper
{
    public static void Bootstrap(TriageDbContext db)
    {
        // Gate on the configured provider (IsNpgsql), not IsRelational: the
        // integration-test host registers both the Npgsql and in-memory providers,
        // which can make IsRelational report true for the in-memory context and send
        // it down the relational Migrate path — throwing "Relational-specific methods
        // can only be used...". IsNpgsql reflects the actual provider, so the
        // in-memory test context always takes EnsureCreated.
        if (db.Database.IsNpgsql())
            db.Database.Migrate();
        else
            db.Database.EnsureCreated();
    }
}
