# GitHub Issue Auto-Triager (.NET / Blazor edition)

An AI-assisted issue triage tool: a REST API plus a Blazor Server + Fluent
UI front end that classifies GitHub issues (bug / feature / question /
documentation), scores their priority, advises on labels, and recommends a
next step for a maintainer — backed by PostgreSQL via EF Core.

It is built as a proper multi-project .NET solution (REST API + separate
Blazor UI) so the backend and front end can be built, deployed, and scaled
independently — see "Design notes" below for the reasoning behind the
classifier, priority engine, and label advisor specifically.

> **Important — please read first:** this was written in a sandbox with no
> .NET SDK installed and no outbound network access, so it could not be
> compiled, restored, or run here. Everything below is written carefully
> and reviewed by hand, but **you are the first one to actually build and
> run it** — see "If something doesn't compile" at the bottom.

## Architecture — 4 projects in one solution

```
GithubIssueTriager.sln
src/
  GithubIssueTriager.Shared/   Class library: DTOs/models shared by Api + Web
  GithubIssueTriager.Api/      ASP.NET Core Web API (minimal APIs)
    Classification/            LexiconClassifier: weighted-vocabulary classifier
    Priority/                  PriorityEngine + pluggable IPriorityHeuristic rules
    Labels/                    LabelAdvisor: labels + next step + review flag
    Services/                  ingestion, EF Core/Postgres store, orchestrator,
                               settings (read/write appsettings.json)
    Endpoints/                 /api/triage/*, /api/history, /api/settings
  GithubIssueTriager.Web/      Blazor Server UI (Fluent UI components)
    Components/Pages/          Triage.razor, History.razor, Settings.razor
    Services/TriageApiClient.cs   typed HttpClient calling the Api
  GithubIssueTriager.Tests/    xUnit tests (classifier, priority engine,
                               label advisor, EF Core store, ingestion, API)
fixtures/                      5 mock issues covering bug/feature/question/
                               documentation plus one deliberately ambiguous case
docker-compose.yml              wires up db + api + web for `docker compose up`
src/GithubIssueTriager.Api/Dockerfile
src/GithubIssueTriager.Web/Dockerfile
```

The Web project never talks to the database directly — it only calls the
Api over HTTP. This mirrors a normal real-world split between a backend
service and a UI, and the two could even be deployed/scaled separately.

## Design notes — classifier, priority engine, and label advisor

- **Classification** (`Classification/LexiconClassifier.cs`) scores text
  against a per-category dictionary of weighted words and phrases (a tiny
  bag-of-words model) instead of counting regex pattern matches. Title
  words count double. Confidence is the winning category's share of the
  total weight matched across *all* categories.
- **Priority** (`Priority/PriorityEngine.cs`) is a small pipeline of
  independent `IPriorityHeuristic` strategies — severity wording, diagnostic
  evidence (code blocks / logs / HTTP status codes), community engagement
  tiers, and an explicit regression-language detector — each casting a
  weighted vote with its own reason. Adding a new signal means writing a
  new heuristic class, not editing a big scoring function.
- **Labels & next steps** (`Labels/LabelAdvisor.cs`) includes a safety net:
  if the classifier's confidence is below a configurable threshold, the
  advisor adds a `needs-human-review` label and
  rewrites the next step to say so, instead of presenting a low-confidence
  guess as a settled fact. `fixtures/issue_205_auth_bypass.json` is a real
  example where this fires — a sophisticated security report that doesn't
  use any of the classifier's common bug vocabulary.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL running locally (since you already have it installed).

### PostgreSQL setup

```bash
createdb issue_triager
```

Then edit `src/GithubIssueTriager.Api/appsettings.json`:

```json
"Database": {
  "ConnectionString": "Host=localhost;Port=5432;Database=issue_triager;Username=postgres;Password=postgres"
}
```

Adjust `Username`/`Password`/`Port` to match your local Postgres instance.
The `triage_history` table is created automatically on first run via EF
Core's `Database.EnsureCreated()` — you don't need to create it by hand or
run a separate migrations step.

## Build & run

```bash
cd github-issue-triager-dotnet
dotnet restore
dotnet build

# terminal 1 — the API (defaults to https://localhost:5101)
dotnet run --project src/GithubIssueTriager.Api

# terminal 2 — the Blazor UI (defaults to https://localhost:5100-ish; check the console output)
dotnet run --project src/GithubIssueTriager.Web
```

Open the URL the Web project prints in its console. You should see three
pages in the nav: **Triage**, **History**, **Settings**.

If the Web project's port doesn't match `Api:BaseUrl` in
`src/GithubIssueTriager.Web/appsettings.json`, update it to whatever port
the Api project actually started on (printed in its own console output).

## API docs

The Api serves interactive OpenAPI documentation in every environment
(not gated behind `Development`), so it's reachable however you run it —
locally or in a container:

- **Swagger UI** — `http://localhost:5101/swagger`
- **Scalar** — `http://localhost:5101/scalar` (a newer, faster
  alternative UI reading the same generated OpenAPI document — useful for
  trying requests with a cleaner layout than classic Swagger UI)
- Raw OpenAPI JSON — `http://localhost:5101/swagger/v1/swagger.json`

## Running with Docker

The whole stack — Postgres, the Api, and the Web UI — can run in
containers without installing the .NET SDK or Postgres locally:

```bash
cd github-issue-triager-dotnet
docker compose up --build
```

This starts three services:

- `db` — `postgres:16`, with a named volume so data survives restarts
- `api` — built from `src/GithubIssueTriager.Api/Dockerfile`, on
  `http://localhost:5101`, pointed at `db` via
  `Database__ConnectionString` (the double-underscore is how
  ASP.NET Core config keys are overridden through environment variables)
- `web` — built from `src/GithubIssueTriager.Web/Dockerfile`, on
  `http://localhost:5100`, pointed at the `api` service via `Api__BaseUrl`

The `api` service waits for `db`'s healthcheck before starting, so the
first `triage_history` table creation doesn't race a Postgres that isn't
ready yet. Swagger/Scalar are reachable at `http://localhost:5101/swagger`
and `http://localhost:5101/scalar` the same as a local run.

To stop everything:

```bash
docker compose down          # keep the Postgres volume
docker compose down -v       # also wipe the Postgres volume
```

Each Dockerfile is a multi-stage build (SDK image to restore/publish,
slim ASP.NET runtime image to actually run), and `.dockerignore` keeps
`bin/`, `obj/`, and `.git/` out of the build context.

## Configuration form (Settings page)

The Settings page reads from and writes back to the **Api's**
`appsettings.json` (the `Triage` section), via `GET /api/settings` and
`PUT /api/settings`:

- **Issue source** — Local JSON (reads from the `fixtures/` folder shipped
  with the Api) or Remote (live GitHub REST API).
- **GitHub API** — default owner/repo/issue number, plus a personal access
  token (masked once saved; only resent if you actually change it).
- **Priority thresholds** — the minimum score needed to reach
  critical/high/medium (anything below "Medium" is "low").
- **Classifier safety net** — the confidence threshold below which the
  `needs-human-review` label kicks in.

Because the host's configuration was built with `reloadOnChange: true`
(the default), saving settings updates the file and the running Api picks
up the change automatically — no restart required.

## Tests

```bash
dotnet test
```

Covers the classifier (including the deliberately-tricky low-confidence
case), the priority engine and its individual heuristics, the label advisor
(including the new review-flag behaviour and its configurability), the
EF Core store (against the EF Core in-memory provider), JSON/GitHub
ingestion (the live-API failure path is tested against a deliberately
non-resolvable host, so it doesn't depend on your network), and a few
end-to-end API integration tests via `WebApplicationFactory` (with the
Postgres-backed DbContext swapped for the in-memory provider, so
`dotnet test` never needs Postgres running).

## If something doesn't compile

This was hand-written without a compiler in the loop, so treat the first
`dotnet build` as the real first draft review. Two places most likely to
need a small fix if package versions have moved on since:

- `Microsoft.FluentUI.AspNetCore.Components` version in
  `GithubIssueTriager.Web.csproj` — if the pinned version doesn't resolve,
  run `dotnet add package Microsoft.FluentUI.AspNetCore.Components` (no
  version) to pick up whatever is current, then fix any component API
  differences the compiler points out.
- `Npgsql.EntityFrameworkCore.PostgreSQL` version in
  `GithubIssueTriager.Api.csproj` — same idea, `dotnet add package <name>`
  without a version pulls the latest compatible with `net10.0`.

Paste me the exact build error if you'd like help fixing it.
