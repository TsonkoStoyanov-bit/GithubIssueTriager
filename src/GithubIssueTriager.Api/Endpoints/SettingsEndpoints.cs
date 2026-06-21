using GithubIssueTriager.Api.Services;
using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/", (SettingsService settings) => Results.Ok(settings.GetCurrent()));

        group.MapPut("/", async (TriageSettingsDto dto, SettingsService settings, CancellationToken ct) =>
        {
            await settings.SaveAsync(dto, ct);
            return Results.Ok(settings.GetCurrent());
        });
    }
}
