# Developer guide

This guide is for contributors who want to change Pickarr. Product behavior that is not obvious from the code is specified in `docs/functional-spec.md`, `docs/technical-spec.md`, and `docs/recommendations.md`. Do not invent scope beyond those documents and the code.

Also read the repository root [`AGENTS.md`](../../AGENTS.md) and [`docs/AGENTS.md`](../AGENTS.md).

## Stack

| Area | Choice |
| --- | --- |
| Backend | .NET 10, C#, ASP.NET Core Minimal API |
| Persistence | EF Core + SQLite |
| Jobs | Hangfire (SQLite `jobs.db` by default; Memory for tests) |
| Frontend | React 19, Vite, TanStack Query, React Router, i18next, Tailwind |
| Tests | xUnit + Shouldly (backend); Vitest + Testing Library (frontend) |
| Packaging | Single Docker image (API + built SPA), linux/amd64 |

Package versions live in `Directory.Packages.props`. Nullable reference types and warnings-as-errors are on for every C# project.

## Repository layout

| Path | Role |
| --- | --- |
| `src/Pickarr.Core` | Domain, DTOs, and abstractions. No EF, HTTP, or file I/O. |
| `src/Pickarr.Infrastructure` | EF Core SQLite, Watchmode, TMDB, Sonarr/Radarr, Hangfire jobs. |
| `src/Pickarr.Api` | Minimal API endpoints in `Endpoints/`. `Program.cs` wires startup. |
| `src/Pickarr.WebApp` | React SPA. |
| `tests/Pickarr.Core.Tests` | Unit tests. |
| `tests/Pickarr.Infrastructure.Tests` | Service tests with SQLite in memory and WireMock. |
| `tests/Pickarr.Integration.Tests` | API tests through `WebApplicationFactory`. |
| `src/Pickarr.Api/Dockerfile` | One image: API plus the built web app. |
| `docker-compose.yml` | linux/amd64 compose. Config volume is `/config`. |
| `Pickarr.slnx` | Solution at the repository root. |
| `docs/` | Operator guides, developer guides, and English product specifications. |

**Dependency rule:** `Api → Core`, `Api → Infrastructure`, `Infrastructure → Core`. Core must stay free of Infrastructure types.

## Local development

Prerequisites: .NET 10 SDK, Node.js 24+.

```bash
# API, http://localhost:5000
dotnet run --project src/Pickarr.Api

# Vite, http://localhost:5173 (proxies /api and /health)
cd src/Pickarr.WebApp
npm ci
npm run dev
```

Local data path is `.data` via `src/Pickarr.Api/Properties/launchSettings.json` (`Pickarr__DataPath`). Pass that path explicitly so a debug session and another process do not open two databases.

```bash
dotnet test
cd src/Pickarr.WebApp && npm test -- --run && npm run typecheck && npm run lint && npm run build
```

Filter a test project when the change is local, for example:

```bash
dotnet test tests/Pickarr.Infrastructure.Tests --filter FullyQualifiedName~TitleQueryServiceTests
```

### File locks

`Pickarr.Api` locks the DLLs in `src/Pickarr.Api/bin`. Stop that process before `dotnet build`, `dotnet test` of a project that references the API, or `dotnet ef`. A file-lock error is not a compile error.

### Migrations

Generate EF migrations from `src/Pickarr.Infrastructure` with startup project `src/Pickarr.Api`, output `Persistence/Migrations`. Update `PickarrDbContextModelSnapshot.cs` with the migration. Do not hand-edit generated snapshots without the matching migration.

**Never** delete, reset, or replace the developer or production SQLite database to unblock schema work. Ship additional migrations instead. The database holds paid/limited catalog data and per-profile personalization.

## Tests

- Backend: xUnit and Shouldly. Infrastructure HTTP tests use WireMock fixtures under `tests/fixtures`.
- `Pickarr.Integration.Tests` collection `"api"` shares one `ApiFactory` and one database. A test that assumes Sonarr/Radarr are unset, or that a table is empty, will fail if another test in that collection already wrote that state. Give that test its own collection and factory. The factory sets `Hangfire:Storage=Memory` and `Pickarr:Catalog:AutoInitialImport=false`; do not point tests at the developer database.
- Frontend: Vitest and Testing Library (`jsdom`). Stub `localStorage` when a component reads the preferred catalog source or `pickarr.profileId`. Assert behavior (route change, request argument, empty state), not only that a component rendered.
- Add or update a test for a bug fix in the same change.

## UI copy

UI strings are English and live in `src/Pickarr.WebApp/src/i18n/locales/en.json`. Add keys there instead of hard-coding strings.

## Secrets

Do not print Watchmode, TMDB, Sonarr, or Radarr secrets. They are stored encrypted by ASP.NET Data Protection under the data path.

## CI and packaging

GitHub Actions (`.github/workflows/ci.yml`) runs backend/frontend lint and tests, then builds the Docker image. Push to GHCR happens from the default branch (`edge`, `X.Y.Z.N`) and from SemVer tags (`X.Y.Z`, `latest`). Platform: linux/amd64.

## Next steps

- [Architecture](architecture.md)
- [User documentation](../user/README.md)
- [docs/AGENTS.md](../AGENTS.md) — when to update documentation
