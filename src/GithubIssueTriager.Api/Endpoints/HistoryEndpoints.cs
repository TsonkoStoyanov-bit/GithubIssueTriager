using GithubIssueTriager.Api.Services;

namespace GithubIssueTriager.Api.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/history", (TriageOrchestrator orchestrator, int? limit) =>
            Results.Ok(orchestrator.GetHistory(limit ?? 50)))
            .WithTags("History");
    }
}
