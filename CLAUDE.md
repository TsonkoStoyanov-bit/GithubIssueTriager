# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# restore + build the whole solution
dotnet restore
dotnet build

# run the Api (minimal APIs, defaults to https://localhost:5101)
dotnet run --project src/GithubIssueTriager.Api

# run the Blazor Server UI (defaults to https://localhost:5100-ish; check console output)
dotnet run --project src/GithubIssueTriager.Web

# run all tests
dotnet test

# run a single test class / method
dotnet test --filter "FullyQualifiedName~LexiconClassifierTests"
dotnet test --filter "FullyQualifiedName~LabelAdvisorTests.LowConfidence_AddsReviewLabel"

# full stack (Postgres + Api + Web) in Docker, no local SDK/Postgres needed
docker compose up --build
docker compose down       # add -v to also drop the Postgres volume
```

API docs (Swagger UI and Scalar) are mounted in every environment, not just Development:
`/swagger`, `/scalar` (Scalar.AspNetCore 2.x's default route — earlier 1.x versions used
`/scalar/v1`), raw spec at `/swagger/v1/swagger.json`.

## Architecture

4-project .NET 10 solution. `GithubIssueTriager.Web` never touches the database directly — it
only calls `GithubIssueTriager.Api` over HTTP through `TriageApiClient`, so the two can be
deployed/scaled independently.

```
GithubIssueTriager.Shared/   DTOs and option types shared by Api + Web (Issue, ClassificationResult,
                              PriorityResult, LabelResult, TriageRecord, TriageOptions, DatabaseOptions)
GithubIssueTriager.Api/      ASP.NET Core minimal API
GithubIssueTriager.Web/      Blazor Server UI (Fluent UI components)
GithubIssueTriager.Tests/    xUnit — classifier, priority engine, label advisor, EF Core store,
                              GitHub ingestion, and WebApplicationFactory-based API integration tests
fixtures/                    5 mock GitHub issues used by the "Local JSON" issue source
```

### Triage pipeline (the core of `GithubIssueTriager.Api`)

A request flows through `Services/TriageOrchestrator.cs` as: ingest → classify → score priority →
advise labels → persist. Each stage is its own independently-testable piece:

- **Classification** (`Classification/`) — `Tokenizer` does hand-rolled word splitting (no regex),
  `CategoryLexicon` holds a per-category dictionary of weighted words/phrases, and
  `LexiconClassifier` scores text against it (title words count double). Confidence is the winning
  category's share of total weight matched across *all* categories.
- **Priority** (`Priority/`) — `PriorityEngine` sums votes from independent `IPriorityHeuristic`
  implementations in `Priority/Heuristics/` (severity wording, diagnostic evidence like code blocks/
  logs/HTTP status codes, community engagement tiers, regression-language detection). Each heuristic
  returns a `HeuristicVote(Weight, Reason)`. **Add a new signal by writing a new `IPriorityHeuristic`
  class and registering it in `PriorityEngine`'s constructor list — don't edit a big scoring
  function.** The summed score is mapped to low/medium/high/critical via `TriageOptions.PriorityThresholds`.
- **Labels** (`Labels/LabelAdvisor.cs`) — maps category+priority to labels and a next-step string via
  static lookup tables, then layers a confidence-aware safety net on top: if classification
  confidence is below `TriageOptions.LowConfidenceReviewThreshold`, it adds a `needs-human-review`
  label and rewrites the next step instead of presenting a low-confidence guess as settled.
- **Storage** (`Services/ITriageStore.cs` → `EfTriageStore.cs`) — backed by EF Core
  (`Data/TriageDbContext.cs`, `Data/TriageHistoryEntity.cs`) against PostgreSQL only
  (`Npgsql.EntityFrameworkCore.PostgreSQL`). `Init()` calls `Database.EnsureCreated()` —
  no separate `dotnet ef migrations` step to keep in sync by hand. Tests swap in the EF
  Core in-memory provider (`Microsoft.EntityFrameworkCore.InMemory`) instead of hitting a
  real Postgres instance — see `EfTriageStoreTests.cs` and `ApiIntegrationTests.TestFactory`.
- **Ingestion** (`Services/IIssueSource.cs` → `GitHubIssueSource.cs`) — fetches from the real GitHub
  REST API or reads from `fixtures/*.json`, selected by `TriageOptions.IssueSource` ("Local" vs
  "Remote"). Network failures surface as a clear error rather than crashing the request.

### Live-reloading configuration

`TriageOptions` is read through `IOptionsMonitor<T>` (not a constructor-injected snapshot), and
`appsettings.json`'s default `reloadOnChange: true` file watcher means the **Settings page writes
back to the Api's `appsettings.json`** (`Services/SettingsService.cs`, via `GET`/`PUT /api/settings`)
and the change applies immediately, no restart. This covers issue source (Local/Remote), GitHub
owner/repo/issue number + token, priority thresholds, and the low-confidence review threshold.
`DatabaseOptions` (the Postgres connection string) is *not* part of this live-reload path — it's
read once at startup to configure the EF Core `DbContext`, so changing it needs a restart.

### Known gotchas (found by an actual build, not just review)

- In `Components/App.razor`, `InteractiveServer` requires
  `@using static Microsoft.AspNetCore.Components.Web.RenderMode` — it's a static property of
  `RenderMode`, not a free-standing name.
- This Fluent UI Blazor version's `HorizontalAlignment` enum only has `Left`/`Center`/`Right`/
  `Stretch` — there is no `SpaceBetween`. Use `Style="justify-content:space-between"` instead.
- `TriageSettingsDto` must stay a mutable `class` (not a `record`) — Razor's `@bind-Value` two-way
  binding needs settable properties, and `record` positional properties are init-only.
- Inside containers, `Web/Program.cs` skips `UseHttpsRedirection()` when
  `DOTNET_RUNNING_IN_CONTAINER=true` (set by its Dockerfile), since there's no HTTPS cert in the
  container by default.
- `docker-compose.yml` overrides config via the ASP.NET Core double-underscore env var syntax (e.g.
  `Database__ConnectionString`, `Api__BaseUrl`) — match that pattern for any new nested config option.
- Swashbuckle.AspNetCore v10+ (required for .NET 10's OpenAPI 3.1 support) moved its model types
  from `Microsoft.OpenApi.Models` to `Microsoft.OpenApi` — `Program.cs` uses
  `using Microsoft.OpenApi;` for `OpenApiInfo`, not the old namespace. If you add more OpenAPI
  customization (filters, etc.), expect interfaces like `IOpenApiSchema` rather than the old
  concrete `OpenApiSchema` types in a few places.
