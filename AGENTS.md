# AGENTS.md

Pickarr is a single-user, self-hosted catalog browser. It syncs streaming catalogs through Watchmode, enriches them with TMDB, and sends titles to Sonarr and Radarr. There is no authentication.

Product behavior that is not obvious from the code is specified in `docs/documento-funzionale.md` and `docs/specifica-tecnica.md`. Do not invent scope beyond those documents and the code.

## Layout

| Path | Role |
| --- | --- |
| `src/Pickarr.Core` | Domain, DTOs, and abstractions. No EF, HTTP, or file I/O. |
| `src/Pickarr.Infrastructure` | EF Core SQLite, Watchmode, TMDB, Sonarr/Radarr, Hangfire jobs. |
| `src/Pickarr.Api` | Minimal API endpoints in `Endpoints/`. `Program.cs` wires startup. |
| `src/Pickarr.WebApp` | React 19, Vite, TanStack Query, React Router, i18next. |
| `tests/Pickarr.Core.Tests` | Unit tests. |
| `tests/Pickarr.Infrastructure.Tests` | Service tests with SQLite in memory and WireMock. |
| `tests/Pickarr.Integration.Tests` | API tests through `WebApplicationFactory`. |
| `src/Pickarr.Api/Dockerfile` | One image: API plus the built web app. |
| `docker-compose.yml` | linux/amd64 compose. Config volume is `/config`. |
| `Pickarr.slnx` | Solution at the repository root. |

Package versions live in `Directory.Packages.props`. Nullable reference types and warnings-as-errors are on for every C# project.

## Commands

```bash
# API, http://localhost:5000
dotnet run --project src/Pickarr.Api

# Vite, http://localhost:5173 (proxies /api and /health)
cd src/Pickarr.WebApp
npm ci
npm run dev

# Tests and checks
dotnet test
cd src/Pickarr.WebApp && npm test -- --run && npm run typecheck && npm run lint && npm run build
```

Filter a test project when the change is local, for example `dotnet test tests/Pickarr.Infrastructure.Tests --filter FullyQualifiedName~TitleQueryServiceTests`.

EF migrations are generated from `src/Pickarr.Infrastructure` with startup project `src/Pickarr.Api`, output `Persistence/Migrations`. Update `PickarrDbContextModelSnapshot.cs` with the migration.

## Local database and the running API

`Pickarr:DataPath` (env `Pickarr__DataPath`) is the SQLite and Data Protection directory. Default is `/config`. The checkout used for development keeps data in `.data` (`Pickarr__DataPath` in `src/Pickarr.Api/Properties/launchSettings.json`); pass that path explicitly so a debug session and the agent do not open two databases. Application settings from the environment use flat names (`WATCHMODE_API_KEY`, `SONARR_URL`, …); the legacy `Pickarr__Settings__…` form still works.

`Pickarr.Api` locks the DLLs in `src/Pickarr.Api/bin`. Stop that process before `dotnet build`, `dotnet test` of a project that references the API, or `dotnet ef`. A file-lock error is not a compile error.

Do not print Watchmode, TMDB, Sonarr, or Radarr secrets. They are stored encrypted by ASP.NET Data Protection under the data directory.

SQLite uses WAL. Do not put the data directory on a network share.

## Product boundaries

- The catalog page is `/catalog`. It is one provider, then one country. Quick filters are All, New, Leaving, and Recently removed.
- My list is `/list` and `GET /api/v1/favorites` (plus `/types` and `/genres`). It is cross-provider and cross-country. Do not put it back on the catalog as `view=favorites` or `favorite=true`. The catalog endpoints ignore that flag. `/catalog?view=favorites` redirects to `/list`.
- Title cards and the title dialog stay on the route that opened them.
- Type and genre options come from the titles in the current scope (catalog source, or the whole list).
- Series seasons are newest first. Episodes inside a season are oldest first. Adding a series to Sonarr sends only the selected seasons.
- A catalog source needs at least one country. On the Watchmode free plan the limit is `CatalogOffers.FreeCountryLimit` distinct countries (several providers in one country count once) and `CatalogOffers.FreeMonthlyQuota` credits per month. Leaving dates are a short preview, not a full calendar.
- The grid is empty until Sonarr and/or Radarr are configured. Series require Sonarr; movies require Radarr.
- UI copy is English and lives in `src/Pickarr.WebApp/src/i18n/locales/en.json`. Add keys there instead of hard-coding strings.

## Tests

Backend tests use xUnit and Shouldly. Infrastructure tests that call HTTP use WireMock fixtures under `tests/fixtures`.

`Pickarr.Integration.Tests` collection `"api"` shares one `ApiFactory` and one database. A test that assumes Sonarr/Radarr are unset, or that a table is empty, will fail if another test in that collection already wrote that state. Give that test its own collection and factory. The factory sets `Hangfire:Storage=Memory` and `Pickarr:Catalog:AutoInitialImport=false`; do not point tests at the developer database.

Frontend tests use Vitest and Testing Library (`jsdom`). Stub `localStorage` when a component reads the preferred catalog source. Assert behavior: a route change, a request argument, or an empty state, not only that a component rendered.

## Changes

- Keep Core free of Infrastructure types.
- Add or update a test for a bug fix in the same change.
- Do not commit, push, or edit git config unless the user asks.
- Do not hand-edit generated EF snapshots without the matching migration.
