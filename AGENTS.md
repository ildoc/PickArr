# AGENTS.md

Pickarr is a self-hosted catalog browser for a trusted household install. It syncs streaming catalogs through Watchmode, enriches them with TMDB, and sends titles to Sonarr and Radarr. There is no authentication; up to 10 profiles share one install.

Product behavior that is not obvious from the code is specified in `docs/functional-spec.md`, `docs/technical-spec.md`, and `docs/recommendations.md`. Do not invent scope beyond those documents and the code.

Operator and contributor guides live under `docs/user/` and `docs/developer/` (index: `docs/README.md`). When a change is user- or contributor-facing, update those docs (and the specs when FR/BR/D- IDs change) in the same change — rules in `docs/AGENTS.md`.

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

The SQLite database is critical and must never be discarded, reset, deleted, or replaced to “fix” a schema problem. It holds catalog data obtained from paid/limited API calls (Watchmode and related enrichment) and per-profile ratings, favorites, watched state, and recommendation/taste personalization. Losing it means re-consuming Watchmode quota and wiping user personalization; that is an unacceptable risk.

Prefer extra or incremental EF migrations, careful schema evolution, and data-preserving fixes over any approach that drops tables, recreates the database, deletes `.data` / `/config`, or points the app at a fresh empty file. Do not suggest or run `EnsureDeleted`, wipe scripts, or “delete the DB and restart” as a remedy. Extra migrations are always preferable to data loss.

`Pickarr:DataPath` (env `Pickarr__DataPath`) is the SQLite and Data Protection directory. Default is `/config`. The checkout used for development keeps data in `.data` (`Pickarr__DataPath` in `src/Pickarr.Api/Properties/launchSettings.json`); pass that path explicitly so a debug session and the agent do not open two databases. Application settings from the environment use flat names (`WATCHMODE_API_KEY`, `SONARR_URL`, …); the legacy `Pickarr__Settings__…` form still works.

`Pickarr.Api` locks the DLLs in `src/Pickarr.Api/bin`. Stop that process before `dotnet build`, `dotnet test` of a project that references the API, or `dotnet ef`. A file-lock error is not a compile error.

Do not print Watchmode, TMDB, Sonarr, or Radarr secrets. They are stored encrypted by ASP.NET Data Protection under the data directory.

SQLite uses WAL. Do not put the data directory on a network share.

## Product boundaries

- There is no authentication. Up to 10 profiles share one install. The first is admin (`IsAdmin`); personal data from before multi-profile migrates onto that admin. The admin role is not transferred and the admin profile is not deleted.
- Profile choice is `/who`. The last id is `localStorage` `pickarr.profileId`. One profile skips the selector and enters the catalog; a remembered id that no longer exists opens `/who`. The header control always opens `/who`. After choice the SPA sends `X-Pickarr-Profile`.
- Non-admin profiles do not see Settings; `/settings` redirects to the catalog. Settings, jobs, and system APIs stay open. Only the admin creates or deletes other profiles; anyone edits their own name, color, and reco toggles (`useImplicit`, `excludeInLibrary`).
- Favorites, ratings, watched, recommendations, and taste state are per profile. Catalog metadata, embeddings, app settings, and Arr library rows stay global. A new Arr add with the header stores `ProfileId` on the audit row for that profile’s implicit signal; without the header the action is valid and the taste signal is not attributed.
- Under `N_eff < N0`, recommendations blend a household prior from other profiles’ Love/Like (cap 0.5 on the non-personal slot). At `N_eff ≥ N0` that term is zero. Details in `docs/recommendations.md`.
- The catalog page is `/catalog`. It is one provider, then one country. Quick filters are All, New, Leaving, and Recently removed. Remembered catalog source is `pickarr.sourceId.{profileId}`; the admin may once copy legacy `pickarr.sourceId`.
- My list is `/list` and `GET /api/v1/favorites` (plus `/types` and `/genres`). It is cross-provider and cross-country for the active profile. Do not put it back on the catalog as `view=favorites` or `favorite=true`. The catalog endpoints ignore that flag. `/catalog?view=favorites` redirects to `/list`.
- Title cards and the title dialog stay on the route that opened them.
- Type and genre options come from the titles in the current scope (catalog source, or the whole list).
- Series seasons are newest first. Episodes inside a season are oldest first. Adding a series to Sonarr sends only the selected seasons.
- A catalog source needs at least one country. On the Watchmode free plan the limit is `CatalogOffers.FreeCountryLimit` distinct countries (several providers in one country count once) and `CatalogOffers.FreeMonthlyQuota` credits per month. Leaving dates are a short preview, not a full calendar.
- The grid is empty until Sonarr and/or Radarr are configured. Series require Sonarr; movies require Radarr.
- UI copy is English and lives in `src/Pickarr.WebApp/src/i18n/locales/en.json`. Add keys there instead of hard-coding strings.

## Tests

Backend tests use xUnit and Shouldly. Infrastructure tests that call HTTP use WireMock fixtures under `tests/fixtures`.

`Pickarr.Integration.Tests` collection `"api"` shares one `ApiFactory` and one database. A test that assumes Sonarr/Radarr are unset, or that a table is empty, will fail if another test in that collection already wrote that state. Give that test its own collection and factory. The factory sets `Hangfire:Storage=Memory` and `Pickarr:Catalog:AutoInitialImport=false`; do not point tests at the developer database.

Frontend tests use Vitest and Testing Library (`jsdom`). Stub `localStorage` when a component reads the preferred catalog source or `pickarr.profileId`. Assert behavior: a route change, a request argument, or an empty state, not only that a component rendered.

## Changes

- Keep Core free of Infrastructure types.
- Add or update a test for a bug fix in the same change.
- Update docs under `docs/` (guides and English specs when FR/BR/D- IDs change) for substantial product or contributor-facing changes; see `docs/AGENTS.md`.
- Do not commit, push, or edit git config unless the user asks.
- Do not hand-edit generated EF snapshots without the matching migration.
- Never delete, reset, or replace the developer or production SQLite database to unblock schema work; ship additional migrations instead. Data loss is not an acceptable tradeoff.
