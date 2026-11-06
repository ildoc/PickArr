# PickArr

Self-hosted catalog browser for Netflix (IT/US) with TMDB metadata and Sonarr/Radarr integration.

MVP scope: Watchmode catalog sync (incremental + full + leaving preview), enrichment, UI grid/filters/favorites, Arr add/delete/status, admin jobs & credit quota, Docker amd64, GitHub Actions.

## Prerequisites

- .NET 10 SDK
- Node.js 24+
- Docker or Podman (optional, for container runs)

## Development

```bash
# API
dotnet run --project src/PickArr.Api

# Web app (another terminal)
cd src/PickArr.WebApp
npm ci
npm run dev
```

- API + SPA (production build served by API): `http://localhost:5000`
- Vite dev: `http://localhost:5173` (proxies `/api` and `/health`)

Data directory defaults to `%LOCALAPPDATA%/PickArr` on Windows / `~/.local/share/PickArr` style override via `PickArr__DataPath`.

Configure Watchmode API key, TMDB token, and Sonarr/Radarr URLs under **Settings** in the UI (secrets stored encrypted with ASP.NET Data Protection).

### Tests

```bash
dotnet test
cd src/PickArr.WebApp && npm test -- --run && npm run build
```

## Docker

Image is **linux/amd64** only. Persist SQLite + Data Protection keys on `/config`.

```bash
# build locally
docker build -f src/PickArr.Api/Dockerfile -t pickarr:local .

# run (named volume)
docker run --rm -p 8080:8080 -v pickarr-config:/config pickarr:local

# health
curl -fsS http://localhost:8080/health
```

Compose example (bind-mount preferred on a local disk — avoid SMB/NFS for SQLite WAL):

```bash
docker compose up -d
curl -fsS http://localhost:8080/health
```

Open `http://localhost:8080`, set API keys in Settings, then trigger catalog / leaving jobs from Admin.

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs on pull requests, pushes to `main`, and tags.

| Job | Checks |
| --- | --- |
| Backend lint | `dotnet format --verify-no-changes`; NuGet vulnerability report (does not fail the job) |
| Frontend lint | `npm run lint`, `npm run typecheck` |
| Backend test | `dotnet test` (Release); TRX uploaded as an artifact |
| Frontend test | `npm test`, `npm run build` |
| Image | `linux/amd64` build of `src/PickArr.Api/Dockerfile` |

The image is pushed to `ghcr.io/<owner>/<repo>` only from the default branch (`edge` and `sha-<commit>`) and from git tags (the tag, `latest`, and `sha-<commit>`). `VERSION` is `0.0.0-<short sha>` on branch builds and the tag name without a leading `v` on releases. The first push creates a private GHCR package; grant this repository write access on the package if the push is rejected.

## Ops notes

- Watchmode **free** plan: leaving dates are a ~10-title preview per country (see Admin leaving job + UI note).
- SQLite uses **WAL**; keep `/config` on local storage.
- No authentication in MVP (single-user LAN assumption).
