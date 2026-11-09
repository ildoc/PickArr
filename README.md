# Pickarr

Pickarr is a **single-user, self-hosted** web app for browsing streaming catalogs and sending titles to your media stack.

It syncs what a service offers in a country (via [Watchmode](https://watchmode.com)), enriches titles with [TMDB](https://www.themoviedb.org/) metadata, and lets you add series to **Sonarr** or movies to **Radarr**. There is **no login**: run it on a trusted network or behind a reverse proxy that authenticates.

**Catalog.** Browse by catalog source (provider + country). Search and filter (type, genre, year, library state). Quick views: All, New, Leaving, Recently removed. Seeded sources include Netflix IT and Netflix US; more providers/countries can be enabled from Settings within Watchmode plan limits.

**Library.** Title cards and the detail dialog show Arr status. Add or remove titles in Sonarr/Radarr. The grid stays empty until at least one of Sonarr or Radarr is configured (series need Sonarr; movies need Radarr).

**My list & ratings.** Cross-provider favorites on `/list`. Explicit votes (love / like / dislike / not interested), hide, and a local **For you** page (`/for-you`) with similar titles in the detail dialog. Votes never leave your install; an optional ONNX embedding model improves similarity when installed.

**Ops.** Jobs, Watchmode credit usage, and settings live in the UI. One Docker image serves the API and the built SPA on port **8080**.

---

## What you need

| Service | Role |
| --- | --- |
| [Watchmode](https://api.watchmode.com/) API key | Catalog sync. Free plan: ~2 500 credits/month, up to 3 countries; leaving dates are a short preview. |
| [TMDB](https://developer.themoviedb.org/) API key | Posters, overviews, genres, external ids. |
| Sonarr and/or Radarr | Library add / status / delete. |
| A host for Docker (**linux/amd64**) | One container; data in `/config` on **local disk** (not SMB/NFS — SQLite WAL). |

Optional: OMDb API key; ONNX model files under the data path for richer recommendations.

---

## Configure and use

1. Deploy the container (see [Deploy](#deploy)) with a writable `/config` volume.
2. Open `http://localhost:8080` (or your reverse-proxy URL).
3. Under **Settings**, set Watchmode and TMDB keys, then Sonarr/Radarr URL + API key. Use **Test connection** for Arr instances and choose quality profile / root folder defaults.
4. With a Watchmode key present, Pickarr enables seeded catalogs and queues the first import (disable with `Pickarr__Catalog__AutoInitialImport=false`).
5. Wait for catalog import and metadata enrichment (see **Jobs**). Then open **Catalog**, pick a source, and browse.
6. Rate titles or use **Teach your taste** from **For you** while learning is below the personalization threshold. Use **Hide** or **Not interested** to keep suggestions clean.

Secrets saved in the UI are encrypted with ASP.NET Data Protection under `/config`. The API only reports whether a secret is set.

You can also set application settings from the environment. A **non-empty** env value overrides the database for the process lifetime and is not written back when you save in the UI. Empty variables are ignored.

### Environment naming

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

The form `Pickarr__Settings__…` (`.` → `__`) still works; when both are set, the flat name wins.

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

### Application settings

| Key | Default | |
| --- | --- | --- |
| `watchmode.apiKey` | | Watchmode API key. Secret. |
| `watchmode.plan` | `free` | `free` or `paid`. |
| `watchmode.quota` | `2500` | Monthly credit quota. |
| `watchmode.reservePercent` | `10` | Quota held back from jobs. |
| `watchmode.maxCreditsPerRun` | `80` | Cap for one catalog run. |
| `watchmode.billingDayOfMonth` | `1` | Day the monthly quota resets. |
| `tmdb.apiKey` | | TMDB v3 key or v4 read token. Secret. |
| `omdb.apiKey` | | Optional. Secret. |
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
| `reco.useImplicit` | `true` | Favorites / library / hide as signals. |
| `reco.excludeInLibrary` | `true` | Exclude titles already in Arr from For you by default. |
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

### Process settings

| Variable | Default | |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address. |
| `Pickarr__DataPath` | `/config` | SQLite, Data Protection keys, logs. |
| `TZ` | `UTC` in the image | Schedules and timestamps (tzdata is included). |
| `Pickarr__Models__Path` | `<DataPath>/models` | ONNX model files. |
| `Pickarr__Catalog__AutoInitialImport` | `true` | Queue first import when a Watchmode key is available. |
| `Pickarr__Watchmode__BaseUrl` | `https://api.watchmode.com/v1/` | |
| `Pickarr__Watchmode__RequestsPerSecond` | `1` | |
| `Pickarr__Tmdb__BaseUrl` | `https://api.themoviedb.org/3/` | |
| `Pickarr__Tmdb__RequestsPerSecond` | `20` | |
| `Serilog__MinimumLevel__Default` | `Information` | |

`Hangfire__Storage` defaults to `Sqlite` (`jobs.db` under the data path). `Memory` drops the queue on restart.

The header shows the running version from `GET /api/v1/system/status` (same string as the Docker image `VERSION` / assembly informational version).

---

## Development

- .NET 10 SDK  
- Node.js 24+

```bash
dotnet run --project src/Pickarr.Api
```

API: `http://localhost:5000` (serves the web app after a frontend build).

```bash
cd src/Pickarr.WebApp
npm ci
npm run dev
```

Vite: `http://localhost:5173` (proxies `/api` and `/health`).

Local data path is `.data` via `launchSettings.json`. Pass `Pickarr__DataPath` explicitly if another process would open a second database.

```bash
dotnet test
cd src/Pickarr.WebApp && npm test -- --run && npm run typecheck && npm run lint && npm run build
```

Product detail lives in `docs/documento-funzionale.md`, `docs/specifica-tecnica.md`, and `docs/raccomandazioni.md`.

---

## Deploy

One container serves the API and the built web app on port **8080**. SQLite (WAL) and Data Protection keys live in `/config` — use a **local** disk or named volume, not a network share.

The process runs as non-root user `app` (uid **1654**). A new named volume inherits ownership from the image. A host directory you create must be writable by uid 1654.

Published images are **linux/amd64** on `ghcr.io/<owner>/<repo>` (for this repository: `ghcr.io/ildoc/Pickarr`).

| Tag | When |
| --- | --- |
| `edge` | Default branch |
| `X.Y.Z.N` | Default branch build (`N` commits after the latest release tag) |
| `X.Y.Z`, `latest` | Git tag `vX.Y.Z` / `X.Y.Z` |

### Compose

`docker-compose.yml` builds `pickarr:local` and stores data in `pickarr-config`.

```bash
docker compose up -d
curl -fsS http://localhost:8080/health
```

Open `http://localhost:8080`. To pull a published image instead of building, comment out `build:` and set:

```yaml
image: ghcr.io/ildoc/Pickarr:edge
```

### Prebuilt image

```bash
docker run -d --name pickarr \
  -p 8080:8080 \
  -e TZ=Europe/Rome \
  -e WATCHMODE_API_KEY=YOUR_KEY \
  -e TMDB_API_KEY=YOUR_KEY \
  -e SONARR_URL=http://sonarr:8989 \
  -e SONARR_API_KEY=YOUR_SONARR_KEY \
  -e RADARR_URL=http://radarr:7878 \
  -e RADARR_API_KEY=YOUR_RADARR_KEY \
  -v pickarr-config:/config \
  ghcr.io/ildoc/Pickarr:edge
```

Health: from the host, `curl -fsS http://localhost:8080/health`. Inside the container the healthcheck uses `/usr/local/bin/wget`.

### Build locally

```bash
docker build -f src/Pickarr.Api/Dockerfile -t pickarr:local .
```

Optional build-arg `VERSION` sets the assembly / UI version string (defaults to `0.0.0`).
