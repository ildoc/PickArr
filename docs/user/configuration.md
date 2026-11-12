# Configuration

Settings can be edited in the UI (admin profile) or pinned from the environment. A **non-empty** environment value overrides the database for the process lifetime and is **not** written back when you save in the UI. Empty variables are ignored.

Secrets saved only in the UI are encrypted with ASP.NET Data Protection under the data path. The API reports whether a secret is set, never the value.

## Environment naming

Flat `SCREAMING_SNAKE` names map from setting keys (`.` and camelCase → `_`):

```text
watchmode.apiKey           →  WATCHMODE_API_KEY
tmdb.apiKey                →  TMDB_API_KEY
sonarr.url                 →  SONARR_URL
sonarr.apiKey              →  SONARR_API_KEY
radarr.url                 →  RADARR_URL
reco.weights.love          →  RECO_WEIGHTS_LOVE
sonarr.syncIntervalMinutes →  SONARR_SYNC_INTERVAL_MINUTES
```

The legacy form `Pickarr__Settings__…` (`.` → `__`) still works; when both are set, the flat name wins.

Booleans are `true` / `false`. Numbers use a decimal point (`0.8`). An unknown nested key, or a value that does not parse, stops startup.

Sonarr/Radarr defaults as nested fields or one JSON object:

```text
SONARR_DEFAULTS_QUALITY_PROFILE_ID=1
SONARR_DEFAULTS_ROOT_FOLDER_PATH=/tv
```

```text
SONARR_DEFAULTS={"qualityProfileId":1,"rootFolderPath":"/tv"}
```

A trailing `/` on `sonarr.url` and `radarr.url` is stripped.

## Process settings

| Variable | Default | Description |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address. |
| `Pickarr__DataPath` | `/config` | SQLite, Data Protection keys, logs. |
| `TZ` | `UTC` in the image | Schedules and timestamps. |
| `Pickarr__BaseUrl` | (empty) | Public URL used when generating webhook URLs. |
| `Pickarr__PathBase` | (empty) | Path prefix behind a reverse proxy subpath. |
| `Pickarr__Models__Path` | `<DataPath>/models` | ONNX model files. |
| `Pickarr__Catalog__AutoInitialImport` | `true` | After onboarding completes (`setup.completed`), queue first import for enabled catalogs that have never been imported. |
| `Pickarr__Watchmode__BaseUrl` | `https://api.watchmode.com/v1/` | |
| `Pickarr__Watchmode__RequestsPerSecond` | `1` | |
| `Pickarr__Tmdb__BaseUrl` | `https://api.themoviedb.org/3/` | |
| `Pickarr__Tmdb__RequestsPerSecond` | `20` | |
| `LOG_LEVEL` | `Debug` | Serilog minimum (`Verbose` / `Debug` / `Information` / `Warning` / `Error` / `Fatal`). |
| `Hangfire__Storage` | `Sqlite` | `Memory` drops the queue on restart. |
| `Hangfire__Dashboard__Enabled` | `false` | Hangfire dashboard. |

## Application settings

| Key | Default | Notes |
| --- | --- | --- |
| `watchmode.apiKey` | | Secret. |
| `watchmode.plan` | `free` | `free` or `paid`. |
| `watchmode.quota` | `2500` | Monthly credit quota. |
| `watchmode.reservePercent` | `10` | Quota held back from jobs. |
| `watchmode.maxCreditsPerRun` | `80` | Cap for one catalog run. |
| `watchmode.billingDayOfMonth` | `1` | Day the monthly quota resets. |
| `tmdb.apiKey` | | TMDB v3 key or v4 read token. Secret. |
| `sonarr.url` | | e.g. `http://sonarr:8989` |
| `sonarr.apiKey` | | Secret. |
| `sonarr.syncIntervalMinutes` | `15` | |
| `sonarr.defaults.qualityProfileId` | | Used when adding a series. |
| `sonarr.defaults.rootFolderPath` | | |
| `radarr.url` | | |
| `radarr.apiKey` | | Secret. |
| `radarr.syncIntervalMinutes` | `15` | |
| `radarr.defaults.qualityProfileId` | | |
| `radarr.defaults.rootFolderPath` | | |
| `ui.language` | `en` | UI preference (shipped UI is English). |
| `catalog.newWindowDays` | `14` | New filter window. |
| `catalog.leavingWindowDays` | `30` | Leaving filter window. |
| `catalog.removedWindowDays` | `30` | Recently removed window. |
| `catalog.removalConfirmations` | `2` | Full syncs required before marking removed. |
| `catalog.maxRemovalRatio` | `0.1` | Abort full sync if too much of the catalog would vanish. |
| `catalog.staleWarningDays` | `21` | Age after which a source is shown as stale. |
| `reco.enabled` | `true` | Local recommendations. |
| `reco.useImplicit` | `true` | Global default; each profile has its own toggle. |
| `reco.excludeInLibrary` | `true` | Global default; each profile has its own toggle. |
| `reco.n0` | `30` | Effective ratings before full personalization. |
| `reco.weights.love` | `1.5` | |
| `reco.weights.like` | `1` | |
| `reco.weights.dislike` | `-1` | |
| `reco.weights.notInterested` | `-0.75` | |
| `reco.weights.favorite` | `0.5` | |
| `reco.weights.arrAdd` | `0.6` | |
| `reco.weights.downloaded` | `0.4` | |
| `reco.weights.watched` | `0` | Hide: exclude from For you; default weight is zero. |
| `reco.mmr.lambda` | `0.7` | Diversity (`0` disables). |
| `reco.explore.rate` | `0.1` | Exploration share (`0` disables). |
| `reco.ml.minRatings` | `100` | Minimum votes before supervised model. |
| `reco.events.enabled` | `false` | Local reco event log. |
| `reco.embeddings.enabled` | `true` | Use ONNX when the model is present. |
| `reco.embeddings.modelId` | `all-MiniLM-L6-v2@q8` | |
| `reco.embeddings.threads` | `2` | |
| `reco.embeddings.batchSize` | `16` | |
| `reco.embeddings.minCoverage` | `0.8` | |
| `reco.embeddings.throttle` | `true` | |
| `jobs.runHistoryLimit` | `10` | Finished SyncRuns kept per job (Kind + catalog source). Clamped 1–100. |

## Profiles and headers

After choosing a profile, the SPA sends `X-Pickarr-Profile` on API calls that need personal context. Browser storage:

| Key | Purpose |
| --- | --- |
| `pickarr.profileId` | Last selected profile id |
| `pickarr.sourceId.{profileId}` | Preferred catalog source for that profile |

## Version string

The header shows the running version from `GET /api/v1/system/status` (same string as the Docker image `VERSION` / assembly informational version).
