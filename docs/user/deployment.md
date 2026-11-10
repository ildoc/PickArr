# Deployment

One container serves the API and the built web app on port **8080**. SQLite (WAL mode) and Data Protection keys live under `/config`. Use a **local** disk or named volume — do not put the data directory on a network share (SMB/NFS).

## Image

Published images are **linux/amd64** on GitHub Container Registry. For this repository: `ghcr.io/ildoc/Pickarr`.

| Tag | When |
| --- | --- |
| `edge` | Default branch |
| `X.Y.Z.N` | Default branch build (`N` commits after the latest release tag) |
| `X.Y.Z`, `latest` | Git tag `vX.Y.Z` / `X.Y.Z` |

The process runs as non-root user `app` (uid **1654**). A new named volume inherits ownership from the image. A host directory you create must be writable by uid 1654.

## Compose

The repository `docker-compose.yml` builds `pickarr:local` and stores data in the `pickarr-config` named volume.

```bash
docker compose up -d
curl -fsS http://localhost:8080/health
```

Open `http://localhost:8080`. To pull a published image instead of building, comment out `build:` and set:

```yaml
image: ghcr.io/ildoc/Pickarr:edge
```

Example environment block:

```yaml
environment:
  - TZ=Europe/Rome
  - Pickarr__BaseUrl=http://localhost:8080
  # - WATCHMODE_API_KEY=
  # - TMDB_API_KEY=
  # - SONARR_URL=http://sonarr:8989
  # - SONARR_API_KEY=
  # - RADARR_URL=http://radarr:7878
  # - RADARR_API_KEY=
volumes:
  - pickarr-config:/config
```

Full settings reference: [Configuration](configuration.md).

## Prebuilt image

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

Health from the host: `curl -fsS http://localhost:8080/health`. Inside the container the healthcheck uses `/usr/local/bin/wget`.

## Build locally

```bash
docker build -f src/Pickarr.Api/Dockerfile -t pickarr:local .
```

Optional build-arg `VERSION` sets the assembly / UI version string (defaults to `0.0.0`).

## Data directory

| Path (inside container) | Contents |
| --- | --- |
| `/config/Pickarr.db` | Application SQLite database |
| `/config/jobs.db` | Hangfire SQLite storage (default) |
| `/config/keys/` | ASP.NET Data Protection key ring |
| `/config/models/` | Optional ONNX embedding models |

**Do not delete or replace the database to “fix” a schema problem.** It holds catalog data obtained from limited/paid API calls and all per-profile personalization. Prefer waiting for application migrations; losing `/config` means re-consuming Watchmode quota and wiping household taste data.

## Reverse proxy

- Point the proxy at the container’s port 8080.
- Set `Pickarr__BaseUrl` to the public URL when webhook URLs must be generated.
- If the app is served under a subpath, set `Pickarr__PathBase` accordingly.
- Terminate TLS and authentication at the proxy. Pickarr itself has no login.

## Time zone

Set `TZ` (for example `Europe/Rome`) so cron schedules are interpreted in your local zone. The image includes tzdata; default without override is UTC.
