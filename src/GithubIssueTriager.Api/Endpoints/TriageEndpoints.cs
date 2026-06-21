using GithubIssueTriager.Api.Services;
using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Endpoints;

public static class TriageEndpoints
{
    public static void MapTriageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/triage").WithTags("Triage");

        group.MapPost("/json", async (TriageLocalRequest req, TriageOrchestrator orchestrator, Microsoft.Extensions.Options.IOptionsMonitor<TriageOptions> options, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.FileName))
                return Results.BadRequest(new ApiErrorResponse("fileName is required", "check GET /api/triage/fixtures for available files"));

            var localPath = Path.Combine(ResolveFixturesDirectory(options.CurrentValue.LocalJsonPath), req.FileName);
            if (!File.Exists(localPath))
                return Results.NotFound(new ApiErrorResponse($"fixture not found: {req.FileName}", "check GET /api/triage/fixtures for available files"));

            var result = await orchestrator.TriageFromJsonAsync(localPath, req.Repo ?? "local/mock-repo", ct);
            return Results.Ok(result);
        });

        group.MapPost("/github", async (TriageGitHubRequest req, TriageOrchestrator orchestrator, Microsoft.Extensions.Options.IOptionsMonitor<TriageOptions> options, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Owner) || string.IsNullOrWhiteSpace(req.Repo))
                return Results.BadRequest(new ApiErrorResponse("owner and repo are required", null));

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

        group.MapPost("/batch", async (TriageBatchRequest req, TriageOrchestrator orchestrator, Microsoft.Extensions.Options.IOptionsMonitor<TriageOptions> options, CancellationToken ct) =>
        {
            var directory = ResolveFixturesDirectory(options.CurrentValue.LocalJsonPath);
            var results = await orchestrator.TriageDirectoryAsync(directory, req.Repo ?? "local/mock-repo", ct);
            return Results.Ok(results);
        });

        group.MapGet("/fixtures", (TriageOrchestrator orchestrator, Microsoft.Extensions.Options.IOptionsMonitor<TriageOptions> options) =>
        {
            var directory = ResolveFixturesDirectory(options.CurrentValue.LocalJsonPath);
            return Results.Ok(orchestrator.ListFixtures(directory));
        });
    }

    // Fixtures are copied next to the built assembly (see the csproj Content/LinkBase item),
    // so a relative LocalJsonPath must resolve against AppContext.BaseDirectory -- NOT
    // ContentRootPath. The two diverge whenever the app runs from build output instead of a
    // `dotnet publish`: plain `dotnet run` and Visual Studio's Docker fast mode both leave
    // ContentRootPath pointing at the project folder while the copied fixtures live under bin/.
    // An absolute LocalJsonPath is honoured as-is.
    private static string ResolveFixturesDirectory(string localJsonPath) =>
        Path.IsPathRooted(localJsonPath)
            ? localJsonPath
            : Path.Combine(AppContext.BaseDirectory, localJsonPath);
}
