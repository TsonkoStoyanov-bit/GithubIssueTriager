using System.Net.Http.Json;
using System.Net;
using GithubIssueTriager.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GithubIssueTriager.Tests;

/// <summary>
/// End-to-end tests against the real Api host via WebApplicationFactory. The
/// Postgres-backed TriageDbContext registered by Program.cs is swapped out
/// for the EF Core in-memory provider here, so these tests never require a
/// real Postgres instance to be running.
/// </summary>
public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.TestFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(TestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Root_ReturnsOk()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FixturesEndpoint_ListsFiveMockIssues()
    {
        var response = await _client.GetAsync("/api/triage/fixtures");
        response.EnsureSuccessStatusCode();

        var fixtures = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(fixtures);
        Assert.Equal(5, fixtures!.Count);
    }

    [Fact]
    public async Task TriageJson_ReturnsClassifiedResult()
    {
        var response = await _client.PostAsJsonAsync("/api/triage/json", new
        {
            fileName = "issue_201_memory_leak.json",
            repo = "org/repo",
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"category\":\"bug\"", body.Replace(" ", "").ToLowerInvariant());
    }

    // Regression test for a real bug: posting a body with no fileName (e.g. an
    // unedited Swagger/Scalar "Try it out" example) used to crash with an
    // unhandled ArgumentNullException from Path.Combine inside the endpoint,
    // surfacing as a raw 500 instead of a clean validation error. The endpoint
    // now checks for this explicitly -- this test pins that behaviour down so
    // it can't silently regress back to a 500.
    [Fact]
    public async Task TriageJson_MissingFileName_ReturnsBadRequestNotServerError()
    {
        var response = await _client.PostAsJsonAsync("/api/triage/json", new
        {
            repo = "org/repo",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TriageGitHub_MissingOwnerOrRepo_ReturnsBadRequestNotServerError()
    {
        var response = await _client.PostAsJsonAsync("/api/triage/github", new
        {
            owner = "",
            repo = "claude-code",
            number = 1,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NextIssue_NoHistory_ReturnsOne()
    {
        // A repo with no triage history yet should suggest issue #1.
        var response = await _client.GetAsync($"/api/history/next-issue?owner=fresh&repo=repo-{Guid.NewGuid():N}");
        response.EnsureSuccessStatusCode();

        var next = await response.Content.ReadFromJsonAsync<int>();
        Assert.Equal(1, next);
    }

    [Fact]
    public async Task NextIssue_AfterTriage_ReturnsHighestPlusOne()
    {
        // Triage a fixture into a uniquely-named repo so this test is isolated from
        // any other history in the shared in-memory database.
        const string owner = "org";
        var repoName = $"next-{Guid.NewGuid():N}";

        var triage = await _client.PostAsJsonAsync("/api/triage/json", new
        {
            fileName = "issue_201_memory_leak.json",
            repo = $"{owner}/{repoName}",
        });
        triage.EnsureSuccessStatusCode();

        // Read the actual issue number back rather than hard-coding the fixture's value.
        var triaged = await triage.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var issueNumber = triaged.GetProperty("issue").GetProperty("number").GetInt32();

        var response = await _client.GetAsync($"/api/history/next-issue?owner={owner}&repo={repoName}");
        response.EnsureSuccessStatusCode();

        var next = await response.Content.ReadFromJsonAsync<int>();
        Assert.Equal(issueNumber + 1, next);
    }

    public class TestFactory : WebApplicationFactory<Program>
    {
        // One stable in-memory database name for the whole factory, so data written by
        // one request is visible to the next (e.g. triage then read-back). Generated per
        // factory instance with a Guid, so parallel test classes still can't collide.
        private readonly string _dbName = $"triager-it-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            // By default WebApplicationFactory roots the host at the Api's *source*
            // directory, which has no fixtures/ folder (the fixtures live at the repo
            // root and are only copied into each project's build output). Point the
            // content root at the test assembly's output directory, where this project's
            // csproj copies the same fixtures/*.json -- so endpoints that read
            // ContentRootPath/fixtures find the five mock issues.
            builder.UseContentRoot(AppContext.BaseDirectory);

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TriageDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                // Program.cs already registered the Npgsql provider's services into the
                // app container via AddDbContext/UseNpgsql. Simply re-registering with
                // UseInMemoryDatabase here would leave *both* providers visible to EF Core
                // in the same container, which throws "Only a single database provider can
                // be registered". Pin the in-memory context to its own isolated internal
                // service provider so it never sees the Npgsql registration.
                var inMemoryProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                services.AddDbContext<TriageDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName)
                           .UseInternalServiceProvider(inMemoryProvider));
            });
        }
    }
}
