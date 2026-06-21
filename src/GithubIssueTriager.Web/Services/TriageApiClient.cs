using System.Net.Http.Json;
using GithubIssueTriager.Shared.Models;

namespace GithubIssueTriager.Web.Services;

/// <summary>Typed HttpClient wrapper the Blazor UI uses to talk to GithubIssueTriager.Api.</summary>
public class TriageApiClient
{
    private readonly HttpClient _http;

    public TriageApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<string>> GetFixturesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<string>>("/api/triage/fixtures", ct) ?? new();

    public async Task<ApiResult<TriageResultDto>> TriageJsonAsync(string fileName, string? repo, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/triage/json", new TriageLocalRequest(fileName, repo), ct);
        return await ToApiResultAsync<TriageResultDto>(response, ct);
    }

    public async Task<ApiResult<TriageResultDto>> TriageGitHubAsync(string owner, string repo, int number, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/triage/github", new TriageGitHubRequest(owner, repo, number), ct);
        return await ToApiResultAsync<TriageResultDto>(response, ct);
    }

    public async Task<ApiResult<List<TriageResultDto>>> TriageBatchAsync(string? repo, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/triage/batch", new TriageBatchRequest(repo), ct);
        return await ToApiResultAsync<List<TriageResultDto>>(response, ct);
    }

    public async Task<List<TriageRecord>> GetHistoryAsync(int limit = 50, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<TriageRecord>>($"/api/history?limit={limit}", ct) ?? new();

    public async Task<TriageSettingsDto?> GetSettingsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<TriageSettingsDto>("/api/settings", ct);

    public async Task<TriageSettingsDto?> SaveSettingsAsync(TriageSettingsDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("/api/settings", dto, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TriageSettingsDto>(cancellationToken: ct);
    }

    private static async Task<ApiResult<T>> ToApiResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return ApiResult<T>.Ok(value!);
        }

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
        return ApiResult<T>.Fail(error?.Error ?? $"request failed with status {(int)response.StatusCode}", error?.Hint);
    }
}

/// <summary>Small result wrapper so Razor pages can show API errors without exceptions.</summary>
public class ApiResult<T>
{
    public bool Success { get; private init; }
    public T? Value { get; private init; }
    public string? Error { get; private init; }
    public string? Hint { get; private init; }

    public static ApiResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static ApiResult<T> Fail(string error, string? hint) => new() { Success = false, Error = error, Hint = hint };
}
