# Architecture

High-level view for contributors. For entity tables, job schedules, and API contracts, see `technical-spec.md` and `recommendations.md`.

## Runtime topology

One process in one container:

```text
┌──────────────────────── container Pickarr ────────────────────────┐
│  Kestrel :8080                                                      │
│   ├─ /            → SPA (wwwroot)                                   │
│   ├─ /api/v1/*    → Minimal API                                     │
│   ├─ /docs        → OpenAPI + Scalar                                │
│   └─ /health      → health check                                    │
│  Hangfire Server (in-process): queues default, sync, arr, ml        │
│  SQLite: DataPath/Pickarr.db, jobs.db, keys/, models/               │
└─────────────────────────────────────────────────────────────────────┘
        │               │               │               │
     Watchmode         TMDB       Sonarr (v4+)     Radarr (v5+)
```

Data flow:

```text
Watchmode ──(scheduled jobs)──► Local catalog ◄──(enrichment)── TMDB
                                       │
                                       ▼
              UI: catalog, list, For you, ratings, detail
                                       │
                                       ▼
                    Backend ──► Sonarr / Radarr   (+ optional ONNX ml queue)
                                       ◄── status (periodic sync + webhooks)
```

## Layers

| Project | Responsibility |
| --- | --- |
| **Core** | Entities, enums, pure recommendation math, settings key names, abstractions (`ICatalogProvider`, `IMetadataProvider`, Arr clients, reco services). |
| **Infrastructure** | `PickarrDbContext`, migrations, HTTP clients, Hangfire jobs, Data Protection, ONNX/ML hosting. |
| **Api** | Endpoint groups, DI composition, static file hosting of the SPA, health. |
| **WebApp** | Routes, TanStack Query, profile header, i18n. |

Endpoints live under `src/Pickarr.Api/Endpoints/` (catalog, titles, favorites, ratings, recommendations, profiles, settings, Arr, admin, system).

## Data boundaries

| Shared (install-wide) | Per profile |
| --- | --- |
| Titles, availabilities, providers, catalog sources | Favorites |
| TMDB enrichment, embeddings | Ratings, watched/hide |
| App settings, Watchmode credits | Recommendation build state and “For you” |
| Arr library rows / status | Reco toggles (`useImplicit`, `excludeInLibrary`) |
| | Preferred catalog source in browser storage |

Profile context arrives via `X-Pickarr-Profile`. Arr adds without the header are valid but do not attribute an implicit taste signal.

## Jobs (conceptual)

| Area | Examples |
| --- | --- |
| Catalog | Incremental sync, full sync, credit budget, removal confirmation |
| Metadata | TMDB enrichment backlog |
| Arr | Periodic library sync; optional webhook intake |
| Recommendations | Embedding encode, reco build, offline eval (ml queue) |

Schedules reconcile when relevant settings change. Hangfire storage defaults to SQLite under the data path; tests often use Memory.

## Recommendations (short)

Local, multi-level scoring over titles already present on enabled catalog sources. Signals include explicit votes and optional implicit signals (favorites, Arr add, download). Under cold start (`N_eff < N0`), a household prior from other profiles’ Love/Like can blend into the non-personal slot (capped). Optional ONNX embeddings improve similarity. Full algorithm and settings: `recommendations.md`.

## Security model

- No authentication in v1. Profiles are a declared id, not a login.
- Settings UI is admin-only; settings/job/system APIs remain reachable (protect at the network edge).
- Secrets encrypted at rest with Data Protection keys under the data path.
- Taste and embedding inference stay inside the container.

## Frontend routes

| Path | Notes |
| --- | --- |
| `/who` | Profile selection |
| `/catalog` | One source at a time |
| `/list` | Cross-source favorites |
| `/for-you` | Recommendations |
| `/ratings`, `/taste` | Votes and onboarding |
| `/settings` | Admin only in UI |

Title dialogs stay on the route that opened them. My list is never a catalog query flag.
