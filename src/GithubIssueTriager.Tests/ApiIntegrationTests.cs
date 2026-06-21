using System.Net.Http.Json;
using System.Net;
using GithubIssueTriager.Api.Data;
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

    public class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TriageDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddDbContext<TriageDbContext>(options =>
                    options.UseInMemoryDatabase($"triager-it-{Guid.NewGuid():N}"));
            });
        }
    }
}
