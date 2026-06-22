using GithubIssueTriager.Api.Services;

namespace GithubIssueTriager.Api.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/history", (TriageOrchestrator orchestrator, int? limit) =>
            Results.Ok(orchestrator.GetHistory(limit ?? 50)))
            .WithTags("History");

        // Suggests the next issue number to triage for owner/repo, derived from stored
        // history (highest triaged + 1, or 1 when there's none). The UI uses this to
        // pre-fill the Remote tab instead of a manually configured default.
        app.MapGet("/api/history/next-issue", (TriageOrchestrator orchestrator, string? owner, string? repo) =>
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
                return Results.Ok(1);
            return Results.Ok(orchestrator.GetNextIssueNumber($"{owner}/{repo}"));
        }).WithTags("History");
    }
}
