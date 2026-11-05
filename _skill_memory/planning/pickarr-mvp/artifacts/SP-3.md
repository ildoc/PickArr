# SP-3 — Sonarr / Radarr (T18)

> Data: 2026-11-05  
> Fonti: docs ufficiali API v3 (`sonarr.tv/docs/api`, OpenAPI Radarr develop, schema starr/golift + sonarr-api-rs); **nessuna istanza live** (D-010)  
> Target prodotto: **ultime release** Sonarr 4.x / Radarr 5.x (API path resta `/api/v3`)

## Auth e base

| | Sonarr | Radarr |
| --- | --- | --- |
| Base | `{url}/api/v3` | `{url}/api/v3` |
| Auth | header `X-Api-Key` | header `X-Api-Key` |
| Status | `GET /system/status` → `version`, `appName` | idem |

## Endpoint usati da PickArr (sync stato)

| Uso | Sonarr | Radarr |
| --- | --- | --- |
| Libreria | `GET /series` | `GET /movie` |
| Coda | `GET /queue?page=&pageSize=&includeSeries=true` | `GET /queue?…&includeMovie=true` |
| Opzioni | `/qualityprofile`, `/rootfolder`, `/tag` | idem |
| Lookup | `/series/lookup?term=tvdb:{id}` | `/movie/lookup?term=tmdb:{id}` |
| Find | `/series?tvdbId=` | `/movie?tmdbId=` |
| Add | `POST /series` | `POST /movie` |
| Delete | `DELETE /…/{id}?deleteFiles=&addImportExclusion=` | idem |
| Search | `POST /command` `SeriesSearch` | `MoviesSearch` |

Coda: risposta **paginata** `{ page, pageSize, totalRecords, records[] }`. Record: `seriesId` / `movieId`, `size`, `sizeleft`, `status`.

## Statistiche serie (K-009 → FACT)

Da `SeriesStatisticsResource` / `Season.statistics`:

| Campo | Semantica adottata |
| --- | --- |
| `episodeFileCount` | file su disco |
| `episodeCount` | episodi **aired + monitorati** (attesi per BR-10) |
| `totalEpisodeCount` | tutti gli episodi noti (incl. non aired / non monitorati) |

**Regola PickArr:** usare `episodeCount` per Downloaded/Partial/Missing; se `episodeCount == 0` → Missing (serie non ancora iniziata / niente da monitorare). Season 0 (specials) inclusa solo se `monitored`.

## Monitor / add options (per T21)

Sonarr `addOptions.monitor` (enum docs): `all`, `future`, `missing`, `existing`, `firstSeason`, `lastSeason`, `latestSeason`, `pilot`, `recent`, `none`.  
Anche `monitorNewItems`: tipicamente `all` | `none`.  
`seriesType`: `standard`, `daily`, `anime`.

Radarr: `minimumAvailability` = `announced` | `inCinemas` | `released`; `addOptions.monitor` tipicamente `movieOnly` (+ search flag).

## Default profilo/root (D-018 / D-01)

Nessun “default esplicito” obbligatorio nelle API: liste `qualityprofile` / `rootfolder`.  
PickArr: settings `sonarr.defaults` / `radarr.defaults` (`qualityProfileId`, `rootFolderPath`); se assenti → **primo** elemento delle liste istanza.

## Delete

Query bool `deleteFiles`, `addImportExclusion` su DELETE — confermati dagli OpenAPI pubblici.

## Fixture

`backend/tests/fixtures/sonarr/` · `backend/tests/fixtures/radarr/`  
(`system-status`, `series|movie-library`, `queue-page1`, `qualityprofiles`, `rootfolders`)

## Gap residui (ANALYSIS)

| Gap | Impatto | Mitigazione |
| --- | --- | --- |
| Nessuna verifica live su istanza reale | possibili campi opzionali/extra | WireMock + fixture; contract test |
| Webhook payload shape | TR futura (refresh job) | fuori sync periodica MVP |
| Paginazione libreria | Sonarr/Radarr restituiscono array completo tipico | se serve, aggiungere in seguito |

## Impatto F-001 / K-009

| ID | Esito |
| --- | --- |
| K-009 | **FACT** — payload allineati a OpenAPI/docs pubblici v3 |
| F-001 (Arr) | **ridotto**: contratto sync/status chiuso da docs; live smoke opzionale |
