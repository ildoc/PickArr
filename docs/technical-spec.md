# Pickarr — Technical specification

| | |
|---|---|
| **Version** | 0.3 |
| **Date** | 2026-11-10 |
| **Language** | English |
| **Linked documents** | `functional-spec.md` (FR-/BR-/D- catalog); `recommendations.md` (FR-REC-*, D-REC-*, BR-REC-*) |

---

## Table of contents

1. Introduction and conventions
2. Technology stack
3. Architecture
4. Data model
5. External integrations
6. Jobs and synchronization
7. Domain logic
8. API contracts
9. Frontend
10. Security
11. Observability
12. Configuration
13. Test strategy
14. Build, packaging, and CI/CD
15. Multi-service extension
16. Error handling
17. Technical risks and initial spikes
18. Recommended implementation order
19. Appendices

---

## 1. Introduction and conventions

- **Goal:** turn the functional document into implementable technical decisions.
- **Verification status:**
  - Figures for Watchmode plans, credits, and limits come from public documentation consulted on 2026-11-03.
  - Exact Watchmode parameter names and response shapes, as well as Sonarr/Radarr payloads, are reported **from memory and must be validated** via the spikes (§17) against Watchmode’s OpenAPI documentation and the `/swagger` page of the Sonarr/Radarr instances in use. Where a fact still needs confirmation it is marked **(to verify)**.
- **Conventions:**
  - Date/time in UTC in the DB and APIs (ISO 8601). The `TZ` timezone is only used to interpret cron expressions.
  - Enumerations serialized as strings in `camelCase` in APIs and in `PascalCase` in the DB.
  - JSON in `camelCase`. Errors in RFC 7807 format (`application/problem+json`).
  - Provisional project name: `Pickarr` (namespace `Pickarr.*`).

---

## 2. Technology stack

Versions are the stable ones at project start, to be pinned in `Directory.Packages.props` and `package-lock.json`.

### Backend

| Area | Choice |
|---|---|
| Runtime | .NET 10 (LTS), C# 14 |
| Web | ASP.NET Core **Minimal API**, endpoint groups per feature |
| Persistence | EF Core 10 + SQLite (`Microsoft.EntityFrameworkCore.Sqlite`), EF migrations |
| Jobs | Hangfire (`Hangfire.AspNetCore`) with SQLite storage on a **separate file** (community package, see SP-4) |
| HTTP client | Typed `IHttpClientFactory` + `Microsoft.Extensions.Http.Resilience` (retry, circuit breaker, rate limiter) |
| Validation | FluentValidation (or manual validation with `IValidatableObject`) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` (native) + Scalar UI on `/docs` |
| Logging | Serilog (JSON console + rotating file) |
| Secrets | ASP.NET Core Data Protection (persisted key ring) |
| Local ML | `Microsoft.ML.OnnxRuntime` + `Microsoft.ML.Tokenizers` (embedding); `Microsoft.ML` (level-3 logistic, optional/gated); `System.Numerics.Tensors` |
| Test | xUnit, Shouldly or AwesomeAssertions, NSubstitute, WireMock.Net, `WebApplicationFactory` |

### Frontend

| Area | Choice |
|---|---|
| Build | Vite, TypeScript `strict` |
| UI | React 19, Tailwind CSS, Radix/shadcn components (dialog, select, tooltip) |
| Data | TanStack Query v5 (`useInfiniteQuery` for the grid) |
| Routing | React Router (modal route for the title card) |
| Form | react-hook-form + zod |
| Virtualization | `@tanstack/react-virtual` |
| i18n | `i18next` + `react-i18next` (fallback `en`) |
| API client | `openapi-typescript` + `openapi-fetch`, generated from the backend OpenAPI |
| Test | Vitest, Testing Library, MSW, Playwright (e2e) |
| Quality | ESLint, Prettier |

---

## 3. Architecture

### 3.1 Runtime topology

One container, one process:

```
┌──────────────────────── Pickarr container ────────────────────────┐
│  Kestrel :8080                                                       │
│   ├─ /            → React SPA (static files in wwwroot)              │
│   ├─ /api/v1/*    → Minimal API                                      │
│   ├─ /docs        → OpenAPI + Scalar                                 │
│   └─ /health      → health check                                     │
│  Hangfire Server (in-process): queues "default", "sync", "arr", "ml" │
│    (second server WorkerCount=1 for "ml" queue only)                 │
│  SQLite: /config/Pickarr.db   /config/jobs.db   /config/keys/        │
│  ONNX models: /config/models (hash verified on download)             │
└──────────────────────────────────────────────────────────────────────┘
        │               │               │               │
     Watchmode         TMDB       Sonarr (v4)      Radarr (v5)
```

### 3.2 Repository structure (monorepo)

```
Pickarr/
├─ Pickarr.slnx
├─ Directory.Build.props
├─ Directory.Packages.props
├─ docker-compose.yml
├─ src/
│  ├─ Pickarr.Core/              # domain + use cases + abstractions (no infrastructure dependency)
│  ├─ Pickarr.Infrastructure/    # EF, external clients, jobs, security
│  ├─ Pickarr.Api/               # host, endpoints, DTOs, DI composition, Dockerfile
│  └─ Pickarr.WebApp/            # SPA (see §9)
├─ tests/
│  ├─ Pickarr.Core.Tests/
│  ├─ Pickarr.Infrastructure.Tests/
│  └─ Pickarr.Integration.Tests/
├─ docs/
│  ├─ README.md / AGENTS.md
│  ├─ user/  developer/  pickarr-vs-seerr.md
│  ├─ functional-spec.md
│  ├─ technical-spec.md
│  └─ recommendations.md
├─ .github/workflows/ci.yml
├─ CHANGELOG.md
└─ README.md
```

**Dependency rule:** `Api → Core`, `Api → Infrastructure`, `Infrastructure → Core`. `Core` does not know EF, HTTP, or Hangfire.

### 3.3 Internal structure of `Core`

```
Core/
├─ Domain/            Entities, Enums, ValueObjects, Rules (BR-xx as pure functions)
├─ Abstractions/      ICatalogProvider, IMetadataProvider, IArrClient, ICreditBudget, IClock, ISettingsStore,
│                     IRatingService, IRecoBuildService, IRecoQueryService, IRecoEvalService, …
├─ Recommendations/   pure algorithm: prior, signals, sparse, kNN, MMR, explore, eval metrics, L3 features
├─ Catalog/           TitleQueryModels (rating field in DTOs), membership helpers
├─ Metadata/          TMDB DTOs (keywords, credits, created_by)
├─ Settings/          SettingsService, RecoSettingsDto
└─ …
```

EF persistence, HTTP clients, Hangfire, ONNX, and L3 training (`Microsoft.ML`) live in **Infrastructure** (e.g. `EnrichmentService`, `RecoBuildService`).
`Api` endpoints are thin: they validate input, call a service, and map to DTOs.

### 3.4 Registration and composition

- `Program.cs`: `AddPickarrCore()`, `AddPickarrInfrastructure(config)`, `AddPickarrApi()`.
- Endpoints are defined in `static` classes with a `MapXxxEndpoints(this IEndpointRouteBuilder)` method per feature: `Catalog`, `Titles`, `Favorites`, `Ratings`, `Recommendations`, `RecoAdmin`, `Arr`, `Profiles`, `Settings`, `Admin`, `Webhooks`, `System`.
- The SPA fallback (`MapFallbackToFile("index.html")`) excludes `/api`, `/docs`, `/health`.

---

## 4. Data model

Database `Pickarr.db` (SQLite). Conventions:

- Surrogate integer autoincrement `Id` keys, except where noted.
- Timestamps as UTC `DateTime` with a value converter (ISO 8601 text), to allow sorting and comparison. Do not use `DateTimeOffset` with SQLite.
- Enums stored as strings (`HasConversion<string>()`).
- Connection-open pragmas: `journal_mode=WAL`, `synchronous=NORMAL`, `foreign_keys=ON`, `busy_timeout=5000`.

### 4.1 Tables

**`Providers`** — streaming services

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| Key | text UNIQUE | e.g. `netflix` |
| DisplayName | text | |
| ProviderType | text | e.g. `watchmode` (selector for the `ICatalogProvider` implementation) |
| ExternalId | text null | for Watchmode, the `source_id` (Netflix = 203) |
| IsEnabled | bool | |
| CreatedAt | datetime | |

**`CatalogSources`** — service + country

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| ProviderId | int FK | |
| CountryCode | text(2) | ISO 3166-1 alpha-2 uppercase |
| MetadataLanguage | text | e.g. `it-IT`, `en-US` |
| IsEnabled | bool | |
| IncrementalCron | text | default `0 4 * * *` |
| FullSyncCron | text | default `0 3 * * 0` |
| IncrementalPages | int | default 3 |
| BaselineCompletedAt | datetime null | set on the first successful full sync |
| LastIncrementalAt, LastFullAt | datetime null | |
| LastFullTotalPages | int null | estimate for budget |

Unique index `(ProviderId, CountryCode)`.

**`Titles`**

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| WatchmodeId | int UNIQUE null | Watchmode id |
| TmdbId | int null | |
| TmdbType | text null | `movie` / `tv` |
| ImdbId | text null | `tt…` |
| TvdbId | int null | series only |
| MediaType | text | `Movie` / `Series` (derived from TMDB type, or from Watchmode if TMDB is absent) |
| Category | text | `Movie` / `Series` / `StandUp` |
| OriginalTitle | text null | |
| ReleaseYear | int null | |
| RuntimeMinutes | int null | movies |
| SeasonCount, EpisodeCount | int null | series |
| VoteTmdb | real null | |
| VoteTmdbCount | int null | |
| ImdbRating | real null | optional (OMDb) |
| Popularity | real null | |
| OriginalLanguage | text null | |
| PosterPath, BackdropPath | text null | neutral TMDB paths |
| MetadataStatus | text | `Pending` / `Ready` / `Unmapped` / `Failed` |
| MetadataFetchedAt | datetime null | |
| MetadataAttempts | int | |
| CreatedAt, UpdatedAt | datetime | |

Indexes: partial unique `(TmdbType, TmdbId)` (`WHERE TmdbId IS NOT NULL`), `TvdbId`, `ImdbId`, `(MetadataStatus)`, `(ReleaseYear)`.

**`TitleLocalizations`** — PK `(TitleId, Language)`

| Column | Type | Notes |
|---|---|---|
| TitleId, Language | PK | BCP 47 language (`it-IT`, `en-US`) |
| Name | text | |
| NameNormalized | text | lowercase, no diacritics, for search |
| Overview | text null | |
| Tagline | text null | |
| FetchedAt | datetime | |

Index on `NameNormalized`.

**`Genres`** (PK = TMDB id) and **`GenreLocalizations`** `(GenreId, Language, Name)`; **`TitleGenres`** `(TitleId, GenreId)`.

**`Availabilities`** — title/source relation

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| TitleId | int FK | |
| CatalogSourceId | int FK | |
| FirstSeenAt | datetime | first sighting |
| LastSeenAt | datetime | last confirmation in a read |
| AddedAt | datetime null | add date; null if baseline |
| IsBaseline | bool | |
| MissedFullScans | int | count of absences in full syncs (BR-05) |
| RemovedAt | datetime null | |
| LeavingDate | date null | known expiry date |

Unique index `(TitleId, CatalogSourceId)`; index `(CatalogSourceId, RemovedAt, AddedAt)`.

**`AvailabilityEvents`**

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| AvailabilityId | int FK | |
| Type | text | `Added`, `Removed`, `Readded`, `LeavingAnnounced`, `LeavingCleared` |
| OccurredAt | datetime | |
| SyncRunId | guid null | |
| Details | text null | JSON |

**`Profiles`** — up to 10; `Id`, `Name` (1–40, unique case-insensitive), `Color` (palette: `blue`, `teal`, `green`, `yellow`, `orange`, `red`, `pink`, `purple`), `IsAdmin` (unique index on `IsAdmin = 1`), `UseImplicit`, `ExcludeInLibrary`, `CreatedAt`. Seed/migration: one admin “Admin” / `blue`; preexisting personal data receives that admin’s `ProfileId`.

**`Favorites`** — PK `(ProfileId, TitleId)`, `CreatedAt`.

**`Watched`** — PK `(ProfileId, TitleId)`, `CreatedAt` (hide / already watched).

**`Ratings`** — PK `(ProfileId, TitleId)`; `Value` (`Love`/`Like`/`Dislike`/`NotInterested`), `RatedAt`.

**`RatingEvents`** — history: `Id`, `ProfileId`, `TitleId`, `Value` (null = clear), `At`, `Origin` (`Ui`/`Onboarding`/`Import`).

**`Keywords`** (PK = TMDB id, `Name`), **`TitleKeywords`** `(TitleId, KeywordId)`.

**`People`** (PK = TMDB id, `Name`), **`TitleCredits`** `(TitleId, PersonId, Role, Order)` with `Role` ∈ `Cast`/`Director`/`Creator`.

**`TitleEmbeddings`** — PK `(TitleId, ModelId)`; `Vector` BLOB (float32 LE), `TextHash`, `UpdatedAt`. Global (not per profile).

**`Recommendations`** — PK `(ProfileId, Version, TitleId)`; `Score`, `Personal` null, `Prior`, `Rank`, `ReasonsJson`. Index `(ProfileId, Version, Score DESC)`.

**`RecoState`** — one row per profile (unique `ProfileId`): `CurrentVersion`, `BuiltAt`, `RatingCount`, `EffectiveN`, `Stage`, `ProfileJson`, `LastError`, … `ModelStatus`, `ModelId`, `EmbeddingCoverage`, `LastEvalJson`, `LastEvalAt`, `Level3Active` are read and written only on the admin row.

**`RecoEventLog`** (only if `reco.events.enabled`): `Id`, `ProfileId`, `At`, `TitleId`, `Event` (`Shown`/`Opened`/`Liked`/`Disliked`/`AddedToArr`), `Version`, `Rank`, `Scope`.

**`ArrLibraryItems`** — Sonarr/Radarr library cache

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| Kind | text | `Sonarr` / `Radarr` |
| ArrId | int | id in the instance |
| TmdbId, TvdbId | int null | |
| ImdbId | text null | |
| TitleId | int FK null | match with the catalog (null if not present) |
| ArrTitle | text | |
| Year | int null | |
| Monitored | bool | |
| State | text | `Missing` / `Downloading` / `Downloaded` / `Partial` |
| QueueProgress | real null | 0–100 |
| SizeOnDisk | bigint | |
| Path | text null | |
| EpisodeFileCount, EpisodeCount, TotalEpisodeCount | int null | series |
| HasFile | bool null | movies |
| IsAvailable | bool null | movies (Radarr `isAvailable`) |
| SyncedAt | datetime | |

Unique index `(Kind, ArrId)`; indexes on `TmdbId`, `TvdbId`, `TitleId`.

**`ArrSeasonStates`** — PK `(ArrLibraryItemId, SeasonNumber)`: `Monitored`, `EpisodeFileCount`, `EpisodeCount`, `TotalEpisodeCount`, `SizeOnDisk`, `State`, `QueueProgress`.

**`ArrAuditLog`**: `Id`, `At`, `Action` (`Add`/`Delete`), `Kind`, `ArrId`, `TitleId` null, `ProfileId` null, `Options` (JSON), `Outcome`, `Error` null. `ArrLibraryItems` stays global (no `ProfileId`). An Add with header `X-Pickarr-Profile` stores the `ProfileId` and feeds only that profile; without the header the action is valid and the taste signal is not attributed. Migrated history goes to the admin.

**`SyncRuns`**

| Column | Type | Notes |
|---|---|---|
| Id | guid PK | |
| Kind | text | `CatalogIncremental`, `CatalogFull`, `Enrichment`, `ArrSync`, `Leaving`, `RecoFeature`, `RecoEmbedding`, `RecoBuild`, `RecoEval`, `RecoModelDownload` |
| CatalogSourceId | int null | |
| TriggeredBy | text | `Schedule` / `Manual` / `Resume` / `Webhook` |
| StartedAt, FinishedAt | datetime / null | |
| Status | text | `Running`, `Succeeded`, `PartiallySucceeded`, `Paused`, `NeedsReview`, `Failed`, `Cancelled` |
| PagesFetched, CreditsUsed | int | |
| TitlesAdded, TitlesUpdated, TitlesRemoved | int | |
| ErrorMessage | text null | |
| CheckpointJson | text null | cursor for resume |

Finished rows are pruned to `jobs.runHistoryLimit` (default 10, clamp 1–100) per `(Kind, CatalogSourceId)` after each terminal status; `Running` rows are kept. Related `ApiUsage` rows for deleted run ids are removed.

**`ApiUsage`**: `Id`, `Service` (`watchmode`/`tmdb`/`omdb`), `Endpoint`, `Timestamp`, `Credits` (int), `QuotaUsed` (int null), `QuotaLimit` (int null), `StatusCode`, `SyncRunId` null. Index on `(Service, Timestamp)`.

**`Settings`**: `Key` PK, `ValueJson`, `IsSecret` (value encrypted with Data Protection), `UpdatedAt`.

### 4.2 Job database

`jobs.db` is a distinct file, managed entirely by Hangfire, to avoid lock contention with `Pickarr.db`.

### 4.3 Migrations and seed

- EF migrations are applied at startup (`Database.MigrateAsync()`), before jobs start.
- Idempotent seed: `Providers(netflix)`, `CatalogSources(netflix, IT, it-IT)` and `(netflix, US, en-US)` (disabled until the Watchmode key is present), default settings.
- Before every destructive migration a backup copy of the file is created (`Pickarr.db.bak-<version>`).

### 4.4 Search

Phase 1: `NameNormalized LIKE '%term%'` on `TitleLocalizations` (source language + `en-US`) and normalized `Titles.OriginalTitle`. With a few thousand rows this is adequate. If needed (measure against NFR-01), move to an **FTS5** virtual table maintained by triggers. The search API does not change.

---

## 5. External integrations

Pattern common to all clients:

- Typed client with `HttpClient` from `IHttpClientFactory`, base address and auth headers configured at request time (reading current settings, so no restart).
- Resilience pipeline: per-attempt timeout, **retry** with exponential backoff and jitter on 5xx/timeout/429 (honoring `Retry-After`), **circuit breaker**, outbound **rate limiter**.
- No secrets in logs: keys travel in headers, not in query string, where the API allows it.
- Every Watchmode and TMDB call goes through a `DelegatingHandler` that writes to `ApiUsage`.

### 5.1 Watchmode

- **Base URL:** `https://api.watchmode.com/v1`
- **Authentication:** header `X-API-Key` (docs also allow the `apiKey` parameter; the header is used so the key is not exposed in URLs).
- **Credits:** every response reports `X-Account-Quota` and `X-Account-Quota-Used`; some calls cost more than 1 credit **(measure, SP-1)**.
- **Rate limit:** outbound limiter default 1 request/second (configurable), to stay conservative.

Endpoints used:

| Use | Endpoint | Notes |
|---|---|---|
| Service list | `GET /sources/` (filters `regions`, `types`) | Find the service id (Netflix = 203). Result stored in `Providers`. |
| Country list | `GET /regions/` | Validate countries and data quality level. |
| Title list | `GET /list-titles/` | Heart of sync. Parameters **(to verify)**: `source_ids`, `regions`, `types` (e.g. `movie,tv_series,tv_miniseries,tv_special`), `sort_by`, `page`, `limit` (max 250). |
| Expiries | “leaving dates” endpoint (`getTitleLeavingDates`) | Free: preview of the next 10 per country. Path and parameters to verify. |
| Quota | `GET /status/` | Read only if costs allow; otherwise use headers. |
| Change feed (paid plan) | `GET /changes/titles_sources_changed`, `/changes/new_titles` | Used only with `WatchmodePlan = Paid` (FR-CAT-13). 1 credit per page. |

Expected shape of the `list-titles` response **(to verify)**:

```json
{
  "titles": [
    { "id": 1234567, "title": "Example", "year": 2024,
      "imdb_id": "tt1234567", "tmdb_id": 987654, "tmdb_type": "movie",
      "type": "movie" }
  ],
  "page": 1, "total_results": 6120, "total_pages": 25
}
```

Watchmode type mapping to `MediaType`:

| Watchmode `type` / `tmdb_type` | MediaType |
|---|---|
| `movie` / `movie` | Movie |
| `tv_series`, `tv_miniseries` / `tv` | Series |
| `tv_special` | based on `tmdb_type` (`movie` → Movie, `tv` → Series) |

### 5.2 TMDB

- **Base URL:** `https://api.themoviedb.org/3`. **Auth:** `Authorization: Bearer <read access token>`.
- **Rate limit:** outbound limiter default 20 requests/second.

| Use | Endpoint |
|---|---|
| Movie detail | `GET /movie/{id}?language={lang}&append_to_response=external_ids,keywords` |
| Series detail | `GET /tv/{id}?language={lang}&append_to_response=external_ids,keywords` |
| Mapping from IMDb | `GET /find/{imdb_id}?external_source=imdb_id` (fallback if `tmdb_id` is missing) |
| Genres | `GET /genre/movie/list?language={lang}` and `GET /genre/tv/list?language={lang}` |
| Image configuration | `GET /configuration` (base URL and sizes, cache 7 days) |

Rules:

- **Language fallback (BR-15):** call with the source language. If `overview` or title are empty, repeat with `en-US` and store **two** localizations.
- **TVDB id:** from `external_ids.tvdb_id` for series. `imdb_id` from `external_ids` (series) or from the detail (movies).
- **Stand-up:** UI category from TMDB keyword **names** that normalize to `standupcomedy` (e.g. “stand-up comedy”). No hardcoded TMDB keyword ids (ids drift / collide). Watchmode genre preference (D-06) is not wired yet.
- **Image URLs:** built by the backend (`secure_base_url` + size + `PosterPath`); suggested `w342` for the grid, `w500` for the card, `w780` for the backdrop.
- **Attribution:** required logo and wording shown in the UI (FR-MET-07).

### 5.3 OMDb (optional, FR-MET-04)

`GET https://www.omdbapi.com/?i={imdb_id}&apikey={key}` → `imdbRating`. Run only if the key is set, with a configurable daily limit (OMDb’s free plan has a daily cap), at low priority, and only for titles with `ImdbId`.

### 5.4 Sonarr (API v3, header `X-Api-Key`) — verify with `/swagger`

Base: `{url}/api/v3`.

| Use | Call |
|---|---|
| Connection test, version | `GET /system/status` |
| Dialog options | `GET /qualityprofile`, `GET /rootfolder`, `GET /tag` |
| Library (with per-season statistics) | `GET /series` |
| Single series | `GET /series/{id}`; by TVDB id `GET /series?tvdbId={id}` |
| Lookup before add | `GET /series/lookup?term=tvdb:{tvdbId}` |
| Add | `POST /series` |
| Download queue | `GET /queue?page=1&pageSize=…&includeSeries=true` (paginated) |
| Search | `POST /command` with `{ "name": "SeriesSearch", "seriesId": id }` |
| Delete | `DELETE /series/{id}?deleteFiles={bool}&addImportExclusion={bool}` |

Body of `POST /series`: start from the object returned by lookup (contains `title`, `titleSlug`, `images`, `seasons`, `tvdbId`, …) and overwrite the fields chosen by the user:

```json
{
  "tvdbId": 121361,
  "title": "…",
  "qualityProfileId": 4,
  "rootFolderPath": "/tv",
  "seriesType": "standard",
  "seasonFolder": true,
  "monitored": true,
  "monitorNewItems": "all",
  "tags": [1],
  "addOptions": {
    "monitor": "all",
    "searchForMissingEpisodes": true,
    "searchForCutoffUnmetEpisodes": false
  }
}
```

Values of `addOptions.monitor` **(to verify)**: `all`, `future`, `missing`, `existing`, `firstSeason`, `lastSeason`, `latestSeason`, `pilot`, `recent`, `none`. Values of `seriesType`: `standard`, `daily`, `anime`.

### 5.5 Radarr (API v3, header `X-Api-Key`) — verify with `/swagger`

| Use | Call |
|---|---|
| Connection test, version | `GET /system/status` |
| Dialog options | `GET /qualityprofile`, `GET /rootfolder`, `GET /tag` |
| Library | `GET /movie` |
| By TMDB id | `GET /movie?tmdbId={id}` |
| Lookup | `GET /movie/lookup?term=tmdb:{tmdbId}` |
| Add | `POST /movie` |
| Queue | `GET /queue?page=1&pageSize=…&includeMovie=true` |
| Search | `POST /command` with `{ "name": "MoviesSearch", "movieIds": [id] }` |
| Delete | `DELETE /movie/{id}?deleteFiles={bool}&addImportExclusion={bool}` |

Body of `POST /movie`:

```json
{
  "tmdbId": 603,
  "title": "…",
  "qualityProfileId": 4,
  "rootFolderPath": "/movies",
  "monitored": true,
  "minimumAvailability": "released",
  "tags": [],
  "addOptions": { "monitor": "movieOnly", "searchForMovie": true }
}
```

`minimumAvailability`: `announced`, `inCinemas`, `released`.

### 5.6 `IArrClient` interface

A single abstraction with two implementations (`SonarrClient`, `RadarrClient`) avoids duplication in services:

```csharp
public interface IArrClient
{
    ArrKind Kind { get; }
    Task<ArrSystemInfo> GetSystemInfoAsync(CancellationToken ct);
    Task<ArrOptions> GetOptionsAsync(CancellationToken ct);              // profiles, root folder, tags
    Task<IReadOnlyList<ArrItemSnapshot>> GetLibraryAsync(CancellationToken ct); // includes statistics and seasons
    Task<IReadOnlyList<ArrQueueEntry>> GetQueueAsync(CancellationToken ct);
    Task<ArrItemSnapshot?> FindByExternalIdAsync(ArrExternalId id, CancellationToken ct);
    Task<ArrItemSnapshot> AddAsync(ArrAddRequest request, CancellationToken ct);
    Task SearchAsync(int arrId, CancellationToken ct);
    Task DeleteAsync(int arrId, bool deleteFiles, bool addImportExclusion, CancellationToken ct);
}
```

`ArrAddRequest` is a discriminated union: `SonarrAddRequest` and `RadarrAddRequest` with the specific fields.

### 5.7 Inbound webhooks

Configured in Sonarr/Radarr (*Settings → Connect → Webhook*), method `POST`.

- URL: `{baseUrl}/api/v1/webhooks/sonarr?token={secret}` and `.../radarr?token={secret}`. The token is generated by the app (§12) and shown in settings.
- Events to enable: Grab, Import/Download, Rename, Series/Movie Add, Series/Movie Delete, Episode/Movie File Delete.
- The backend **does not trust the payload for state**: it only uses it to identify the item (`series.id` / `movie.id`, `tvdbId` / `tmdbId`) and enqueues an `ArrRefreshItemJob` that queries the instance. `Test` event: responds 200.
- Debounce: multiple events on the same item within 5 seconds produce a single refresh.

---

## 6. Jobs and synchronization

### 6.1 Job list

| Job | Trigger | Queue | Concurrency | Retry |
|---|---|---|---|---|
| `CatalogIncrementalSyncJob(sourceId)` | cron per source (default `0 4 * * *`) | sync | 1 per source | handled internally |
| `CatalogFullSyncJob(sourceId)` | cron per source (default `0 3 * * 0`) | sync | 1 per source | handled internally |
| `CatalogResumeJob` | daily (and after monthly reset) | sync | 1 | — |
| `EnrichmentJob` | enqueued after syncs + every hour | default | 1 | 3 with backoff |
| `MetadataRefreshJob` | daily, batches | default | 1 | 3 |
| `LeavingSyncJob(country)` | daily | sync | 1 per country (one Watchmode call, fan-out to sources) | 2 |
| `ArrSyncJob(kind)` | interval (default 15 min) | arr | 1 per kind | 2 |
| `ArrRefreshItemJob(kind, arrId)` | after add/delete/webhook | arr | per item | 3 |
| `RecoFeatureJob` | after enrichment / hourly | ml | 1 (`DisableConcurrentExecution`) | 2 |
| `RecoEmbeddingJob` | after feature / manual | ml | 1 | 2 |
| `RecoBuildJob` | rating (debounce 30 s), after embedding, nightly, manual | ml | 1 | 2 |
| `RecoEvalJob` | manual | ml | 1 | 1 |
| `RecoModelDownloadJob` | manual (download or detect) | ml | 1 | 1 |

`ml` queue: second `AddHangfireServer` with `WorkerCount = 1` and `Queues = ["ml"]`, so long embedding/build work does not saturate `sync`/`arr`.

Registration:

- At startup and on every settings change, a `ScheduleReconciler` reads `CatalogSources` and settings and registers/updates/removes Hangfire recurring jobs (`RecurringJob.AddOrUpdate` / `RemoveIfExists`) with local `TimeZoneInfo`.
- Because schedules are recreated from the application DB, Hangfire storage holds only transient state (queues, retries): losing `jobs.db` does not lose configuration.
- Jobs are classes with `ExecuteAsync(…, CancellationToken)` invoked via `IBackgroundJobClient`; logic lives in `Core` services, not in the jobs.
- `[DisableConcurrentExecution]` (or a lock on `SyncRuns` in `Running` state for `(Kind, CatalogSourceId)`) prevents concurrent runs (UC-10).

### 6.2 Credit budget (`ICreditBudget`)

Parameters (settings): `MonthlyQuota` (default 2500), `ReservePercent` (default 10), `MaxCreditsPerRun` (default 80), `WatchmodePlan` (`Free`/`Paid`).

```
used        = last known X-Account-Quota-Used (otherwise sum of ApiUsage for the billing month)
quota       = X-Account-Quota if known, otherwise MonthlyQuota
reserve     = ceil(quota * ReservePercent / 100)
spendable   = max(0, quota - used - reserve)          // for scheduled jobs
spendableManual = max(0, quota - used)                // for manual operations

TryReserve(cost, isManual):
    return cost <= (isManual ? spendableManual : spendable)
        && runCredits + cost <= MaxCreditsPerRun
```

- The check happens **before every** Watchmode call; after the response, `used` is updated from the real header.
- If `TryReserve` fails, the run moves to `Paused` saving the checkpoint (BR-14).
- The billing cycle starts from the signup day, not the first of the month: the `BillingDayOfMonth` setting (default 1) allows alignment; in any case headers are the source of truth.
- Month-end projection (FR-ADM-04): `used / daysElapsed * totalDays`.

**Consumption estimate** (to confirm with SP-1): with `limit=250`, a full read of one source needs `ceil(titles/250)` pages, so the order of magnitude is a few tens of credits per source on each full sync. With IT+US, weekly full sync and daily incremental of 3 pages per source, expected monthly consumption is a few hundred credits, well under 2,500 (if each page costs 1 credit).

### 6.3 Incremental sync (`ListPagesStrategy` strategy)

```
run = StartRun(CatalogIncremental, source)
pages = source.IncrementalPages
for page in 1..pages:
    if !budget.TryReserve(cost): run.Pause(checkpoint = page); return
    result = provider.List(source, sort: release_date_desc, page)
    Upsert(result.Entries, run)          // see 6.5
run.Succeed()
EnqueueEnrichment()
```

Known limit (R-02): an old title added to Netflix appears at the top only if it has a recent release date; others are detected by the full sync. On a paid plan (`WatchmodePlan = Paid`) use `ChangeFeedStrategy`: `changes/titles_sources_changed` for the country and update only the involved ids. The strategy is chosen by a factory based on the plan; the interface is `ICatalogSyncStrategy`.

### 6.4 Full sync

```
run = StartRun(CatalogFull, source, checkpoint = resume?.checkpoint)
for page in (checkpoint.page ?? 1)..total_pages:
    if !budget.TryReserve(cost): run.Pause(checkpoint = { page, seenIds? }); return
    result = provider.List(source, sort: stable, page)
    Upsert(result.Entries, run)
    run.Checkpoint(page + 1)
// read completed
unseen = active Availabilities for the source with LastSeenAt < run.StartedAt
guard  = unseen.Count / active.Count > MaxRemovalRatio (default 0.10)
if guard: run.Status = NeedsReview; return        // no removals (FR-CAT-10)
foreach a in unseen:
    a.MissedFullScans++
    if a.MissedFullScans >= RemovalConfirmations (default 2):
        a.RemovedAt = now; Event(Removed)
// seen titles reset MissedFullScans in Upsert
if source.BaselineCompletedAt == null:
    mark as IsBaseline all availabilities created in this run; source.BaselineCompletedAt = now
source.LastFullAt = now; source.LastFullTotalPages = total_pages
run.Succeed()
```

Notes:

- **Resume:** the checkpoint holds the next page. `LastSeenAt` updated by upserts from all runs (including incremental) after `run.StartedAt` avoids spurious removals of titles seen in the meantime.
- **Stable sort:** use a sort that does not depend on release date or popularity (e.g. by title or by id), if supported **(SP-1)**. In any case the double confirmation (BR-05) absorbs page shifts.
- **Interrupted run:** if the container stops, on restart orphan `Running` runs are marked `Paused` with the last checkpoint.
- **Baseline:** until it is completed, `AddedAt` stays null, no title is “new”, and removals are not computed.

### 6.5 Catalog upsert (`CatalogUpserter`)

For each page entry, in short per-page transactions:

1. Look up the `Title` by `WatchmodeId`, then by `(TmdbType, TmdbId)`, then by `ImdbId`. If it does not exist, create it with `MetadataStatus = Pending` when a `tmdb_id` or `ImdbId` is present (enrichment resolves IMDb via TMDB `/find`); otherwise `Unmapped`. When linking by IMDb, keep the survivor’s `WatchmodeId` if already set (unique).
2. Look up the `Availability (TitleId, SourceId)`:
   - absent → create with `FirstSeenAt = LastSeenAt = now`; if baseline is completed `AddedAt = now`, otherwise `IsBaseline = true` and `AddedAt = null`; event `Added`;
   - present and active → `LastSeenAt = now`, `MissedFullScans = 0`;
   - present and removed → `RemovedAt = null`, `AddedAt = now`, `IsBaseline = false`, `MissedFullScans = 0`; event `Readded`.
3. Update the run counters.

Idempotent operation: re-running the same page does not change state (except `LastSeenAt`).

### 6.6 Enrichment (`EnrichmentJob`)

```
batch = Titles with MetadataStatus in (Pending, Failed with attempts < 5) ORDER BY CreatedAt LIMIT 100
foreach t in batch (limited parallelism, e.g. 4):
    details = tmdb.Get(t.TmdbType, t.TmdbId, lang = source language)
    if 404: retry the other type (movie↔tv) and correct TmdbType if there is no conflict
    if still absent: MetadataStatus = Unmapped (unknown id, not an API-key error)
    if overview/name empty: details_en = tmdb.Get(..., "en-US")
    save Title (year, runtime/seasons, votes, ids, image paths, category), localizations, genres
    t.MetadataStatus = Ready
on network/auth error: MetadataAttempts++, MetadataStatus = Failed
```

- Language is per source: if a title is in multiple sources with different languages, one localization is stored for each requested language.
- `MetadataRefreshJob`: every day refreshes a batch of titles with `MetadataFetchedAt` older than 30 days and active availability (FR-MET-05).
- Mapping fallback: missing `tmdb_id` and present `ImdbId` → `/find`; otherwise `Unmapped` (FR-CAT-12).
- The enrichment `SyncRun` fails only if TMDB rejects requests (e.g. 401) and no title becomes Ready; 404/`Unmapped` alone leave the run as Succeeded.

### 6.7 Sonarr/Radarr library sync (`ArrSyncJob`)

```
snapshot = client.GetLibrary()   // series with per-season statistics, or movies
queue    = client.GetQueue()     // all pages
foreach item in snapshot:
    state = ArrStatusCalculator.Calculate(item, queue)   // §7.1
    upsert ArrLibraryItem (+ ArrSeasonStates)
    TitleId = ArrMatcher.Match(item, Titles)             // §7.2
delete from cache items no longer present in the snapshot
```

- On network/credential error the run fails **without touching the cache** (FR-ST-07); the error is recorded in `SyncRuns`.
- After a successful sync, rematch titles that arrived later (`TitleId` null).

### 6.8 Leaving-dates sync (`LeavingSyncJob`, FR-CAT-11)

One call to the leaving-dates endpoint per country (free: max 10 items), then fan-out to all enabled sources for that country filtering by `provider_id`. For each entry resolve the title via Watchmode id, set `LeavingDate`, and record `LeavingAnnounced`; for titles that had a date and no longer appear, record `LeavingCleared`. On the free plan the job is low-cost and optional (setting).

---

## 7. Domain logic

Rules are pure functions in `Core/Domain/Rules`, covered by unit tests.

### 7.1 Library state calculation (BR-09, BR-10)

```csharp
public static ArrState ForMovie(ArrMovieSnapshot m, QueueIndex q) =>
    q.HasMovie(m.ArrId) ? ArrState.Downloading :
    m.HasFile            ? ArrState.Downloaded  :
                           ArrState.Missing;

public static ArrState ForSeason(SeasonStats s, bool inQueue) =>
    inQueue                           ? ArrState.Downloading :
    s.EpisodeCount == 0               ? ArrState.Missing :
    s.EpisodeFileCount >= s.EpisodeCount ? ArrState.Downloaded :
    s.EpisodeFileCount > 0            ? ArrState.Partial :
                                        ArrState.Missing;

public static ArrState ForSeries(IReadOnlyList<SeasonState> seasons, bool anyInQueue)
{
    if (anyInQueue) return ArrState.Downloading;
    var monitored = seasons.Where(s => s.Monitored && s.EpisodeCount > 0).ToList();
    if (monitored.Count == 0) return ArrState.Missing;
    if (monitored.All(s => s.State == ArrState.Downloaded)) return ArrState.Downloaded;
    if (monitored.Any(s => s.EpisodeFileCount > 0))         return ArrState.Partial;
    return ArrState.Missing;
}
```

- `EpisodeCount` is the number of expected episodes per Sonarr statistics (aired and monitored episodes). Exact semantics must be **verified on instances (SP-3)** and, if different, the function adapts using `TotalEpisodeCount`.
- Series download percentage is the weighted average of `size/sizeleft` of queue items.

### 7.2 Library ↔ catalog match (`ArrMatcher`, BR-08)

| Kind | Primary key | Fallback |
|---|---|---|
| Radarr | `Titles.TmdbId` with `TmdbType = movie` | `ImdbId` |
| Sonarr | `Titles.TvdbId` | `Titles.TmdbId` with `TmdbType = tv`, then `ImdbId` |

### 7.3 Title action capability

```
kind      = (MediaType == Series) ? Sonarr : Radarr
requiredId= (kind == Sonarr) ? TvdbId : TmdbId
canAdd    = instance configured && !inLibrary && requiredId != null
canDelete = inLibrary
addBlockedReason ∈ { NotConfigured, MissingTvdbId, MissingTmdbId, AlreadyInLibrary }
```

### 7.4 Add flow (`ArrAddService`)

1. Load the title; determine `kind` and the required id; if missing → error `ARR_ID_UNMAPPED` (422).
2. `FindByExternalId` on the instance; if it exists → `ARR_ALREADY_EXISTS` (409), update the cache and return state.
3. Lookup on the instance; if empty → `ARR_REJECTED`.
4. Build the request from the lookup applying options; execute `POST`.
5. If `searchNow` and the instance does not already handle it via `addOptions`, enqueue `SearchAsync`.
6. Write `ArrAuditLog` (with `ProfileId` if the profile header is present) and enqueue `ArrRefreshItemJob` (or update the cache immediately from the response).
7. Return the updated state.

Validation errors returned by Sonarr/Radarr (400 with an `errorMessage` array) are mapped to `ARR_REJECTED` with the original messages in the `errors` field.

### 7.5 Delete flow (`ArrDeleteService`)

1. Find `ArrLibraryItem` for the title (or by `kind` + `arrId`).
2. `DELETE` with `deleteFiles` and `addImportExclusion` as chosen.
3. Remove the item from the cache, write the audit, return `NotInLibrary`.
4. On error no local change (BR-13); catalog and favorites are untouched.

---

## 8. API contracts

Prefix `/api/v1`. JSON `camelCase`. Pagination with `page` (from 1) and `pageSize` (default 48, max 100). Errors follow RFC 7807 with `code` and `errors` extensions (§16). The full specification is generated as OpenAPI at `/openapi/v1.json` and browsable at `/docs`.

### 8.1 Catalog

**`GET /catalog/sources`** — enabled sources for the selector.

```json
[
  { "id": 1, "providerKey": "netflix", "providerName": "Netflix",
    "countryCode": "IT", "metadataLanguage": "it-IT",
    "baselineCompleted": true, "lastFullSyncAt": "2026-11-01T03:12:44Z",
    "dataStale": false }
]
```

**`GET /catalog/sources/{sourceId}/titles`** — grid/search.

Query: `q`, `view` (`all`|`new`|`leaving`|`removed`), `type` (multi: `movie`,`series`,`standup`), `genre` (multi, id), `yearFrom`, `yearTo`, `arrState` (multi: `notInLibrary`,`missing`,`downloading`,`downloaded`,`partial`), `includeRemoved` (bool), `minRating`, `sort` (`relevance`|`added`|`year`|`rating`|`title`), `dir` (`asc`|`desc`), `page`, `pageSize`.

Favorites are not filtered on the catalog: use `GET /favorites` and the UI route `/list`. Any `favorite` flag on the catalog query is ignored.

```json
{
  "page": 1, "pageSize": 48, "total": 1832,
  "items": [
    {
      "id": 101,
      "name": "Example",
      "originalTitle": "Example",
      "year": 2024,
      "category": "series",
      "posterUrl": "https://image.tmdb.org/t/p/w342/abc.jpg",
      "genres": [{ "id": 18, "name": "Drama" }],
      "ratingTmdb": 7.8,
      "ratingImdb": null,
      "isFavorite": true,
      "rating": "like",
      "availability": { "addedAt": "2026-10-28T04:00:10Z", "removedAt": null, "leavingDate": null, "isNew": true },
      "arr": { "kind": "sonarr", "state": "downloading", "monitored": true, "progress": 42.5 }
    }
  ]
}
```

**`GET /catalog/sources/{sourceId}/types`** — types present in the source, with counts.

**`GET /catalog/sources/{sourceId}/genres`** — genres present in the source, with counts.

**`GET /catalog/types`** — types present across the union of enabled catalog sources (For you All scope).

**`GET /catalog/genres`** — genres present across the union of enabled catalog sources (For you All scope).

**`GET /catalog/sources/{sourceId}/featured`** (C) — featured title for the hero.

### 8.2 Title

**`GET /titles/{id}?sourceId=1`** — full card.

```json
{
  "id": 101,
  "name": "Example", "originalTitle": "Example", "year": 2024,
  "category": "series", "overview": "…",
  "runtimeMinutes": null, "seasonCount": 3, "episodeCount": 24,
  "genres": [{ "id": 18, "name": "Drama" }],
  "posterUrl": "https://image.tmdb.org/t/p/w500/abc.jpg",
  "backdropUrl": "https://image.tmdb.org/t/p/w780/def.jpg",
  "ratings": { "tmdb": 7.8, "tmdbVotes": 1520, "imdb": null },
  "links": { "tmdb": "https://www.themoviedb.org/tv/987654", "imdb": "https://www.imdb.com/title/tt1234567/", "tvdb": null },
  "isFavorite": true,
  "rating": "like",
  "metadataStatus": "ready",
  "availability": [
    { "sourceId": 1, "countryCode": "IT", "addedAt": "2026-10-28T04:00:10Z", "removedAt": null, "leavingDate": null }
  ],
  "arr": {
    "kind": "sonarr", "arrId": 55,
    "state": "partial", "monitored": true, "progress": null,
    "sizeOnDisk": 18253611008, "path": "/tv/Example",
    "syncedAt": "2026-11-04T08:15:00Z",
    "seasons": [
      { "number": 1, "monitored": true, "state": "downloaded", "episodeFileCount": 8, "episodeCount": 8, "totalEpisodeCount": 8 },
      { "number": 2, "monitored": true, "state": "partial", "episodeFileCount": 3, "episodeCount": 8, "totalEpisodeCount": 8 }
    ],
    "canAdd": false, "canDelete": true, "addBlockedReason": null,
    "externalUrl": null
  }
}
```

For a title not in the library `arr` is `{ "kind": "radarr", "state": "notInLibrary", "canAdd": true, "canDelete": false, "addBlockedReason": null }`; without a configured instance `canAdd` is `false` and `addBlockedReason` is `notConfigured`.

### 8.3 Favorites and profile

Header **`X-Pickarr-Profile: {id}`**: required on personal mutations and lists (favorites, ratings, watched, recommendations). Without header → **400**. Catalog and title card without header remain **200** with empty personal flags. Creating/deleting a profile or editing another without a declared admin → **403**. Deleting the admin or exceeding 10 profiles → **409**. The check on the declared id is spoofable (no authentication).

| Method | Path | Notes |
|---|---|---|
| `GET` | `/profiles` | Profile list. |
| `POST` | `/profiles` | Create (declared admin only). |
| `PATCH` | `/profiles/{id}` | Update name/color/toggles; a non-admin only self. |
| `DELETE` | `/profiles/{id}` | Delete (admin only; not the admin itself). |
| `PUT` | `/favorites/{titleId}` | Idempotent. 204. Header required. |
| `DELETE` | `/favorites/{titleId}` | Idempotent. 204. Header required. |
| `GET` | `/favorites` | Active profile’s favorites list (cross-provider). Types/genres: `/favorites/types`, `/favorites/genres`. |

Any `favorite` flag on the catalog query is ignored: the list is `/favorites` and the UI route `/list`.

### 8.4 Sonarr/Radarr

**`GET /arr/options?kind=sonarr`** — data for the dialog.

```json
{
  "kind": "sonarr",
  "configured": true, "reachable": true,
  "qualityProfiles": [{ "id": 4, "name": "HD-1080p" }],
  "rootFolders": [{ "path": "/tv", "freeSpace": 812345678901 }],
  "tags": [{ "id": 1, "label": "netflix" }],
  "defaults": { "qualityProfileId": 4, "rootFolderPath": "/tv" },
  "monitorOptions": ["all","future","missing","existing","firstSeason","lastSeason","latestSeason","pilot","recent","none"],
  "seriesTypes": ["standard","daily","anime"]
}
```

For Radarr: `monitorOptions` absent, `minimumAvailabilityOptions: ["announced","inCinemas","released"]`. Server-side cache for 60 seconds.

**`POST /titles/{titleId}/arr`** — add (UC-06).

Sonarr request:

```json
{
  "qualityProfileId": 4, "rootFolderPath": "/tv",
  "monitor": "all", "monitorNewItems": "all",
  "seriesType": "standard", "seasonFolder": true,
  "tags": [1], "searchNow": true
}
```

Radarr request:

```json
{
  "qualityProfileId": 4, "rootFolderPath": "/movies",
  "monitored": true, "minimumAvailability": "released",
  "tags": [], "searchNow": true
}
```

Response `201` with the same `arr` object as the card. Errors: `422 ARR_ID_UNMAPPED`, `409 ARR_ALREADY_EXISTS`, `424 ARR_REJECTED` (with `errors`), `503 ARR_NOT_CONFIGURED` / `ARR_UNREACHABLE`, `502 ARR_UNAUTHORIZED`.

**`DELETE /titles/{titleId}/arr?deleteFiles=false&addImportExclusion=false`** — delete (UC-08). Response `200` with updated `arr` (`notInLibrary`). Confirmation is the UI’s responsibility; the API requires the two parameters **explicitly** (if absent, 400) to avoid accidental deletes via direct calls.

**`POST /titles/{titleId}/arr/search`** — trigger search (S). 202.

**`POST /titles/{titleId}/arr/refresh`** — re-read state from the instance immediately. 200 with `arr`.

### 8.5 Settings

**`GET /settings`** — secrets are not returned.

```json
{
  "watchmode": { "apiKey": { "isSet": true }, "plan": "free",
                 "quota": 2500, "reservePercent": 10, "maxCreditsPerRun": 80 },
  "tmdb":      { "apiKey": { "isSet": true } },
  "omdb":      { "apiKey": { "isSet": false } },
  "sonarr":    { "url": "http://sonarr:8989", "apiKey": { "isSet": true },
                 "defaults": { "qualityProfileId": 4, "rootFolderPath": "/tv" },
                 "syncIntervalMinutes": 15 },
  "radarr":    { "url": "http://radarr:7878", "apiKey": { "isSet": true },
                 "defaults": { "qualityProfileId": 6, "rootFolderPath": "/movies" },
                 "syncIntervalMinutes": 15 },
  "webhooks":  { "token": { "isSet": true } },
  "catalog":   { "newWindowDays": 14, "leavingWindowDays": 30, "removedWindowDays": 30,
                 "removalConfirmations": 2, "maxRemovalRatio": 0.10 },
  "ui":        { "language": "en" },
  "reco":      { "enabled": true, "useImplicit": true, "excludeInLibrary": true,
                 "n0": 30, "weightLike": 1, "weightDislike": -1,
                 "weightFavorite": 0.5, "weightArrAdd": 0.6, "weightDownloaded": 0.4,
                 "mlMinRatings": 100, "eventsEnabled": false }
}
```

**`PUT /settings`** — partial update (JSON merge). A secret is changed only if present in the body with a value; `null` clears it.

**`POST /settings/test/{target}`** with `target` ∈ `watchmode|tmdb|omdb|sonarr|radarr` — verify with saved values, or with those in the body (to test before saving). Response: `{ "ok": true, "version": "4.0.x", "detail": "…" }` or `{ "ok": false, "code": "ARR_UNAUTHORIZED", "detail": "…" }`.

**`POST /settings/webhook-token/rotate`** — regenerate the token and return it once.

**Source CRUD (S, FR-ADM-07):** `GET /admin/providers/available` (services from the provider), `POST /admin/providers`, `GET/POST/PUT/DELETE /admin/catalog-sources`.

### 8.6 Administration and jobs

| Method | Path | Description |
|---|---|---|
| `GET` | `/admin/jobs` | Job list with status, last and next run. |
| `POST` | `/admin/jobs/{jobKey}/trigger` | Body `{ "sourceId": 1, "mode": "incremental" }`. `202` with `runId`; `409 SYNC_ALREADY_RUNNING`. |
| `POST` | `/admin/runs/{runId}/cancel` | Cancel a run (S). |
| `POST` | `/admin/runs/{runId}/resume` | Resume a paused run (S). |
| `GET` | `/admin/runs?kind=&status=&page=` | Paginated history. |
| `GET` | `/admin/runs/{runId}` | Detail with statistics. |
| `GET` | `/admin/quota` | Credits. |
| `GET` | `/admin/titles/problems?kind=unmapped|failed` | Unmapped / failed-enrichment titles. |
| `POST` | `/admin/titles/{id}/retry-metadata` | Retry enrichment. |
| `GET` | `/admin/arr-audit` | Add/delete log (S). |
| `POST` | `/admin/recommendations/rebuild` | Enqueue `RecoBuildJob`. `202` + `hangfireJobId`. |
| `POST` | `/admin/recommendations/embeddings/rebuild` | Optional query `all=true`. |
| `POST` | `/admin/recommendations/model/download` | Download ONNX model. |
| `POST` | `/admin/recommendations/model/detect` | Detect files in `/config/models`. |
| `POST` | `/admin/recommendations/evaluate` | Enqueue `RecoEvalJob`. |
| `GET` | `/admin/recommendations/evaluations/latest` | Latest leave-one-out report. |
| `POST` | `/admin/recommendations/reset` | Required query `includeRatings=true\|false`. |

### 8.5bis Ratings and recommendations

All routes below require `X-Pickarr-Profile` (400 if absent). Build, status, and scores belong to the declared profile; `useImplicit` and `excludeInLibrary` are read from the profile fields, not from the global `reco.*` keys (which may still be stored).

| Method | Path | Description |
|---|---|---|
| `PUT` / `DELETE` | `/titles/{id}/rating` | Set or remove Love/Like/Dislike/NotInterested. |
| `PUT` / `DELETE` | `/titles/{id}/watched` | Mark or clear *Watched* (`isWatched`). |
| `GET` | `/ratings` | Profile ratings list (page). |
| `GET` | `/ratings/export` | JSON export. |
| `POST` | `/ratings/import` | Import; unknown TMDB ids → `unknown`. |
| `POST` | `/ratings/onboarding` | Batch rating from `/taste`. |
| `GET` | `/recommendations` | Profile *For you* (scope `source`\|`enabled`, filters, `diversify`, page). |
| `GET` | `/recommendations/status` | Stage, N_eff, model (admin row), coverage, pool, profile version. |
| `GET` | `/recommendations/onboarding` | 24-title onboarding pool. Query `exclude` (CSV) to skip titles already shown. |
| `POST` | `/recommendations/events` | Log Shown/Opened/… if `reco.events.enabled`. |
| `GET` | `/titles/{id}/similar` | Similar titles on the enabled pool (profile personal flags). |

List and title-detail DTOs, with the header, expose the profile’s `isFavorite`, `rating`, `isWatched`; without the header personal flags are empty. Where applicable, `reco` (score, reasons).

`GET /admin/quota`:

```json
{ "service": "watchmode", "quota": 2500, "used": 412, "reserve": 250,
  "spendable": 1838, "projectedMonthEnd": 1980, "lastUpdatedAt": "2026-11-04T04:05:00Z" }
```

### 8.7 Webhooks and system

| Method | Path | Notes |
|---|---|---|
| `POST` | `/webhooks/sonarr?token=…` | Responds `202` (or `401` with wrong token, constant-time comparison). |
| `POST` | `/webhooks/radarr?token=…` | same |
| `GET` | `/system/status` | Server version and time (used by the header). |
| `GET` | `/system/image-updates` | Compare the running version to GHCR tags (`ghcr.io/ildoc/pickarr`). Returns `currentVersion`, `channel` (`release`\|`development`), `newerTags`, `updateAvailable`, `checkedAt`, `source`. Release builds only list newer `X.Y.Z` tags; development builds list any newer versioned tag. Registry failures yield an empty list (fail-soft). Tag list is cached in memory (~1h). |
| `GET` | `/system/stats` | Title counts (total, per provider, country, source) and disk space under `DataPath` (catalog DB, jobs, logs, models, keys). |
| `GET` | `/system/logs` | List `pickarr-*.log` files under `DataPath/logs`. |
| `GET` | `/system/logs/{fileName}` | Download one log file (path-safe names only). |
| `DELETE` | `/system/logs` | Delete all `pickarr-*.log` files; returns `{ deleted, failed }` (active file may fail). |
| `GET` | `/health`, `/health/ready` | Liveness and readiness (DB, Hangfire). Outside `/api/v1`. |
| `GET` | `/events` (C) | Server-Sent Events for state updates (`arr-state-changed`, `run-updated`). |

---

## 9. Frontend

### 9.1 Structure

```
src/Pickarr.WebApp/src/
├─ app/            App.tsx, router.tsx, providers.tsx (QueryClient, i18n, toasts)
├─ api/            client.ts, types.ts, recommendations.ts, ratings.ts, …
├─ features/
│  ├─ who/         WhoPage (profile selector and management)
│  ├─ catalog/     CatalogPage, TitleCard, FilterBar, QuickFilters
│  ├─ list/        MyListPage
│  ├─ title/       TitleDialog, SeasonTable, ArrStatusBadge, SimilarRow
│  ├─ arr/         AddToArrDialog, DeleteFromArrDialog
│  ├─ favorites/   FavoriteButton
│  ├─ watched/     WatchedButton
│  ├─ ratings/     RatingButtons, MyRatingsPage
│  ├─ reco/        ForYouPage, TastePage, LearningProgress, ReasonChips
│  ├─ settings/    SettingsLayout, SettingsPage, CatalogSourcesPage, RecommendationsSettingsPage
│  └─ admin/       JobsPage (also under /settings/jobs)
├─ components/     layout/ (Header, Footer, Attributions)
├─ i18n/           index.ts, locales/en.json   # English = source of truth (AGENTS.md)
└─ main.tsx
```

> Note: the `frontend/` path cited in earlier drafts is obsolete; the SPA lives in `src/Pickarr.WebApp`.

### 9.2 Routing

| Path | Content |
|---|---|
| `/` | Profile gate: single profile → `/catalog`; valid `pickarr.profileId` → `/catalog`; otherwise `/who` |
| `/who` | Profile selector and management |
| `/catalog` | Grid for the selected source; filters (`q`, `view=all\|new\|leaving\|removed`, `type`, `genre`, …). With reco enabled and `view=all` without `q`, *For you* row (scope `source`, max 25). `view=favorites` redirects to `/list` |
| `/list` | Profile favorites (cross-provider / cross-country); temporary implicit signal for reco |
| `/for-you` | Profile recommendations over the union of enabled sources (scope `enabled`) |
| `/taste` | Taste onboarding |
| `/ratings` | Profile ratings + export/import |
| `/settings` | General settings (**admin only**; otherwise redirect to catalog) |
| `/settings/catalogs` | Catalog sources |
| `/settings/recommendations` | Recommendation status/actions |
| `/settings/jobs` | Jobs and credits (`/admin/jobs` redirects here) |
| `/settings/system` | Diagnostics: runtime version with newer GHCR tag indicator, titles per provider/country, disk space, log file list/download/clear |

Active profile in `localStorage` `pickarr.profileId`; the client sends it as `X-Pickarr-Profile`. Catalog source in `pickarr.sourceId.{profileId}` (an admin without their own key may once copy the legacy `pickarr.sourceId`). Used by the catalog (including the *For you* row) and by *For you*.

### 9.3 State management

- **Server data:** TanStack Query. Hierarchical keys: `['titles', sourceId, filters]`, `['title', id, sourceId]`, `['arr-options', kind]`, `['settings']`, `['admin', …]`.
- **Filters/search:** source of truth = URL (`useSearchParams`); search field has 300 ms debounce.
- **Grid:** `useInfiniteQuery` + `@tanstack/react-virtual`. Virtualized rows are computed from container width (2:3 cards). `placeholderData: keepPreviousData` to avoid flicker on filter change.
- **Favorites:** mutation with optimistic update on `titles` and `title`, rollback on error.
- **Ratings:** `RatingButtons` with real `onMutate` (do not copy the favorite as-is); after a rating invalidate `titles`/`title`/`recommendations`/`ratings` and re-poll status until `version` changes (SSE not required).
- **Library state:** 30 s `refetchInterval` for the open card and `refetchOnWindowFocus` for the grid; after add/delete invalidate `['title', id]` and `['titles']`. If SSE is active, events invalidate the same keys and polling stops.
- **Errors:** an interceptor turns `problem+json` into typed errors with `code`; the UI maps `code` to translated messages (`errors.ARR_UNREACHABLE`, …) and falls back to `detail`.

### 9.4 Key components

- **`TitleCard`**: poster with `loading="lazy"`, placeholder while loading, type badge and library state only if different from *not in library* (colors: yellow *missing*, light blue *downloading* with progress, green *downloaded*, orange *partial*), favorite icon.
- **`AddToArrDialog`**: reads `arr-options`, builds the form with react-hook-form + zod based on `kind`, preselects defaults, enables "Add" only when the form is valid.
- **`DeleteFromArrDialog`**: summary (path, size), two unchecked checkboxes, `destructive` button whose text changes if "delete files" is on; requires an explicit click.
- **`SeasonTable`**: season rows with monitored icon, `episodeFileCount/episodeCount`, state badge.
- **`Attributions`**: TMDB logo + wording, link to Watchmode.com, in the UI’s fixed footer.

### 9.5 i18n

- `react-i18next` with `fallbackLng: 'en'`; single `translation` namespace plus `errors`.
- `en.json` is the source of truth for this product. Any additional language falls back to `en` for missing keys.
- Formatting with `Intl.DateTimeFormat` / `Intl.NumberFormat` according to the current language.
- UI language is persisted in backend settings (`ui.language`) and is independent of metadata language.

### 9.6 Theme and layout

- Dark theme only: fixed `class="dark"` on `<html>`, color tokens in Tailwind (`surface`, `surface-2`, `accent`, `muted`).
- Minimum width 1280 px: below the threshold a non-blocking warning appears.
- Sticky `Header` at the top (`sticky top-0`) with semi-transparent background and blur, so the menu stays reachable while scrolling. On the right: version from `GET /api/v1/system/status` (same `VERSION` string as the Docker image / `InformationalVersion`) with a link to `https://github.com/ildoc/Pickarr`, and a Ko-fi icon to `https://ko-fi.com/pheeleeppo`.
- Grid with columns `repeat(auto-fill, minmax(180px, 1fr))`.
- Basic accessibility: dialogs with focus trap (Radix), text alternatives on posters, adequate contrast in badges.

### 9.7 Build and development

- Dev: `vite` on :5173 with proxy `/api` and `/health` to `http://localhost:5000`.
- API client: `npm run api:generate` runs `openapi-typescript http://localhost:5000/openapi/v1.json -o src/api/schema.d.ts`. The generated file is committed; CI verifies it is up to date (§14).
- Production: `vite build` produces `dist/`, copied into the Docker image `wwwroot` (no separate server).

---

## 10. Security

| Topic | Measure |
|---|---|
| Secrets at rest | Watchmode/TMDB/OMDb keys, Sonarr/Radarr tokens, and the webhook token are encrypted with **Data Protection** (key ring in `/config/keys`; if lost, secrets must be re-entered). |
| Secrets in transit to the UI | Never returned: only `isSet`. Webhook token rotation shows the value once. |
| Logs | No secrets in logs; redact `X-Api-Key` and `Authorization` headers, and `apiKey`/`token` parameters in logged URLs. |
| Webhooks | Random token ≥ 32 bytes, constant-time comparison, rate limit (e.g. 30 req/min), no effect beyond refreshing the item. |
| Input | Validate all DTOs; instance URLs: only `http`/`https`, no embedded credentials, normalized without trailing slash. |
| SSRF | The only configurable outbound destinations are the Sonarr/Radarr instance URLs set in Settings; no other endpoint accepts arbitrary URLs. |
| Destructive actions | The delete API requires explicit parameters; the UI requires confirmation. Audit log. |
| Access | v1 **without login** (D-02): document use on a LAN or behind a reverse proxy with authentication. Prepare an optional authentication middleware (single password with cookie, enableable via environment variable) as a future evolution. |
| Hangfire dashboard | **Disabled** by default (`Hangfire:Dashboard:Enabled=false`); the Admin page uses its own API. If enabled, only with authentication active. |
| ONNX models | Files under `/config/models` (or `Pickarr__Models__Path`); download with hash verification; self-test before marking `Ready`. No taste data leaves the container. |
| Headers | CSP with `img-src 'self' https://image.tmdb.org data:`, `X-Content-Type-Options`, `Referrer-Policy: no-referrer`, no CORS (same origin). |
| Container | Run as non-root user, writable filesystem only on `/config`. |
| Dependencies | Periodic updates, `dotnet list package --vulnerable` and `npm audit` in CI. |

---

## 11. Observability

- **Logging:** Serilog, JSON format on console (`docker logs`) and rotating file in `/config/logs` (daily rotation, max 32 MB per file, at most 7 files and no more than 7 days). Configurable level (`LOG_LEVEL`, default `Debug`). Context properties: `RunId`, `SourceId`, `Job`, `Kind`.
- **Health:** `/health` (liveness, always 200 if the process responds) and `/health/ready` (checks DB open and Hangfire storage).
- **Business metrics:** `SyncRuns` and `ApiUsage` are the source for the Admin page; no external metrics stack is needed.
- **OpenTelemetry (C):** optional OTLP export via environment variable, off by default.
- **Diagnostics:** `GET /system/status` reports version and server time; `GET /system/image-updates` compares that version to GHCR version tags (Settings → System); `GET /system/stats` reports title counts and sizes under `DataPath`; `GET/DELETE /system/logs` manage Serilog files (Settings → System tab). Finished `SyncRuns` are pruned per `(Kind, CatalogSourceId)` using `jobs.runHistoryLimit` (default 10, max 100).

---

## 12. Configuration

### 12.1 Environment variables (bootstrap)

| Variable | Default | Description |
|---|---|---|
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address. |
| `Pickarr__DataPath` | `/config` | Data folder (DB, keys, logs). |
| `TZ` | `UTC` | Timezone for cron and logs. |
| `Pickarr__BaseUrl` | (empty) | Public URL for generating webhook URLs. |
| `Pickarr__PathBase` | (empty) | Prefix when behind a reverse proxy with a subpath. |
| `LOG_LEVEL` | `Debug` | Serilog log level (`Verbose`/`Debug`/`Information`/`Warning`/`Error`/`Fatal`). |
| `Pickarr__Auth__Password` | (empty) | Enables optional authentication. |
| `Hangfire__Dashboard__Enabled` | `false` | Hangfire dashboard. |
| `Pickarr__Models__Path` | `/config/models` | ONNX models directory. |
| `Pickarr__Models__DownloadBaseUrl` | (model repo) | Download mirror. |
| `Pickarr__Reco__MaxThreads` | `2` | ONNX thread cap (wins if lower than the setting). |

§12.2 keys, except the operational state `catalog.initialImportStarted`, are declared with flat `SCREAMING_SNAKE` names derived from the key (`.` and camelCase → `_`). Examples: `WATCHMODE_API_KEY`, `WATCHMODE_QUOTA`, `TMDB_API_KEY`, `SONARR_URL`, `SONARR_API_KEY`, `RADARR_URL`, `RECO_ENABLED`, `RECO_WEIGHTS_LIKE`. The legacy `Pickarr__Settings__` form with each `.` replaced by `__` remains accepted. If both are set, the flat name wins. Sonarr and Radarr defaults are passed as `SONARR_DEFAULTS_QUALITY_PROFILE_ID` / `SONARR_DEFAULTS_ROOT_FOLDER_PATH` or as JSON in `SONARR_DEFAULTS` (and the Radarr equivalents).

If the variable is set and non-empty, that value is effective for the whole run: it wins over the saved value and a UI update does not replace it. Secrets declared this way are not written to the database and APIs continue to expose only `isSet`. An unparseable number or boolean, or an unknown nested key, prevents startup. Booleans are `true` or `false`; numbers use a decimal point. Without the variable, the database value (or the default) remains.

### 12.2 Application settings (`Settings` table)

Keys with their defaults: `watchmode.apiKey`, `watchmode.plan=free`, `watchmode.quota=2500`, `watchmode.reservePercent=10`, `watchmode.maxCreditsPerRun=80`, `watchmode.rateLimitPerSecond=1`, `watchmode.billingDay=1`, `tmdb.apiKey`, `omdb.apiKey`, `omdb.dailyLimit=900`, `sonarr.url`, `sonarr.apiKey`, `sonarr.defaults`, `sonarr.syncIntervalMinutes=15`, `radarr.*` (like Sonarr), `webhooks.token`, `catalog.newWindowDays=14`, `catalog.leavingWindowDays=30`, `catalog.removedWindowDays=30`, `catalog.removalConfirmations=2`, `catalog.maxRemovalRatio=0.10`, `catalog.staleWarningDays=21`, `ui.language=en`, plus reco keys: `reco.enabled=true`, `reco.useImplicit=true`, `reco.excludeInLibrary=true`, `reco.n0=30`, `reco.weights.*` (including `reco.weights.watched=0.35`), `reco.embeddings.*`, `reco.mmr.lambda=0.7`, `reco.explore.rate=0.1`, `reco.ml.minRatings=100`, `reco.events.enabled=false` (full list in `recommendations.md` §16).

When settings relevant to schedules change, `SettingsService` emits an event that triggers `ScheduleReconciler`.

---

## 13. Test strategy

### 13.1 Pyramid

| Level | What | Tools |
|---|---|---|
| **Unit — Core** | BR rules (arr states, newness, removal, budget), `CatalogUpserter` with in-memory repository, `LanguageFallback`, type mapping. High coverage target (≥ 85% on `Core`). | xUnit, Shouldly, NSubstitute |
| **Unit — Infrastructure** | HTTP clients against **WireMock.Net** with JSON fixtures: parsing, 429/5xx handling, secret redaction, `ApiUsage` writes. | WireMock.Net |
| **API integration** | `WebApplicationFactory` with SQLite on a temporary file and WireMock instead of Watchmode, TMDB, Sonarr, Radarr. Flows: sync → enrichment → list/search → add → state → delete. | xUnit, `WebApplicationFactory` |
| **Sync scenarios** | First full sync (baseline), incremental with new releases, full sync with double-confirmation removal, anti-anomaly guard, pause/resume for budget, restart with orphan run, title reappearance. | integration |
| **Contract** | Fixtures recorded from real (anonymized) Watchmode/TMDB/Sonarr/Radarr responses, in `tests/fixtures/`; tests that verify parsing and detect deviations. | xUnit |
| **Frontend unit/component** | Dialog forms (validation, defaults), `ArrStatusBadge`, URL and formatting utilities, i18n fallback; calls mocked with MSW. | Vitest, Testing Library, MSW |
| **E2E** (S) | `docker compose` stack with WireMock: search, add, state, delete, settings. | Playwright |

### 13.2 Rules

- No test calls real services; spikes (§17) are separate scripts/manual work and produce fixtures.
- Time and GUIDs via `IClock`/`IIdGenerator` to make sync tests deterministic.
- Migration tests: from empty DB and from a previous-version snapshot.
- CI fails under the coverage threshold (configurable); flaky tests are quarantined with a ticket.

---

## 14. Build, packaging, and CI/CD

### 14.1 Dockerfile (single image)

The Dockerfile in `src/Pickarr.Api/Dockerfile` is the one run by the pipeline. In summary:

- `npm ci` and `dotnet restore` sit on their own layers, before sources. A code change does not re-download packages; a frontend-only change does not republish the API, and vice versa.
- Publish is framework-dependent (`--no-self-contained`) for `linux-x64` or `linux-arm64`, without debug symbols and without a second runtime. Frontend and `dotnet publish` run on the builder architecture (`$BUILDPLATFORM`) to avoid QEMU emulation; the final image is that of the target platform.
- The runtime image stays `aspnet:10.0`: ONNX needs `libgomp`, which chiseled images do not contain. `curl` is not installed. The healthcheck uses a static `wget` copied from BusyBox.
- The `base` stage prepares `/config` owned by `app` for Visual Studio fast mode and for new volumes. An already existing root-owned volume is not rewritten.

The non-root user and `/config` volume permissions must be verified (SP-5); the `aspnet` image uses the `app` user, so the mounted folder must be writable by it (or set `user:` in compose).

### 14.2 `docker-compose.yml`

```yaml
services:
  Pickarr:
    image: ghcr.io/<owner>/pickarr:latest
    container_name: Pickarr
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - TZ=Europe/Rome
      - Pickarr__BaseUrl=http://Pickarr:8080   # used to generate webhook URLs
      # WATCHMODE_API_KEY / TMDB_API_KEY / SONARR_URL / … (default DataPath=/config, URLS=:8080)
    volumes:
      - ./config:/config
```

### 14.3 Pipeline (`.github/workflows/ci.yml`)

Pull request, push to the default branch, and tags. Lint and test run in parallel; the image is built only if they are green. Published platform: `linux/amd64`.

| Job | Content |
| --- | --- |
| Backend lint | `dotnet format --verify-no-changes`; non-blocking NuGet vulnerability report |
| Frontend lint | `npm run lint`, `npm run typecheck` |
| Backend test | `dotnet test` in Release; TRX artifact |
| Frontend test | `npm test`, `npm run build` |
| Image | Build Dockerfile. Push to GHCR from the default branch (`X.Y.Z.N` and `edge`) and from SemVer tags (`X.Y.Z` and `latest`). |

Details:

- **Versioning:** a `vX.Y.Z` or `X.Y.Z` tag fixes version `X.Y.Z` (image `X.Y.Z` and `latest`). Subsequent builds use `X.Y.Z.N`, where N is the number of commits after that tag, until the next tag. Without a tag the version is `0.0.0.N`. The default branch also publishes `edge`. The string is the assembly `Version` and `InformationalVersion`; `/system/status` reports the version without any commit suffix. `CHANGELOG.md` updated on every release.
- **Cache:** NuGet and npm packages via setup actions; Docker layers with GitHub Actions cache.
- **Registry:** GitHub Container Registry (`ghcr.io`), authentication with `GITHUB_TOKEN`. No registry secret to configure.
- **Documentation (FR/NFR-08):** OpenAPI is generated by the backend; documents in `docs/` stay versioned with the code.
- **E2E test image (S):** optional job, not in the current pipeline, that starts the stack with WireMock and runs Playwright.

---

## 15. Multi-service extension

### 15.1 Abstraction

The model (§4) is already multi-service: `Providers` → `CatalogSources (provider+country)` → `Availabilities`. The data-access abstraction is:

```csharp
public interface ICatalogProvider
{
    string Type { get; }                                   // "watchmode"
    CatalogProviderCapabilities Capabilities { get; }      // SupportsChangeFeed, SupportsLeavingDates, MaxPageSize, ...

    Task<IReadOnlyList<ProviderInfo>> GetAvailableProvidersAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetCountriesAsync(string providerExternalId, CancellationToken ct);

    Task<CatalogPage> ListTitlesAsync(CatalogListRequest request, CancellationToken ct);
    Task<IReadOnlyList<LeavingEntry>> GetLeavingAsync(CatalogSourceRef source, CancellationToken ct);
    Task<IReadOnlyList<CatalogEntry>> GetChangedTitlesAsync(CatalogSourceRef source, DateOnly from, DateOnly to, CancellationToken ct);
}

public sealed record CatalogListRequest(
    string ProviderExternalId, string CountryCode,
    IReadOnlyList<CatalogEntryType> Types, CatalogSort Sort, int Page, int PageSize);

public sealed record CatalogPage(
    IReadOnlyList<CatalogEntry> Entries, int Page, int TotalPages, int CreditsUsed);

public sealed record CatalogEntry(
    string ExternalId, string Title, int? Year, CatalogEntryType Type,
    int? TmdbId, string? TmdbType, string? ImdbId);
```

- `WatchmodeCatalogProvider` implements the interface and is **data-parameterized**: the service id (`Providers.ExternalId`) and country come from the source. Adding Amazon Prime or Disney+ therefore requires no code.
- The resolver chooses the implementation from `Providers.ProviderType` (`IProviderResolver`). A future different source is another class registered in DI with a different `Type`.
- The rest of the system (upsert, enrichment, API, UI) does not know Watchmode: it works with `CatalogEntry`.

### 15.2 Procedure to add a service or country

1. **Admin → Services:** the backend calls `GetAvailableProvidersAsync` (Watchmode `/sources/`, filtered by country) and proposes the list.
2. The user chooses the service (e.g. Amazon Prime, Disney+) → create a `Providers` row with `ExternalId` = Watchmode `source_id`.
3. The user chooses country and metadata language → `CatalogSources` row (enabled).
4. `ScheduleReconciler` registers recurring jobs; a full sync (baseline) is enqueued.
5. When baseline completes the source appears in the selector.

Constraints and checks:

- Free plan: maximum 3 distinct countries among enabled sources → warning (FR-PRV-04); the check is on the number of countries, not services.
- Before enabling a new source, the system estimates the credits needed (`LastFullTotalPages` if known, otherwise a default) and compares them with the available budget.
- Service ids are read from `/sources/`, never hard-coded (the only known value, Netflix = 203, is in the seed).

### 15.3 Data shared across sources

A title is unique and reused across sources: TMDB metadata is downloaded once (per language), the card shows availability on all sources (FR-DET-07), favorites and library state are global to the title.

---

## 16. Error handling

### 16.1 Format

```json
{
  "type": "https://Pickarr.local/errors/arr-unreachable",
  "title": "Sonarr unreachable",
  "status": 503,
  "detail": "Timeout while connecting to http://sonarr:8989.",
  "code": "ARR_UNREACHABLE",
  "traceId": "00-…",
  "errors": { "rootFolderPath": ["Path is not valid"] }
}
```

### 16.2 Codes

| Code | HTTP | When |
|---|---|---|
| `VALIDATION_ERROR` | 400 | Invalid DTO. |
| `NOT_FOUND` | 404 | Title, source, or run does not exist. |
| `ARR_NOT_CONFIGURED` | 503 | Instance URL or key missing. |
| `ARR_UNREACHABLE` | 503 | Timeout or network error. |
| `ARR_UNAUTHORIZED` | 502 | Key rejected by the instance. |
| `ARR_ID_UNMAPPED` | 422 | TVDB/TMDB id missing for the title. |
| `ARR_ALREADY_EXISTS` | 409 | Title already in the library. |
| `ARR_REJECTED` | 424 | The instance rejected the request (with `errors`). |
| `SYNC_ALREADY_RUNNING` | 409 | Job already running. |
| `QUOTA_EXHAUSTED` | 429 | Insufficient credit budget for a manual operation. |
| `UPSTREAM_ERROR` | 502 | Unclassified error from an external service. |
| `INTERNAL_ERROR` | 500 | Unexpected error (details only in logs). |

### 16.3 Principles

- A global `ExceptionHandler` converts domain/infrastructure exceptions to `ProblemDetails`; no stack traces to the client.
- Jobs do not rethrow exceptions to the framework: they record the outcome in `SyncRuns` (`Failed`, with message) and in logs, and leave data consistent (per-page transactions, no removals on an incomplete run).
- Failed Sonarr/Radarr syncs never empty the cache (FR-ST-07).
- Transient errors use HTTP resilience (§5); permanent ones (401, 403, configuration) are not retried.

---

## 17. Technical risks and initial spikes

Spikes are short activities to do **before** implementation, with a written output (notes + fixtures in `tests/fixtures/`).

| ID | Spike | Questions to answer |
|---|---|---|
| **SP-1** | **Watchmode** | Credit cost of `list-titles` (per page) and other calls; exact parameter names (`source_ids`, `regions`, `types`, `sort_by`, `page`, `limit`); max `limit`; response format; available sorts and stability (for full sync); Netflix IT/US title counts to estimate pages; response when quota is exhausted; leaving-dates endpoint and cost (free preview); whether `releases` is available on the free plan. |
| **SP-2** | **TMDB** | `tvdb_id` coverage for Netflix IT/US series; `language` behavior with missing translations; stand-up classification by keyword **name** “stand-up comedy” (no hardcoded keyword ids); `tv_special` cases. |
| **SP-3** | **Sonarr/Radarr** | Versions in use; real payloads of `POST /series` and `POST /movie`; allowed values for `monitor` and `monitorNewItems`; semantics of `statistics.episodeCount` vs `totalEpisodeCount`; queue format and pagination; delete parameters; webhook payload and authentication; presence of an explicit default for profile/root folder (D-01). |
| **SP-4** | **Hangfire on SQLite** | Stability of the storage package under concurrency and restarts; if negative, in-memory storage (schedules are recreated) or another storage. |
| **SP-5** | **SQLite and container** | Search and grid performance with 10k titles and joins with `ArrLibraryItems`; WAL behavior on network/Docker volumes; `/config` volume permissions with non-root user. |
| **SP-6** | **Frontend** | Smoothness of the virtualized grid; CSP with TMDB images; modal route behavior with filters in the URL. |
| **SP-R1** | **TMDB reco** | Does the already-requested payload suffice for keywords, person id, crew, `created_by`? Cost of an extra `en-US` call. |
| **SP-R2** | **ONNX** | .NET tokenizer, pooling, reference vectors, x64/arm64 throughput, q8 variant, RAM. |
| **SP-R3** | **Reco latency** | Rebuild pool+≤500 signals &lt;10 s; GET recommendations p95 &lt;300 ms (availability join + library filter); MMR/similar impact. |
| **SP-R4** | **Calibration** | With 30–50 ratings: compare levels 0/1/2/(3) and defaults `a`/`b`/`N0`/`γ`/`K`/`m`. |
| **SP-R5** | **Licenses** | TMDB terms on local inference; model license. Manual pre-release check (risk note, not a blocking task). |

Relevant residual risks:

| ID | Risk | Mitigation |
|---|---|---|
| TR-01 | Undocumented variations in external APIs. | Contract tests with fixtures; tolerant handling of unknown fields (`JsonUnmappedMemberHandling.Skip`). |
| TR-02 | Credit exhaustion due to a bug in the pagination cycle. | Hard limit per run, check before every call, dedicated scenario tests. |
| TR-03 | SQLite corruption or lock. | WAL, backup before migrations, dedicated volume, separate job database. |
| TR-04 | Loss of the Data Protection key ring (unreadable secrets). | Document that `/config/keys` is part of the backup; the UI signals undecryptable secrets and asks to re-enter them. |
| TR-05 | Accidental file deletion. | Options off by default, explicit confirmation, mandatory API parameters, audit log. |

---

## 18. Recommended implementation order

1. **Foundations:** repository, .NET solution, Vite, Dockerfile, basic CI, health, migrations and seed, `Settings` with secret encryption. Spikes SP-1…SP-6 in parallel.
2. **Catalog:** Watchmode client, `ICatalogProvider`, `CatalogUpserter`, credit budget, incremental and full sync with checkpoint, `SyncRuns`/`ApiUsage`.
3. **Metadata:** TMDB client, `EnrichmentJob`, localizations with fallback, genres, external ids.
4. **Read API and UI:** titles/search/detail endpoints, grid, filters, card, favorites, i18n, attributions.
5. **Sonarr/Radarr:** `IArrClient`, `ArrSyncJob`, state calculation, match, badges and per-season detail.
6. **Actions:** add dialog, delete with confirmation, audit.
7. **Administration:** jobs page, manual run, credits, unmapped titles.
8. **Recommendations R1–R4:** rating + prior + *For you*; keyword/credits enrichment; sparse/similar; ONNX/kNN/MMR; eval + gated L3 + event log (see `recommendations.md`).
9. **Polish:** webhooks, leaving dates, OMDb, filters in URL, SSE, Italian translation, E2E.
10. **New services:** service/country management UI and validation with Amazon Prime/Disney+.

Each step ends with automated tests and a green pipeline.

---

## 19. Appendices

### A. Requirements → components map

| Requirements | Main components |
|---|---|
| FR-CAT-* | `CatalogSyncService`, `CatalogUpserter`, `ICatalogProvider`, `ICreditBudget`, `ScheduleReconciler`, `SyncRuns`, `Availabilities` |
| FR-MET-* | `EnrichmentService`, `TmdbClient`, `OmdbClient`, `TitleLocalizations` |
| FR-UI/SRC/DET/FAV-* | `TitleQueryService`, `titles`/`favorites` endpoints, frontend features `catalog`, `title`, `favorites` |
| FR-ADD/ST/DEL-* | `IArrClient`, `ArrAddService`, `ArrDeleteService`, `ArrLibrarySyncService`, `ArrStatusCalculator`, `ArrMatcher`, webhook |
| FR-SET/ADM-* | `SettingsService`, `settings`/`admin` endpoints, frontend features `settings`, `admin` |
| FR-I18N-* | frontend `i18n/` |
| FR-PRV-* | `Providers`, `CatalogSources`, `IProviderResolver`, §15 |
| NFR-* | Docker, Serilog, health, test, CI, security (§10–§14) |

### B. Technical glossary

- **Checkpoint:** serialized state of a run (next page) that enables resume.
- **Baseline:** first full import of a source; its titles are not “new”.
- **Sync strategy:** implementation of `ICatalogSyncStrategy` (`ListPagesStrategy` for the free plan, `ChangeFeedStrategy` for the paid plan).
- **Orphan run:** `SyncRun` in `Running` state after a restart; it is marked `Paused`.
