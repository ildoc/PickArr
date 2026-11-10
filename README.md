# Pickarr

Pickarr is a **single-user, self-hosted** web app for browsing streaming catalogs and sending titles to your media stack.

It syncs what a service offers in a country (via [Watchmode](https://watchmode.com)), enriches titles with [TMDB](https://www.themoviedb.org/) metadata, and lets you add series to **Sonarr** or movies to **Radarr**. There is **no login**: run it on a trusted network or behind a reverse proxy that authenticates.

**Catalog.** Browse by catalog source (provider + country). Search and filter (type, genre, year, library state). Quick views: All, New, Leaving, Recently removed. Seeded sources include Netflix IT and Netflix US; more providers/countries can be enabled from Settings within Watchmode plan limits.

**Library.** Title cards and the detail dialog show Arr status. Add or remove titles in Sonarr/Radarr. The grid stays empty until at least one of Sonarr or Radarr is configured (series need Sonarr; movies need Radarr).

**My list & ratings.** Cross-provider favorites on `/list`. Explicit votes (love / like / dislike / not interested), hide, and a local **For you** page (`/for-you`) with similar titles in the detail dialog. Votes never leave your install; an optional ONNX embedding model improves similarity when installed.

**Ops.** Jobs, Watchmode credit usage, and settings live in the UI. One Docker image serves the API and the built SPA on port **8080**.

## Documentation

| Audience | Start here |
| --- | --- |
| Operators | [docs/user/](docs/user/README.md) — features, [deployment](docs/user/deployment.md), [configuration](docs/user/configuration.md) |
| Contributors | [docs/developer/](docs/developer/README.md) — layout, tests, [architecture](docs/developer/architecture.md) |
| Comparison | [Pickarr vs Seerr](docs/pickarr-vs-seerr.md) |
| Index | [docs/README.md](docs/README.md) |

Product specifications (source of truth): [functional-spec](docs/functional-spec.md), [technical-spec](docs/technical-spec.md), [recommendations](docs/recommendations.md). Keeping docs in sync: [docs/AGENTS.md](docs/AGENTS.md).

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

Application settings can also be pinned from the environment (`WATCHMODE_API_KEY`, `TMDB_API_KEY`, `SONARR_URL`, …). A non-empty env value overrides the database for the process lifetime and is not written back from the UI. Full key lists, process variables, and naming rules: [docs/user/configuration.md](docs/user/configuration.md).

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

Contributor workflow and constraints: [docs/developer/](docs/developer/README.md). Product specifications: [functional-spec](docs/functional-spec.md), [technical-spec](docs/technical-spec.md), [recommendations](docs/recommendations.md).

---

## Deploy

Full deploy notes (tags, volumes, reverse proxy, data directory): [docs/user/deployment.md](docs/user/deployment.md). Settings reference: [docs/user/configuration.md](docs/user/configuration.md).

One container serves the API and the built web app on port **8080**. SQLite (WAL) and Data Protection keys live in `/config` — use a **local** disk or named volume, not a network share.

Published images are **linux/amd64** on `ghcr.io/ildoc/Pickarr` (`edge`, `X.Y.Z.N`, SemVer / `latest`). Process user: `app` (uid **1654**).

### Compose

`docker-compose.yml` builds `pickarr:local` and stores data in `pickarr-config`.

```bash
docker compose up -d
curl -fsS http://localhost:8080/health
```

To pull a published image instead of building, comment out `build:` and set `image: ghcr.io/ildoc/Pickarr:edge`.

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

```bash
docker build -f src/Pickarr.Api/Dockerfile -t pickarr:local .
```
