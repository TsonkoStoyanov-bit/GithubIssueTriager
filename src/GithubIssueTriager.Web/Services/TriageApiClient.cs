using System.Net.Http.Json;
using System.Text.Json;
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

    public Task<ApiResult<TriageResultDto>> TriageJsonAsync(string fileName, string? repo, CancellationToken ct = default) =>
        SendAsync<TriageResultDto>(() => _http.PostAsJsonAsync("/api/triage/json", new TriageLocalRequest(fileName, repo), ct), ct);

    public Task<ApiResult<TriageResultDto>> TriageGitHubAsync(string owner, string repo, int number, CancellationToken ct = default) =>
        SendAsync<TriageResultDto>(() => _http.PostAsJsonAsync("/api/triage/github", new TriageGitHubRequest(owner, repo, number), ct), ct);

    public Task<ApiResult<List<TriageResultDto>>> TriageBatchAsync(string? repo, CancellationToken ct = default) =>
        SendAsync<List<TriageResultDto>>(() => _http.PostAsJsonAsync("/api/triage/batch", new TriageBatchRequest(repo), ct), ct);

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

    /// <summary>
    /// Runs an HTTP call and always returns an <see cref="ApiResult{T}"/> — network failures
    /// (Api unreachable, timeouts) and unexpected non-JSON error bodies are turned into a
    /// failed result with a friendly message instead of bubbling up as an exception that would
    /// break the Blazor circuit before the page can show anything.
    /// </summary>
    private static async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        try
        {
            using var response = await send();
            return await ToApiResultAsync<T>(response, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // genuine caller cancellation — let it propagate
        }
        catch (TaskCanceledException)
        {
            return ApiResult<T>.Fail("the request to the API timed out", "the Api may be slow or unreachable — check that it is running");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Fail($"could not reach the API: {ex.Message}", "check that GithubIssueTriager.Api is running and Api:BaseUrl is correct");
        }
    }

    private static async Task<ApiResult<T>> ToApiResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return ApiResult<T>.Ok(value!);
        }

        // The Api returns a JSON ApiErrorResponse for handled failures (400/404/502), but an
        // unhandled 500 yields an HTML error page or empty body — reading that as JSON throws,
        // so fall back to the status code rather than letting the exception escape.
        var fallback = $"request failed with status {(int)response.StatusCode} ({response.ReasonPhrase})";
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
            return ApiResult<T>.Fail(error?.Error ?? fallback, error?.Hint);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return ApiResult<T>.Fail(fallback, "the Api returned an unexpected (non-JSON) error response");
        }
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
