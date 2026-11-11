# Features

## Catalog

Pickarr syncs **catalog sources**: each source is a streaming **provider + country** pair (for example Netflix IT). Seeded sources include Netflix IT and Netflix US. You can enable or disable sources from Settings within Watchmode plan limits.

On `/catalog`:

- Choose one provider, then one country.
- Quick filters: **All**, **New**, **Leaving**, **Recently removed**.
- Filters: type, genre, year, library state, sort, include removed.
- Infinite poster grid with library badges, favorite heart, and rating controls.
- The preferred source is remembered per profile (`pickarr.sourceId.{profileId}`).

Type and genre options come from titles in the current scope. The grid stays empty until Sonarr and/or Radarr are configured (series require Sonarr; movies require Radarr).

### Sync behavior (operator view)

- Scheduled incremental and full sync jobs keep the local catalog current.
- On the Watchmode **free** plan, change feeds and a full leaving calendar are not available: incremental sync uses recent list pages; removals are confirmed across full syncs; leaving dates are a short preview. Older catalog titles (for example a series whose last “new” release is a new season) usually appear only after a **full sync** completes and sets the source baseline.
- Catalog search is scoped to the selected provider + country. A title present on Netflix IT does not appear under Netflix US until that source has an active availability row.
- Free plan limits: about 2 500 credits/month and at most **3** distinct countries (several providers in one country count once).
- Cached catalog data should be refreshed within Watchmode’s free-plan cache window; the UI warns when a source looks stale.
- Titles imported with an IMDb id but no TMDB id stay in the enrichment queue (`Pending`) so TMDB `/find` can fill posters and metadata; duplicate Watchmode rows that share an IMDb id are merged into one title.

## Title detail

Opening a title keeps you on the route that opened it. The detail dialog shows:

- Metadata (overview, genres, scores, external links)
- Availability on the current source (added / leaving / removed)
- Shared Arr library status (monitor, download progress, seasons for series)
- Actions: favorite, rate, Add / Delete, Search now, Refresh status
- **Similar** titles when recommendations are enabled (series seasons newest first; episodes oldest first; adding a series sends only selected seasons)

## Profiles (household)

Up to **10** profiles share one install. There is no authentication: profiles are a logical partition of taste and lists.

| Rule | Behavior |
| --- | --- |
| Admin | First profile is admin (`IsAdmin`). Role is not transferred; admin profile is not deleted. |
| Who | `/who` selects the active profile. The current profile is marked in the picker and shown beside the header avatar. Last id is stored in `localStorage` as `pickarr.profileId`. |
| Single profile | One profile skips the picker and enters the catalog. |
| Header | Profile control always opens `/who`. After choice the SPA sends `X-Pickarr-Profile`. |
| Settings | Non-admin profiles do not see Settings; `/settings/*` redirects to the catalog. Settings APIs stay open. |
| User management | Admins manage household profiles in Settings → Users. `/who` remains the profile picker; each profile can edit its own name, color, and recommendation preferences there. |
| Personal data | Favorites, ratings, watched/hide, recommendations, and reco toggles are per profile. |
| Shared data | Catalog metadata, embeddings, app settings, and Arr library rows stay global. |

Anyone can edit their own name, color, and reco toggles (`useImplicit`, `excludeInLibrary`). Only the admin creates or deletes profiles and can edit every profile from Settings → Users.

## My list

`/list` is the active profile’s favorites across providers and countries. It is **not** a catalog filter (`view=favorites` / `favorite=true` on catalog are ignored; `/catalog?view=favorites` redirects to `/list`).

## Ratings and For you

Explicit votes on a title (per profile): **Love**, **Like**, **Dislike**, **Not interested**. **Hide** (watched) is separate and excludes from For you by default without adding taste weight.

**For you** (`/for-you`) shows local recommendations over titles already in enabled catalogs:

- Default filter: **All** providers and countries (union of enabled sources). Provider and country each offer an All option so the cross-catalog view is explicit.
- Choosing a concrete provider + country narrows the list to that source immediately (no search box on this page).
- Type and genre filters work with All selected (options from the enabled-catalog union) or with a concrete source (options from that source).
- Cold start: while effective ratings are below the personalization threshold (`N0`, default 30), recommendations can blend a **household prior** from other profiles’ Love/Like (capped); at full personalization that term is zero
- Optional ONNX embedding model under the data path improves similarity when installed
- Taste data never leaves the container

`/ratings` lists votes with export/import. Provider/country default to **All** (ratings stay global); a selected source only drives presence badges. `/taste` helps teach taste while learning is cold.

## Sonarr / Radarr

- Add series to Sonarr and movies to Radarr from the title dialog.
- See shared library status on cards and in detail.
- Delete with confirmation; destructive delete options default off.
- Periodic Arr sync (and optional webhooks when configured) refreshes status.
- An Arr add with `X-Pickarr-Profile` stores the profile on the audit row for that profile’s implicit signal; without the header the add is still valid but not attributed for taste.

## Administration

Admin-only Settings tabs are ordered System, Users, Catalogs, Recommendations, Connections, and Jobs. They cover system stats, profile management, catalog sources, recommendation settings (model status, rebuild/eval/reset), Sonarr/Radarr and Watchmode/TMDB connections, and job control. Job views show runs (with kind-specific stats), Watchmode credit usage, and unmapped titles; the Jobs page also configures how many finished runs to keep per job. System shows the running image version and, when GHCR has numerically newer version tags, lists them (release builds only see newer releases; development builds see any newer versioned tag). System also lists log files for download and can clear them.

## Attribution

UI includes attribution for Watchmode and TMDB as required by their personal / non-commercial API terms.
