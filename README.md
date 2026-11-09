# PickArr

PickArr is a single-user, self-hosted catalog browser. It keeps a local catalog of what a streaming service offers in a country, starting with Netflix in Italy and the United States, and lets you browse, search, and save titles. From a title you can send a series to Sonarr or a movie to Radarr, see the library status, and remove it again.

Metadata comes from TMDB. The catalog itself comes from Watchmode. Likes and dislikes stay on this machine and feed a local *For you* page; an optional ONNX model improves similar titles. There is no login. Run it on a trusted network or behind a reverse proxy that authenticates.

The grid stays empty until Sonarr and/or Radarr are configured. Series need Sonarr; movies need Radarr.

## Deploy

The published image is **linux/amd64**. One container serves the API and the built web app on port **8080**. SQLite (WAL) and the ASP.NET Data Protection keys live in `/config`. Keep that directory on a local disk; do not put it on SMB or NFS.

The process runs as the non-root user `app` (uid 1654). A new volume inherits that ownership from the image. A directory you create yourself must be writable by uid 1654, or the app cannot create the database and the log files.

### Compose

`docker-compose.yml` builds `pickarr:local` and stores data in the named volume `pickarr-config`.

```bash
docker compose up -d
curl -fsS http://localhost:8080/health
```

Open `http://localhost:8080`. To use a published image instead of a local build, comment out `build:` and set:

```yaml
image: ghcr.io/<owner>/<repo>:edge
```

Tags from the default branch are `edge` and the build version `X.Y.Z.N` (`N` commits after the latest `vX.Y.Z` tag). A git tag `vX.Y.Z` is published as `X.Y.Z` and `latest`. Before the first tag, branch images are `0.0.0.N`.

### Prebuilt image without Compose

```bash
docker run -d --name pickarr \
  -p 8080:8080 \
  -e TZ=Europe/Rome \
  -e WATCHMODE_API_KEY=YOUR_KEY \
  -e TMDB_TOKEN=YOUR_TOKEN \
  -e SONARR_URL=http://sonarr:8989 \
  -e SONARR_API_KEY=YOUR_SONARR_KEY \
  -e RADARR_URL=http://radarr:7878 \
  -e RADARR_API_KEY=YOUR_RADARR_KEY \
  -v pickarr-config:/config \
  ghcr.io/<owner>/<repo>:edge
```

`ASPNETCORE_URLS` defaults to `http://+:8080` and `PickArr__DataPath` defaults to `/config` in the image and in app settings.

Inside the container, the healthcheck calls `/usr/local/bin/wget`. From the host, `curl -fsS http://localhost:8080/health` is enough.

When a Watchmode API key is present at startup, PickArr enables the seeded country catalogs and queues the first import. Set `PickArr__Catalog__AutoInitialImport=false` to skip that.

### Build the image yourself

```bash
docker build -f src/PickArr.Api/Dockerfile -t pickarr:local .
```

## Settings

Two layers. Process settings come from the environment and the ASP.NET configuration. Application settings live in the database and can also be declared with environment variables. The same keys can be edited under **Settings** in the UI; secrets saved there are encrypted with Data Protection and the API only reports whether they are set.

### Declaring application settings

Use flat `SCREAMING_SNAKE` names derived from the settings key (`.` and camelCase become `_`):

```text
watchmode.apiKey           →  WATCHMODE_API_KEY
tmdb.token                 →  TMDB_TOKEN
sonarr.url                 →  SONARR_URL
sonarr.apiKey              →  SONARR_API_KEY
radarr.url                 →  RADARR_URL
reco.weights.like          →  RECO_WEIGHTS_LIKE
sonarr.syncIntervalMinutes →  SONARR_SYNC_INTERVAL_MINUTES
```

The legacy form `PickArr__Settings__` with each `.` replaced by `__` still works. When both are set, the flat name wins.

A non-empty value wins over the database for the life of the process. Saving the same key in the UI does not replace it, and a declared secret is not written to the database. An empty variable is ignored. `true` and `false` are the only booleans. Numbers use a decimal point (`0.8`, not `0,8`). An unknown nested key, or a number or boolean that does not parse, stops startup.

Sonarr and Radarr defaults are either nested fields or one JSON object:

```text
SONARR_DEFAULTS_QUALITY_PROFILE_ID=1
SONARR_DEFAULTS_ROOT_FOLDER_PATH=/tv
```

```text
SONARR_DEFAULTS={"qualityProfileId":1,"rootFolderPath":"/tv"}
```

A trailing `/` on `sonarr.url` and `radarr.url` is removed. `catalog.initialImportStarted` is runtime state and cannot be set this way.

### Application keys

| Key | Default | |
| --- | --- | --- |
| `watchmode.apiKey` | | Watchmode API key. Secret. |
| `watchmode.plan` | `free` | `free` or `paid`. Paid unlocks the change feed and full leaving dates. |
| `watchmode.quota` | `2500` | Monthly credit quota. |
| `watchmode.reservePercent` | `10` | Quota held back from jobs. |
| `watchmode.maxCreditsPerRun` | `80` | Cap for one catalog run. |
| `watchmode.billingDayOfMonth` | `1` | Day the monthly quota resets. |
| `tmdb.token` | | TMDB v4 read token. Secret. |
| `omdb.apiKey` | | Optional OMDb key. Secret. |
| `sonarr.url` | | Sonarr base URL, for example `http://sonarr:8989`. |
| `sonarr.apiKey` | | Secret. |
| `sonarr.syncIntervalMinutes` | `15` | Library sync interval. |
| `sonarr.defaults.qualityProfileId` | | Quality profile used when adding a series. |
| `sonarr.defaults.rootFolderPath` | | Root folder used when adding a series. |
| `radarr.url` | | Radarr base URL. |
| `radarr.apiKey` | | Secret. |
| `radarr.syncIntervalMinutes` | `15` | |
| `radarr.defaults.qualityProfileId` | | |
| `radarr.defaults.rootFolderPath` | | |
| `webhooks.token` | | Secret. Stored for webhook checks; the webhook endpoint is not served yet. |
| `ui.language` | `en` | Stored preference. The shipped UI is English. |
| `catalog.newWindowDays` | `14` | Window for the New filter. |
| `catalog.leavingWindowDays` | `30` | Window for the Leaving filter. |
| `catalog.removedWindowDays` | `30` | Window for Recently removed. |
| `catalog.removalConfirmations` | `2` | Full syncs that must agree before a title is marked removed. |
| `catalog.maxRemovalRatio` | `0.1` | Full sync aborts when too large a share of the catalog would disappear. |
| `catalog.staleWarningDays` | `21` | Age after which a source is shown as stale. |
| `reco.enabled` | `true` | Local recommendations. |
| `reco.useImplicit` | `true` | Treat favorites and library adds as signals. |
| `reco.excludeInLibrary` | `true` | Drop titles already in Sonarr or Radarr. |
| `reco.n0` | `30` | Ratings before the personal score takes over from the prior. |
| `reco.weights.like` | `1` | |
| `reco.weights.dislike` | `-1` | |
| `reco.weights.favorite` | `0.5` | |
| `reco.weights.arrAdd` | `0.6` | |
| `reco.weights.downloaded` | `0.4` | |
| `reco.mmr.lambda` | `0.7` | Diversity. `0` turns MMR off. |
| `reco.explore.rate` | `0.1` | Share of exploratory slots. `0` turns exploration off. |
| `reco.ml.minRatings` | `100` | Explicit ratings required before the supervised model is used. |
| `reco.events.enabled` | `false` | Local recommendation event log. |
| `reco.embeddings.enabled` | `true` | Use the ONNX model when it is installed. |
| `reco.embeddings.modelId` | `all-MiniLM-L6-v2@q8` | |
| `reco.embeddings.threads` | `2` | ONNX intra-op threads. |
| `reco.embeddings.batchSize` | `16` | |
| `reco.embeddings.minCoverage` | `0.8` | Below this coverage, similar titles fall back to level 1. |
| `reco.embeddings.throttle` | `true` | Pause between embedding batches. |

On the free Watchmode plan, leaving dates are a short preview (about ten titles per country), not a full calendar.

### Process

| Variable | Default | |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address. |
| `PickArr__DataPath` | `/config` | SQLite, Data Protection keys, and logs. Local `dotnet run` overrides this via `launchSettings.json` (`.data`). |
| `TZ` | `UTC` in the image | Time zone for schedules and timestamps. The image includes tzdata. |
| `PickArr__Models__Path` | `<DataPath>/models` | ONNX model files. |
| `PickArr__Catalog__AutoInitialImport` | `true` | Queue the first catalog import once a Watchmode key is available. |
| `PickArr__Watchmode__BaseUrl` | `https://api.watchmode.com/v1/` | Watchmode API root. |
| `PickArr__Watchmode__RequestsPerSecond` | `1` | Outbound Watchmode rate limit. |
| `PickArr__Tmdb__BaseUrl` | `https://api.themoviedb.org/3/` | TMDB API root. |
| `PickArr__Tmdb__RequestsPerSecond` | `20` | Outbound TMDB rate limit. |
| `Serilog__MinimumLevel__Default` | `Information` | Log level (`Debug`, `Information`, `Warning`, …). |

`Hangfire__Storage` defaults to `Sqlite` (`jobs.db` under the data path). `Memory` drops the job queue on restart; recurring schedules are rebuilt from the catalog sources.

## Development

- .NET 10 SDK
- Node.js 24+

```bash
dotnet run --project src/PickArr.Api
```

The API listens on `http://localhost:5000` and, once the web app has been built, serves it too.

```bash
cd src/PickArr.WebApp
npm ci
npm run dev
```

Vite is at `http://localhost:5173` and proxies `/api` and `/health`.

`PickArr__DataPath` in `src/PickArr.Api/Properties/launchSettings.json` points at `.data` in the repository. Pass that path explicitly if a second process would otherwise open another database.

```bash
dotnet test
cd src/PickArr.WebApp && npm test -- --run && npm run typecheck && npm run lint && npm run build
```

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs on pull requests, pushes to `main`, and tags.

| Job | Checks |
| --- | --- |
| Backend lint | `dotnet format --verify-no-changes`; NuGet vulnerability report (does not fail the job) |
| Frontend lint | `npm run lint`, `npm run typecheck` |
| Backend test | `dotnet test` (Release); TRX uploaded as an artifact |
| Frontend test | `npm test`, `npm run build` |
| Image | `linux/amd64` build of `src/PickArr.Api/Dockerfile` |

The image is pushed to `ghcr.io/<owner>/<repo>` only from the default branch and from git tags. A tag `vX.Y.Z` or `X.Y.Z` sets the version to `X.Y.Z` and publishes `X.Y.Z` and `latest`. Each later build uses `X.Y.Z.N`, where `N` is the number of commits after that tag, until the next tag. The default branch is also tagged `edge`. With no tag yet, the version is `0.0.0.N` (`N` is the commit count). The same string is the image's `VERSION`. The first push creates a private GHCR package; grant this repository write access on the package if the push is rejected.
