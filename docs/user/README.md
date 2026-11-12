# User guide

Pickarr is a **self-hosted** catalog browser for your streaming subscriptions. It keeps a local copy of what a streaming service offers in a country, enriches titles with TMDB metadata, and lets household profiles rate titles and send them to **Sonarr** or **Radarr**.

There is **no login**—profiles are a logical partition, not accounts. Up to **10 profiles** share one install (favorites, ratings, watched state, and recommendations are per profile). Catalog data, settings, and the Arr library stay shared. Run Pickarr on a trusted network or behind a reverse proxy that authenticates.

## What you need

| Service | Role |
| --- | --- |
| [Watchmode](https://api.watchmode.com/) API key | Catalog sync. Free plan: about 2 500 credits/month, up to 3 countries; leaving dates are a short preview. |
| [TMDB](https://developer.themoviedb.org/) API key | Posters, overviews, genres, external ids. |
| Sonarr and/or Radarr (optional) | Add, status, and delete in your library. Series need Sonarr; movies need Radarr. Without Arr you can still browse catalogs and use recommendations. |
| Docker host (**linux/amd64**) | One container; data under `/config` on **local disk** (not SMB/NFS — SQLite WAL). |

Optional: OMDb API key for IMDb ratings; ONNX model files under the data path for richer local recommendations.

## First-run checklist

1. [Deploy](deployment.md) the container with a writable `/config` volume.
2. Open the UI (default `http://localhost:8080`).
3. Choose or create a profile on **Who** (`/who`). The first profile is the admin.
4. As admin, open **Settings**: set Watchmode and TMDB keys. Optionally configure Sonarr and/or Radarr URL + API key for library actions. Use **Test connection** and pick quality profile / root folder defaults.
5. With a Watchmode key present, Pickarr enables seeded catalogs (Netflix IT and Netflix US) and queues the first import unless you disabled auto-import.
6. Wait for catalog import and metadata enrichment under **Jobs**, then open **Catalog**, pick a source (provider + country), and browse.
7. Rate titles or use **Teach your taste** from **For you** while personalization is still learning. Use **Hide** or **Not interested** to keep suggestions clean.

Secrets saved in the UI are encrypted with ASP.NET Data Protection under `/config`. The API only reports whether a secret is set. You can also pin settings from the environment; see [Configuration](configuration.md).

## Main surfaces

| Route | Purpose |
| --- | --- |
| `/who` | Profile picker and profile management |
| `/catalog` | Browse one catalog source (provider + country) |
| `/list` | My list — favorites across providers/countries for the active profile |
| `/for-you` | Local recommendations for the active profile |
| `/ratings` | Explicit votes; export/import |
| `/taste` | Onboarding to teach taste while learning is cold |
| `/settings` | Admin-only settings, jobs, system (non-admin users are redirected) |

## Next steps

- [Features](features.md) — what the product does in detail
- [Deployment](deployment.md) — Docker, tags, volumes, health
- [Configuration](configuration.md) — env vars and settings keys
- [Pickarr vs Seerr](../pickarr-vs-seerr.md) — when to use which tool
