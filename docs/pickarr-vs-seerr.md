# Pickarr vs Seerr

Short functional comparison. [Seerr](https://seerr.dev/) is the unified successor to Overseerr and Jellyseerr. Pickarr and Seerr both talk to Sonarr/Radarr, but they solve different problems in a media stack.

## Purpose in one sentence

| Project | Purpose |
| --- | --- |
| **Pickarr** | Browse what your **streaming subscriptions** currently offer in a country, rate titles locally, and send selected titles straight to Sonarr/Radarr. |
| **Seerr** | Let **household/users of a media server** (Plex, Jellyfin, or Emby) **discover and request** media, with approvals, permissions, and library awareness. |

They can coexist: Pickarr answers “what is on Netflix IT right now that I might want?”; Seerr answers “can someone request *Dune* for the shared Plex/Jellyfin library?”

## At a glance

| Theme | Pickarr | Seerr |
| --- | --- | --- |
| Primary job | Streaming **catalog browser** + Arr add | Media **request manager** + discovery |
| Typical user | Single trusted household install | Multi-user media server community |
| Discovery source | Watchmode catalogs (provider + country) | TMDB-oriented discovery + media server library |
| Library truth | Sonarr/Radarr status | Plex / Jellyfin / Emby library scan (+ Arr) |
| Authentication | None (profiles only) | Media-server login and granular permissions |
| Request workflow | Direct add (no approval queue) | Request → approve/deny/auto-approve → Arr |
| Personalization | Local ratings + **For you** (per profile) | Watchlist / blocklist / recommendations in Seerr’s model |
| Deployment shape | One Docker image, SQLite under `/config` | Self-hosted app; SQLite or PostgreSQL |

## Feature comparison

| Capability | Pickarr | Seerr |
| --- | --- | --- |
| Browse Netflix (or other Watchmode providers) by country | Yes | No (shows streaming availability info on titles, not a synced provider catalog) |
| New / leaving / recently removed catalog views | Yes | No |
| Watchmode credit budget and catalog jobs | Yes | No |
| Search TMDB-wide and request anything | No (pool is imported catalogs) | Yes |
| Add movie/series to Radarr/Sonarr | Yes | Yes (usually after request approval) |
| Select seasons when adding a series | Yes | Yes |
| Multiple Arr instances / 4K routing | No (v1) | Yes |
| Request approval queue | No | Yes |
| Request limits per user | No | Yes |
| Plex / Jellyfin / Emby auth and library sync | No | Yes |
| Multi-user permissions | Admin vs non-admin profile in UI only | Granular permissions |
| Household profiles without login | Yes (up to 10) | Users are authenticated accounts |
| Favorites / “my list” | Yes (per profile) | Watchlist |
| Explicit like/dislike taste + local ML recommendations | Yes (`/for-you`, optional ONNX) | Inline recommendations / suggestions (different product model) |
| Notifications (Discord, email, Telegram, …) | No (v1) | Yes |
| Mobile-oriented UI | Desktop-first (responsive not a v1 goal) | Mobile-friendly |
| OpenAPI for the app API | Yes (`/docs`) | Project-specific |

## When to choose which

| Situation | Prefer |
| --- | --- |
| You subscribe to streaming services and want a Netflix-like grid of **what is available now**, with leaving/new filters, then push picks to Arr | **Pickarr** |
| Friends/family already use Plex, Jellyfin, or Emby and should **request** titles without Arr access | **Seerr** |
| You need approval workflows, request quotas, and notification agents | **Seerr** |
| You care about local taste learning over titles in your **imported streaming catalogs** | **Pickarr** |
| You need multiple Radarr/Sonarr targets (e.g. HD vs 4K) | **Seerr** (Pickarr v1 is one of each) |
| You want both “what’s leaving Netflix” and “please add this to the shared library” | **Both** |

## Scope boundaries (do not confuse)

- Pickarr does **not** replace Seerr’s request portal, media-server SSO, or notification hub.
- Seerr does **not** replace Pickarr’s Watchmode-backed catalog sync, credit-aware jobs, or provider/country browsing model.
- Neither app downloads or plays media; both orchestrate Sonarr/Radarr (and Seerr additionally mirrors media-server library state).

## References

- Seerr docs: https://docs.seerr.dev/
- Pickarr user guide: [user/README.md](user/README.md)
- Pickarr features: [user/features.md](user/features.md)
