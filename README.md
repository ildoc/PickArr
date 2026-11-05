# PickArr

Self-hosted catalog browser for Netflix (IT/US) with TMDB metadata and Sonarr/Radarr integration.

MVP scope: Watchmode catalog sync (incremental + full + leaving preview), enrichment, UI grid/filters/favorites, Arr add/delete/status, admin jobs & credit quota, Docker amd64, GitLab CI (buildah).

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

GitLab CI (`.gitlab-ci.yml`):

| Stage | Jobs |
| --- | --- |
| lint | `dotnet format`, frontend lint/typecheck |
| test | `dotnet test`, frontend build |
| package | `buildah bud` → `$CI_REGISTRY_IMAGE:$CI_COMMIT_SHORT_SHA` (amd64) |
| release | tag push to registry on default branch / tags |

Pipeline must run on a GitLab runner with buildah; local substitute is the Docker smoke above.

## Ops notes

- Watchmode **free** plan: leaving dates are a ~10-title preview per country (see Admin leaving job + UI note).
- SQLite uses **WAL**; keep `/config` on local storage.
- No authentication in MVP (single-user LAN assumption).
