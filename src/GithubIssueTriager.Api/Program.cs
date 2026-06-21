using GithubIssueTriager.Api.Classification;
using GithubIssueTriager.Api.Data;
using GithubIssueTriager.Api.Endpoints;
using GithubIssueTriager.Api.Labels;
using GithubIssueTriager.Api.Priority;
using GithubIssueTriager.Api.Services;
using GithubIssueTriager.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TriageOptions>(builder.Configuration.GetSection(TriageOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.AddHttpClient<IIssueSource, GitHubIssueSource>();
builder.Services.AddSingleton<LexiconClassifier>();
builder.Services.AddSingleton<PriorityEngine>();
builder.Services.AddSingleton<LabelAdvisor>();

var dbOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
    throw new InvalidOperationException("Database:ConnectionString is empty in appsettings.json.");

builder.Services.AddDbContext<TriageDbContext>(options => options.UseNpgsql(dbOptions.ConnectionString));
builder.Services.AddScoped<ITriageStore, EfTriageStore>();

builder.Services.AddScoped<TriageOrchestrator>();
builder.Services.AddScoped<SettingsService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GitHub Issue Auto-Triager API",
        Version = "v1",
        Description = "Classifies GitHub issues, scores priority, and advises on labels/next steps.",
    });
});

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// API docs are available in every environment (not just Development) since
// this is a small demo/exam project meant to be easy to explore: classic
// Swagger UI at /swagger, plus Scalar's modern UI at /scalar, both reading
// the same generated OpenAPI document.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "GitHub Issue Auto-Triager API v1");
});
app.MapScalarApiReference(options =>
{
    options.WithTitle("GitHub Issue Auto-Triager API")
           .WithOpenApiRoutePattern("/swagger/v1/swagger.json");
});

app.UseCors();

app.MapTriageEndpoints();
app.MapHistoryEndpoints();
app.MapSettingsEndpoints();

// Root redirects straight to the API docs — easiest landing page for
// anyone opening http://localhost:5101/ in a browser. The old plain-JSON
// status check still exists, just moved to /health so nothing that was
// polling it breaks.
app.MapGet("/", () => Results.Redirect("/scalar"));
app.MapGet("/health", () => Results.Ok(new { service = "GithubIssueTriager.Api", status = "ok" }));

app.Run();

// Exposed so WebApplicationFactory<Program> works from the test project.
public partial class Program { }
