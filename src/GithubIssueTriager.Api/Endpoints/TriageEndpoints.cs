using GithubIssueTriager.Api.Services;
using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Endpoints;

public static class TriageEndpoints
{
    public static void MapTriageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/triage").WithTags("Triage");

        group.MapPost("/json", async (TriageLocalRequest req, TriageOrchestrator orchestrator, IWebHostEnvironment env, CancellationToken ct) =>
        {
            var localPath = Path.Combine(env.ContentRootPath, "fixtures", req.FileName);
            if (!File.Exists(localPath))
                return Results.NotFound(new ApiErrorResponse($"fixture not found: {req.FileName}", "check GET /api/triage/fixtures for available files"));

            var result = await orchestrator.TriageFromJsonAsync(localPath, req.Repo ?? "local/mock-repo", ct);
            return Results.Ok(result);
        });

        group.MapPost("/github", async (TriageGitHubRequest req, TriageOrchestrator orchestrator, Microsoft.Extensions.Options.IOptionsMonitor<TriageOptions> options, CancellationToken ct) =>
        {
            try
            {
                var token = options.CurrentValue.GitHub.Token;
                var result = await orchestrator.TriageFromGitHubAsync(req.Owner, req.Repo, req.Number, string.IsNullOrWhiteSpace(token) ? null : token, ct);
                return Results.Ok(result);
            }
            catch (GitHubApiException ex)
            {
                return Results.Json(
                    new ApiErrorResponse(ex.Message, "use POST /api/triage/json with a local fixture instead"),
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapPost("/batch", async (TriageBatchRequest req, TriageOrchestrator orchestrator, IWebHostEnvironment env, Microsoft.Extensions.Options.IOptionsMonitor<TriageOptions> options, CancellationToken ct) =>
        {
            var directory = Path.Combine(env.ContentRootPath, options.CurrentValue.LocalJsonPath);
            var results = await orchestrator.TriageDirectoryAsync(directory, req.Repo ?? "local/mock-repo", ct);
            return Results.Ok(results);
        });

        group.MapGet("/fixtures", (TriageOrchestrator orchestrator, IWebHostEnvironment env, Microsoft.Extensions.Options.IOptionsMonitor<TriageOptions> options) =>
        {
            var directory = Path.Combine(env.ContentRootPath, options.CurrentValue.LocalJsonPath);
            return Results.Ok(orchestrator.ListFixtures(directory));
        });
    }
}
