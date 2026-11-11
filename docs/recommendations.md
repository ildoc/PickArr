# Pickarr — Ratings and recommendations

| | |
|---|---|
| **Version** | 0.3 |
| **Date** | 2026-11-10 |
| **Language** | English |
| **Linked documents** | `functional-spec.md`, `technical-spec.md` |
| **Conventions** | IDs in this document use the `REC` suffix (FR-REC, BR-REC, NFR-REC, US-REC, UC-REC) or `SP-R` for spikes, so they do not collide with IDs in the linked documents. References to FR-, BR-, D-, SP- without `REC` point to those linked documents. |

### Revisions

| Version | Choice |
|---|---|
| 0.3 | Rating, favorites, watched, and reco build are per **household profile** (profile+title key). Under `N0` the non-personal prior includes the **household prior** (others’ Love/Like, cap 0.5). `useImplicit` and `excludeInLibrary` are read from the profile. Arr library is shared; Add audit stores `ProfileId` when the header is present. |
| 0.2 | Rating is one per `Title`, shared across providers. Shown recommendations belong to already-imported catalogs: by default the selected source (service + country), plus the union of enabled sources. A like given on another service enters the taste profile used for the current source too. Similar titles list titles present in at least one enabled source, with services. The page is `/for-you` and reuses the catalog source. Removal from a service does not exclude a title. No *Hide* state. No harvesting of titles absent from catalogs. |
| 0.1 | Draft with TMDB pool, “all TMDB” scope, and *Hide* state. Replaced by 0.2. |

---

## Table of contents

**Part A — Functional**
1. Introduction and goals
2. Design principles
3. Functional requirements
4. Non-functional requirements
5. Business rules
6. User stories
7. Use cases
8. Screens

**Part B — Technical**
9. Overview and code anchors
10. Data model
11. Data pipeline and jobs
12. Layered recommendation algorithm
13. Local model (embedding)
14. API contracts
15. Frontend
16. Configuration
17. Tests and evaluation
18. Build, container, and CI
19. Risks, spikes, and open decisions
20. Release phases
21. Impact on existing documents

---

# Part A — Functional

## 1. Introduction and goals

### 1.1 Purpose

Add to Pickarr a **rating** (like / dislike) on the title and **recommendations** that use that rating. The rating belongs to the active **household profile** and is unique for that profile even when the same title is on multiple services. The recommendations the profile sees are titles already present in the imported catalogs.

### 1.2 Goals

- Allow rating a title with one gesture (one click), from the card and the detail.
- Keep **one rating per profile and title**. Netflix and Prime Video that share the same title share that rating **for the profile**. The same applies across countries (Netflix IT and Netflix US). Two profiles may diverge.
- Show in **For you**, by default, only titles from the **selected catalog source** (service + country: for example Netflix Italy).
- Offer, on the same page, the **All enabled sources** view: each title once, if it appears in at least one enabled catalog, with the list of services.
- Let ratings given on titles of another service enter the taste profile too. What changes is the filter of what is shown, not the profile.
- At cold start (`N_eff < N0`), help a new profile with a **household prior** from other profiles’ *Like* / *Love* (cap 0.5); at steady state only that profile counts.
- Work from day one, with no ratings (quality and popularity of titles already in catalog, plus “similar titles”), and improve with use.
- Run the computation **locally**, in the container. The embedding model, if installed, is local.
- Explain every recommendation.
- Remain a single Docker image app, multi-profile without authentication, self-hosted.

### 1.3 Out of scope

- Recommendations of titles that have no `Availability` row on an enabled source. Candidates are not imported from TMDB `recommendations`, `similar`, or `discover` only to recommend them. TMDB `/recommendations` arcs are used only as an **amplifier** of local signals on titles already in catalog.
- “All TMDB” scope, *Out of catalog* label, TMDB/JustWatch watch providers.
- ~~Generic *Hide* state~~ — superseded: **Hide** (`Watched`) excludes without taste weight (default 0); **Not interested** is an explicit negative rating and excludes.
- *For you* sorting mixed into the catalog grid. Recommendations for **one** source live in a **dedicated row** on `/catalog` (at most 25 titles). The `/for-you` page covers the union of enabled sources.
- User–user collaborative filtering (beyond the cold-start household prior). External history sync (Trakt, Plex, Jellyfin).
- Fine-tuning of language models. The embedding model, if used, is inference-only.
- Push notifications.

### 1.4 Glossary

| Term | Meaning |
|---|---|
| **Title** | `Titles` row. One per identity (BR-01): Watchmode id, and when known the (TMDB type, TMDB id) pair and/or IMDb id. Providers do not duplicate the title: they add an `Availability`. |
| **Source** | `CatalogSource`: a provider and a country (Netflix IT, Netflix US, Prime Video IT are different sources). |
| **Rating** | *Love*, *Like*, *Dislike*, or *Not interested* on the profile+`Title.Id` pair. At most one per profile. Applies to every source where the title appears. |
| **Hide** | `Watched` flag on the profile+`Title.Id` pair (hide / already watched). Coexists with the rating. Excludes from *For you* and similar; with default weight 0 it does not add taste mass. |
| **Implicit signal** | Interest without a rating: *My list* (temporary / watch-later interest), Hide if weight is &gt; 0, Sonarr/Radarr add attributed to the profile, completed download that follows that Add. Does not replace an explicit rating. |
| **Profile (household)** | UI/data partition (`Profiles`): name, color, reco toggles. |
| **Profile (taste)** | Vector/tastes of the household profile, built from ratings and signals from any source **of that profile**. |
| **Pool** | Titles among which the score is computed: `Title` rows that have at least one `Availability` on an enabled source. |
| **Scope** | Filter applied to already-computed scores. `source`: the selected source. `enabled`: all enabled sources. |
| **Prior** | Non-personal score from average vote and TMDB popularity already stored on the title. Under `N0` it may be blended with the household prior. |
| **Household prior** | Per-title mean of other profiles’ Love/Like weights, scored with sparse level 1. Enters only the non-personal slot, with cap 0.5. |
| **Embedding** | Vector of 384 numbers computed locally from the title text (global). |
| **Cold start** | Phase with few signals (`N_eff < N0`), in which TMDB prior and household prior still weigh. |

---

## 2. Design principles

1. **The rating sits on the profile and the title.** PK `(ProfileId, TitleId)`. There is no rating per title+source pair. A like for the profile given while viewing Prime Video is the same record read when viewing Netflix Italy.
2. **One taste profile per household profile; the filter is catalog.** Scores are computed on the pool **for that profile**. *For you* with scope `source` returns titles that have an `Availability` on the selected source. Scope `enabled` returns titles that have one on at least one enabled source.
3. **Removal from a service does not take the title out of the pool.** `Availability.RemovedAt` is not a recommendation filter. The title remains recommendable. The card may show the removal date already present in the model.
4. **Simple first, then personal.** Every algorithm level works on its own. If the local model is missing, lower levels are used.
5. **Explainable.** Every recommended item reports reasons.
6. **Local.** No taste data leaves the install. The user can export, correct, or clear ratings and derived data.
7. **No extra Watchmode credits.** Recommendations read `Titles`, `Availabilities`, and already-enriched metadata. They do not call Watchmode.
8. **Same catalog boundaries.** Reuse selected source, card DTOs, favorites, Sonarr/Radarr states, and Hangfire jobs. New parts hook in there.

---

## 3. Functional requirements

MoSCoW priority: **M** for the feature MVP, **S** should, **C** could.

### 3.1 Rating (FR-REC-RT)

| ID | Requirement | Pri |
|---|---|---|
| FR-REC-RT-01 | The user assigns a title **Love**, **Like**, **Dislike**, or **Not interested**. They can replace it or remove it (no rating). | M |
| FR-REC-RT-02 | The rating is available on the card (catalog, For you, My ratings) and on the detail dialog. | M |
| FR-REC-RT-03 | The rating is **global on the title** and persists on the backend. It is independent of provider and country. | M |
| FR-REC-RT-04 | A title already rated does not appear in *For you* or in similar. It remains in *My ratings*, in the catalog, and in favorites. *Dislike* remains a negative signal for the profile. | M |
| FR-REC-RT-05 | **My ratings** page: global list of rated titles, *Like* / *Dislike* tabs, search, edit and remove rating. Same cross-cutting presentation as *My list* (`places`). In addition, status relative to the selected source: present in that source (with `removedAt` if any) or absent from that source. | S |
| FR-REC-RT-06 | JSON export and import. Import updates only titles **already in the database**, recognized by (`tmdbType`, `tmdbId`). It does not create titles. | S |
| FR-REC-RT-07 | The UI updates the rating immediately (optimistic update) and rolls it back if the request fails. | M |
| FR-REC-RT-08 | Shortcuts in the dialog: `O` love, `L` like, `D` dislike, `N` not interested, `W` hide. | C |
| FR-REC-RT-09 | **Teach me your tastes** onboarding: a varied selection of titles **already in enabled catalogs**, to rate quickly (like / dislike / skip). The link from *For you* hides when `N_eff ≥ N0`. | S |
| FR-REC-RT-10 | The user marks a title as **Hide** (or clears the flag) from the dialog. The flag is global on the title and independent of the rating. | M |
| FR-REC-RT-11 | A *Hide* title does not appear in *For you*, in similar, or in `/taste` onboarding. It remains in the catalog and may have a rating. | M |

### 3.2 Implicit signals (FR-REC-SG)

| ID | Requirement | Pri |
|---|---|---|
| FR-REC-SG-01 | Positive signals **of the profile**: row in `Favorites`; row in `Watched`; `ArrAuditLog` with action `Add`, outcome `Success`, and the profile’s `ProfileId`; `ArrLibraryItem.State = Downloaded` only if it follows an Add by that profile. Weights are in BR-REC-02. The Arr library remains global. | M |
| FR-REC-SG-02 | If the title has an explicit rating, the profile uses only that rating. | M |
| FR-REC-SG-03 | The user can disable implicit signals in settings. | S |
| FR-REC-SG-04 | Deletion from Sonarr/Radarr (`ArrAuditLog` action `Delete`) is not a negative signal. The “added” signal remains. | M |

### 3.3 Recommendations (FR-REC-RC)

| ID | Requirement | Pri |
|---|---|---|
| FR-REC-RC-01 | **For you** page (`/for-you`): titles ordered by score over the union of enabled sources, infinite scroll. | M |
| FR-REC-RC-02 | On the selected source’s catalog (`/catalog`), a **dedicated row** shows up to **25** recommendations with scope `source`. The `/for-you` page defaults to scope `enabled` (All providers/countries); choosing a concrete provider + country switches to scope `source` for that catalog. | M |
| FR-REC-RC-03 | In the *All enabled sources* scope a title appears once, with `places` (provider name and country) for every enabled source where it has an `Availability`. | M |
| FR-REC-RC-04 | A title without `Availability` on enabled sources is never returned. | M |
| FR-REC-RC-05 | With no ratings or signals, *For you* shows the prior (level 0) and declares it, with the learning indicator. | M |
| FR-REC-RC-06 | Every item reports reasons (similar titles with a like, genres or keywords, or “highly rated / popular”). | M |
| FR-REC-RC-07 | Filters: type (movie/series/stand-up), genre, year, library status. By default titles already in library (*Downloaded*, *Downloading*, *Partial*, *Missing*) are excluded. The user can include them. | M |
| FR-REC-RC-08 | Titles that already have a rating or are marked *Watched* are excluded. | M |
| FR-REC-RC-09 | **Similar titles**, at the bottom of the dialog on multiple rows: neighbors of the open title that have an `Availability` on at least one enabled source, with `places`. Does not require ratings. Not limited to the source from which the dialog was opened. | M |
| FR-REC-RC-10 | Diversity: MMR re-ranking on the top results, so the page is not filled with a single genre or a single franchise. | S |
| FR-REC-RC-11 | Exploration: a quota of positions (default 10%) for titles far from habits. Disableable. | S |
| FR-REC-RC-12 | **Refresh recommendations** action in settings. | M |

### 3.4 Pool (FR-REC-CD)

| ID | Requirement | Pri |
|---|---|---|
| FR-REC-CD-01 | The pool is the set of `Title` with at least one `Availability` on an enabled source (`CatalogSource.IsEnabled` and `Provider.IsEnabled`). It is the same criterion `TitleQueryService` uses to list sources. | M |
| FR-REC-CD-02 | A populated `RemovedAt` does not take the title out of the pool or out of that source’s scope. | M |
| FR-REC-CD-03 | Already-imported types (movie, series, stand-up, FR-CAT-03) are the perimeter. The catalog is not widened for recommendations. | M |
| FR-REC-CD-04 | Keywords and credits used by level 1 are persisted from the TMDB payload enrichment already downloads (`append_to_response=keywords,credits`). It is not a second title-discovery pipeline. | M |

### 3.5 Local model (FR-REC-ML)

| ID | Requirement | Pri |
|---|---|---|
| FR-REC-ML-01 | Embeddings are computed in the container, with an ONNX model on CPU. No external call with taste data. | M |
| FR-REC-ML-02 | The model is not in the base image: it is downloaded on first use, with integrity check, or copied into `/config/models`. | M |
| FR-REC-ML-03 | Without the model the feature works with levels 0 and 1. The UI shows model status. | M |
| FR-REC-ML-04 | The model is replaceable via configuration. On change, embeddings are recomputed in the background. | S |
| FR-REC-ML-05 | ONNX threads and batch size are limitable. | M |
| FR-REC-ML-06 | Optional supervised level, past a rating threshold and only if it beats the previous level in offline evaluation. | C |

### 3.6 Administration (FR-REC-AD)

| ID | Requirement | Pri |
|---|---|---|
| FR-REC-AD-01 | *Recommendations* section in settings: model status, embedding coverage, pool size, rating count, stage, last rebuild, last error. | M |
| FR-REC-AD-02 | Actions: download model, recompute embeddings, rebuild, run offline evaluation. | M |
| FR-REC-AD-03 | Offline evaluation (leave-one-out) comparing prior, level 1, level 2 (and 3 if active). | S |
| FR-REC-AD-04 | Clear derived data (profile, embeddings, recommendations) while keeping ratings. Separate action, with confirmation, to also delete ratings. | M |
| FR-REC-AD-05 | Local event log (shown, opened, rated, added), disableable. | C |
| FR-REC-AD-06 | UI copy: computation is local. | S |

### 3.7 Settings (FR-REC-ST)

| ID | Requirement | Pri |
|---|---|---|
| FR-REC-ST-01 | Feature switch (default on). When off, rating and recommendation pages are hidden. Already-saved ratings remain. | M |
| FR-REC-ST-02 | Switches: implicit signals, local model, exploration. | M |
| FR-REC-ST-03 | The name language in *For you* is `CatalogSource.MetadataLanguage` of the selected source, with English fallback, as in the catalog. | M |
| FR-REC-ST-04 | Weights, thresholds, MMR, and exploration quota in an *Advanced* section. | S |

---

## 4. Non-functional requirements

| ID | Area | Requirement |
|---|---|---|
| NFR-REC-01 | Read | `GET /recommendations` and `GET /titles/{id}/similar` respond in under 300 ms (p95) on the install’s real pool. Scores are precomputed; the request filters by scope and page. |
| NFR-REC-02 | Rebuild | Rebuild on the pool (union of enabled catalogs) and up to 500 signals in under 10 seconds on common CPU. To be measured (SP-R3). |
| NFR-REC-03 | Embedding | Background computation, with a thread cap, without blocking catalog sync and *arr*. |
| NFR-REC-04 | Memory | Quantized model plus pool vectors stay on the order of a few hundred MB. To be measured. |
| NFR-REC-05 | Robustness | Missing or corrupt model, TMDB error, or incomplete metadata: the app stays up; recommendations fall to the available level. |
| NFR-REC-06 | Reproducibility | With the same ratings, signals, and catalog, rebuild produces the same order. Exploration uses a seed tied to the version. |
| NFR-REC-07 | Privacy | Tastes stay in `Pickarr.db`. Toward TMDB only ids of titles already in catalog leave, to complete metadata. No telemetry. |
| NFR-REC-08 | Persistence | Ratings live in the main DB (`/config` backup) and are exportable. |
| NFR-REC-09 | Portability | The model runs on linux/amd64 and linux/arm64 without GPU. |
| NFR-REC-10 | Licenses | Model and data with compatible licenses (SP-R5). |

---

## 5. Business rules

| ID | Rule |
|---|---|
| BR-REC-01 | **One rating per profile and title.** Values `Love`, `Like`, `Dislike`, or `NotInterested`. A new choice replaces the previous one. Removal deletes the row. Every change is a row in `RatingEvents` with the same `ProfileId`. |
| BR-REC-02 | **Weights.** *Love* **+1.5**. *Like* **+1.0**. *Dislike* **−1.0**. *Not interested* **−0.75**. *My list* **+0.5** (temporary interest / watch-later signal: leaves the profile when the favorite is removed or a rating is assigned). *Hide* **0** (exclusion only). *Added to Sonarr/Radarr* **+0.6**. *Downloaded* **+0.4**. Implicit signals are summed and capped to **[0, +1]**. With an explicit rating only that counts. Weights are configurable. |
| BR-REC-03 | **Decay.** Optional, half-life in days. Default: none. |
| BR-REC-04 | **Effective count.** `N_eff = Σ \|s_i\|` over titles with a signal. Governs the profile weight versus the prior (§12.2). |
| BR-REC-05 | **Stages.** `cold` if `N_eff < 5`; `warming` if `5 ≤ N_eff < N0` (default 30); `personalized` if `N_eff ≥ N0`. Informational in UI. Weight grows continuously. |
| BR-REC-06 | **List exclusions.** Titles that have a rating or are *Hide*. The *explore* injection cannot reintroduce them. By default also titles with `ArrLibraryItem` in *Downloaded*, *Downloading*, *Partial*, or *Missing* state (filter disableable). Those titles’ score can still be computed: exclusion is at read time. |
| BR-REC-07 | **Membership.** Scope `source`: an `Availability` exists with `CatalogSourceId` equal to the requested source. Scope `enabled`: an `Availability` exists whose source has `IsEnabled` and whose `Provider.IsEnabled` is true. In both cases `RemovedAt` is irrelevant. |
| BR-REC-08 | **Identity.** The candidate is the `Title` already unified by `CatalogUpserter` (Watchmode id, then TMDB pair). Two `Titles` rows remain two ratings: that happens only if the catalog has not linked them. This feature does not add another merge criterion. |
| BR-REC-09 | **Embedding text.** Prefer the `en-US` localization in `TitleLocalizations`. If missing, use the localization already saved for the source language. |
| BR-REC-10 | **Watchmode.** No recommendation function calls Watchmode. |
| BR-REC-11 | **Sonarr/Radarr.** A recommended title is added with the existing flow (`ArrAddService`), if it has the required id (BR-08). |
| BR-REC-12 | **Rebuild.** After a rating or a *Watched* change, rebuild with a 30-second debounce, without waiting for the nightly job. |
| BR-REC-13 | **Selected source.** It is the same id remembered by the catalog for the profile (`localStorage` `pickarr.sourceId.{profileId}`, query `source`). *For you* reads and writes it. Changing it here changes the catalog on the next visit, and the reverse. |
| BR-REC-15 | **Household prior.** With `N_eff < N0`, the score’s non-personal slot may include the mean Love/Like of other profiles (sparse level 1 only). Cap `h = 0.5 · (1 − raw)` with `raw = min(1, N_eff/N0)`. At `N_eff ≥ N0`, `h = 0`. Others’ Dislike, favorites, and watched do not enter. A single profile in the install reproduces the TMDB prior. A rating rebuilds the voting profile and others still under `N0`. |
| BR-REC-16 | **Profile preferences.** The build reads `UseImplicit` and `ExcludeInLibrary` from the profile, not from `reco.useImplicit` / `reco.excludeInLibrary`. The global keys may remain stored. |
| BR-REC-14 | **Similar and scope.** Similar always applies `enabled` membership, even if *For you* is on scope `source`. |

---

## 6. User stories

**US-REC-01 — Rate quickly.** I want a like or a dislike from the card.
- CA1: the rating shows immediately on the card and the dialog, in any source where I open the same title.
- CA2: I can change it or remove it.

**US-REC-02 — One rating only.** If I mark a title on Prime Video, on Netflix I find it already rated.
- CA1: there is a single rating row for that `Title`.
- CA2: removing it from one source removes it from the other too.

**US-REC-03 — Recommendations in the catalog I’m browsing.** While I browse Netflix Italy, the *For you* row on the catalog contains titles with availability on Netflix Italy (at most 25).
- CA1: a title only on Prime Video does not appear in that row.
- CA2: a title recently removed from Netflix Italy may still appear.
- CA3: the union of enabled sources stays on `/for-you`.

**US-REC-04 — Day-one recommendations.** With no ratings I see appreciated and popular catalog titles, with a notice that it is not personal yet.
- CA1: the indicator is in `cold` stage.
- CA2: the dialog has *Similar titles* even without ratings.

**US-REC-05 — Tastes travel.** A like given on Prime Video raises, inside Netflix Italy, Netflix Italy titles near that one.
- CA1: the profile is not split by provider.
- CA2: the Netflix Italy list continues to contain only titles with availability on that source.

**US-REC-06 — All my sources.** I want a single view of recommendations over enabled catalogs.
- CA1: each title appears once, with services and countries.
- CA2: a disabled source does not contribute.

**US-REC-07 — Similar even on another service.** In the dialog I see nearby titles present in at least one enabled catalog, with where they are.
- CA1: a similar only on Prime Video is visible even if I opened the dialog from the Netflix Italy catalog.
- CA2: a title absent from every enabled catalog does not appear.

**US-REC-08 — Understand why.** Every recommendation shows up to 3 reasons.

**US-REC-09 — Tastes at home.** The model runs in the container. I can export and delete ratings.

**US-REC-10 — Variety.** Top results mix genres and franchises.

**US-REC-11 — Speed up.** Onboarding proposes titles already in enabled catalogs.

**US-REC-12 — My ratings.** I see all ratings, including titles that are not in the selected source, with a badge that says so.

---

## 7. Use cases

### UC-REC-01 — Rate a title

- **Actor:** User.
- **Flow:** click *Like* (card or dialog) → UI updates the icon → `PUT /titles/{id}/rating` → backend writes `Ratings` and `RatingEvents` → queues rebuild (debounce) → learning indicator updates on status refresh.
- **Alternatives:** click the already-active rating → `DELETE`, the rating disappears. Network error → UI returns to the previous state.

### UC-REC-02 — Browse *For you*

- **Actor:** User.
- **Flow:** opens `/for-you` → provider/country default to All → `GET /recommendations?scope=enabled` → cards with reasons, rating, favorite, *arr* status, and `places`.
- **Alternative:** user picks a provider and country → `scope=source&sourceId=` and the list narrows immediately. Empty pool or rebuild never run → message with link to status in settings.

### UC-REC-03 — Similar titles

- **Actor:** User.
- **Flow:** in the dialog, *Similar titles* calls `GET /titles/{id}/similar`. Results are titles from the `enabled` pool, ordered by proximity, with `places`. Click opens the dialog.

### UC-REC-04 — Onboarding

- **Actor:** User.
- **Flow:** `/taste` shows pool titles, chosen to cover genres and eras → user can ask for another batch (excludes titles already shown and keeps ratings already given) → batch ratings → a single rebuild at submit end.

### UC-REC-05 — Install the model

- **Actor:** User.
- **Flow:** *Settings → Recommendations → Download model* → files in `/config/models/{modelId}/`, SHA-256 check, self-test → *Ready* status → background embeddings → when coverage is reached level 2 enters.
- **Alternatives:** manual copy and *Detect*. Hash or self-test failed → *Error* status; levels 0 and 1 remain.

### UC-REC-06 — Offline evaluation

- **Actor:** User.
- **Flow:** action in settings → report with metrics per level and count. Under about 30 ratings the report warns that the sample is small.

### UC-REC-07 — Reset

- **Actor:** User.
- **Flow:** *Clear derived data* deletes profile, embeddings, and recommendations, then rebuilds. *Also clear ratings* is a distinct action and asks for confirmation.

---

## 8. Screens

**Card.** Next to the favorites heart, two actions (like, dislike). The active rating stays visible. On *For you* in scope `enabled`, and on similar, the card shows `places` like *My list*.

**Dialog (`TitleDialog`).** Labeled actions *Like* / *Dislike* / *Not interested*, *Hide*, and favorite. Shortcuts `O` / `L` / `D` / `N` / `W`. *Why we recommend this* block if arriving from *For you*. At the bottom, *Similar titles* grid on multiple rows.

**For you (`/for-you`).** Same provider + country selector as the catalog (`FilterBar`), with an **All** option on both (default). All → scope `enabled`; a concrete pair → scope `source` and refetch immediately (no text search on this page). `N_eff / N0` indicator (with *Teach me your tastes* link) only if `N_eff < N0`. Type, genre, include-in-library filters. Infinite-scroll grid. Reason chips.

**Onboarding (`/taste`).** Grid of pool titles, large actions, counter, *Show more titles*, *Done*.

**My ratings (`/ratings`).** Tabs, search, rating actions, *Export* / *Import*. Global list. Provider/country default to **All**; a selected source only drives presence badges, with `places` on enabled sources.

**Settings → Recommendations (`/settings/recommendations`).** Switches, model status, coverage, pool, ratings, stage, §3.6 actions, reset. Jobs also appear in *Settings → Jobs*, like other `SyncRuns`.

**For you** entry in the header, next to *My list*. *My ratings* is reachable from *For you* and from settings.

---

# Part B — Technical

## 9. Overview and code anchors

### 9.1 What already exists and is reused

| Piece | Where | Use |
|---|---|---|
| Unique title | `Title`, merge in `CatalogUpserter.ResolveTitleAsync` (first `WatchmodeId`, then `TmdbType`+`TmdbId`, then `ImdbId`) | Rating points to `Title.Id`. |
| Presence in a catalog | `Availability` (`TitleId`, `CatalogSourceId`, `RemovedAt`, `AddedAt`, `LeavingDate`) | Scope filter. `RemovedAt` does not exclude. |
| Source | `CatalogSource` + `Provider` | Source = provider + `CountryCode`. On if `CatalogSource.IsEnabled` and `Provider.IsEnabled`. |
| Name and plot | `TitleLocalization` (`TitleId`, `Language`) | Source language, fallback `en-US` (`LanguageFallback`). Add the `en-US` row if missing, for embeddings. |
| Genres | `Genre.Id` = TMDB id, `TitleGenre`, `GenreLocalization` | Level 1 terms, already persisted. |
| Keywords and cast in payload | `TmdbMetadataProvider` (`append_to_response=external_ids,keywords,credits`), `TmdbTitleDetails.Keywords`, `CastJson` (names only) | Keywords today serve stand-up detection and are not saved. Cast has no person id. Extend the mapping; do not open another client. |
| Average vote and popularity | `Title.VoteTmdb`, `VoteTmdbCount`, `Popularity` | Prior. |
| Favorites | `Favorite` (`TitleId` PK) | Implicit signal and UI model of optimistic toggle (`FavoriteButton`). |
| Library | `ArrLibraryItem.State`, `ArrAuditLog` (`Add` / `Delete`, `Success` / `Failed`) | Signals and “in library” filter. |
| UI source | `pickarr.sourceId.{profileId}` (legacy admin: `pickarr.sourceId`), query `source` on `/catalog` | Same source for `/for-you`. |
| UI profile | `pickarr.profileId` → header `X-Pickarr-Profile` | Partitions lists, flags, and build. |
| Card | `TitleListItem` / `TitleListItemDto`, `places`, `TitleCard`, `TitleDialog` | Add the `rating` field. |
| Jobs | Hangfire, queues `default`, `sync`, `arr` (`HangfireBootstrap`), `SyncRun` + `SyncRunKinds` | New `ml` queue and new `Kind`s. |
| Settings | `AppSetting`, `SettingsKeys` | Keys `reco.*`. |
| API | Groups `/api/v1/...`, JSON camelCase, page `page` / `pageSize` | Same conventions as `technical-spec.md` §8. |

### 9.2 Flow

```
Rating / Favorites / ArrAuditLog / ArrLibraryItem
        │
        ▼
Titles already in catalog + Availabilities
        │  existing enrichment, plus persisted keywords/credits/en-US
        ▼
TitleEmbeddings (if the model is present)
        │
        ▼
RecoBuildJob: taste profile for ProfileId + score on pool (+ household prior under N0)
        │
        ▼
Recommendations (versioned)
        │  on read: scope source | enabled, filters, page
        ▼
GET /recommendations , GET /titles/{id}/similar  →  React
```

### 9.3 Decisions

| # | Decision | Why |
|---|---|---|
| 1 | The pool is the union of titles that already have an `Availability` on an enabled source. No `Origin` column, no `Harvested` title. | Recommendations must belong to imported catalogs. Cross-provider merge already exists. |
| 2 | The score is global and precomputed. Scope is applied on read with a join on `Availabilities`. | A like on Prime Video enters the profile; Netflix Italy filters afterward. |
| 3 | `RemovedAt` is not a predicate of recommendation queries. | Removal from the service is irrelevant for the recommendation. The catalog grid, in the default filter, continues to exclude removed titles (`TitleQueryService`, `includeRemoved`). Those are two different queries. |
| 4 | Levels 0 prior, 1 sparse profile, 2 kNN on embeddings, 3 optional classifier. Personal weight grows with `N_eff`. | Usable recommendations immediately, finer when there are ratings and, if needed, the model. |
| 5 | kNN on ratings, not a single centroid. | Different tastes (thriller and comedy) must not be averaged into one point. |
| 6 | ONNX Runtime in the .NET process, model downloaded on demand. Brute-force neighbors with `System.Numerics.Tensors`. | One image, one process. The size is that of the catalogs, not an ANN index. |
| 7 | Similar always uses `enabled` membership and fills `places`. | Product choice, distinct from *For you* scope. |
| 8 | Zero Watchmode calls. | BR-14, BR-REC-10. |

---

## 10. Data model

Conventions of `technical-spec.md` §4. Tables in the `Pickarr.db` DB. Additive EF migration.

### 10.1 New columns on existing tables

No mandatory column on `Titles` for membership: `Availability` is enough.

Optional, if level 1 computes and keeps them:

| Table | Column | Notes |
|---|---|---|
| `Titles` | `OriginCountries` text null | JSON of TMDB origin countries, if read from the already-requested payload. Level 1 feature. |

`CastJson` remains the list of names already saved. Person ids live in the new tables, so the current cast-in-dialog contract is not broken.

### 10.2 New tables

**`Ratings`** — one current rating per profile

| Column | Type | Notes |
|---|---|---|
| ProfileId | int PK, FK → Profiles | |
| TitleId | int PK, FK → Titles | |
| Value | text | `Love` / `Like` / `Dislike` / `NotInterested` |
| RatedAt | datetime | |

**`RatingEvents`**

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| ProfileId | int FK | |
| TitleId | int FK | |
| Value | text null | null = rating removed |
| At | datetime | |
| Origin | text | `Ui` / `Onboarding` / `Import` |

**`Keywords`**: `Id` (TMDB id) PK, `Name`.
**`TitleKeywords`**: PK `(TitleId, KeywordId)`.

**`People`**: `Id` (TMDB id) PK, `Name`.
**`TitleCredits`**: PK `(TitleId, PersonId, Role)`, `Role` ∈ `Cast` / `Director` / `Creator`, `Order` int. First 5 actors; directors for movies; `created_by` for series. Requires reading `credits.cast[].id` and `credits.crew` (and `created_by` on series) in the current TMDB DTO, which today deserializes only the cast name.

**`TitleEmbeddings`**

| Column | Type | Notes |
|---|---|---|
| TitleId | int FK | |
| ModelId | text | e.g. `all-MiniLM-L6-v2@q8` |
| Dim | int | 384 |
| Vector | blob | float32 little-endian, L2 = 1 |
| TextHash | text | hash of the text; if it changes, recompute |
| ComputedAt | datetime | |

PK `(TitleId, ModelId)`.

**`RecoState`** (one row per profile; unique `ProfileId`)

| Column | Type | Notes |
|---|---|---|
| ProfileId | int FK | |
| CurrentVersion | int | |
| BuiltAt | datetime null | |
| RatingCount | int | |
| EffectiveN | real | |
| Stage | text | `cold` / `warming` / `personalized` |
| ModelStatus | text | `Absent` / `Downloading` / `Ready` / `Error` — written only on the admin row |
| ModelId | text null | admin only |
| EmbeddingCoverage | real | 0–1 on the pool; admin only |
| ProfileJson | text null | truncated sparse profile of the profile |
| LastError | text null | |

**`Recommendations`**

| Column | Type | Notes |
|---|---|---|
| ProfileId | int FK | |
| Version | int | |
| TitleId | int FK | |
| Score | real | |
| Personal | real null | |
| Prior | real | non-personal slot after any household blend |
| Rank | int | order of that profile version, before scope |
| ReasonsJson | text | reasons (§12.7) |

PK `(ProfileId, Version, TitleId)`. Index `(ProfileId, Version, Score DESC)`. On publish, previous versions **of that profile** are deleted.

**`RecoEventLog`** (only if FR-REC-AD-05): `Id`, `ProfileId`, `At`, `TitleId`, `Event` (`Shown` / `Opened` / `Liked` / `Disliked` / `AddedToArr`), `Version`, `Rank`, `Scope`.

### 10.3 Signals, without duplicate tables

On rebuild:

- rating: `Ratings` of the `ProfileId`;
- favorite: `Favorites` of the `ProfileId`;
- added: `ArrAuditLog` with `Action = Add`, `Outcome = Success`, non-null `TitleId`, and the profile’s `ProfileId` (without `ProfileId` the signal is not attributed);
- downloaded: `ArrLibraryItem` with `State = Downloaded` and non-null `TitleId`, if the Add attributed to the profile exists.

A successful `Delete` does not invert the `Add`.

### 10.4 Migration

Additive EF. Already-imported titles enter the pool thanks to existing `Availability` rows. A backfill, after enrichment extension, re-reads keywords and credits from `Ready` titles (old payloads did not save them).

---

## 11. Data pipeline and jobs

### 11.1 Queue

In `HangfireBootstrap` the queues become `default`, `sync`, `arr`, `ml`.

Jobs for this feature sit on the `ml` queue, with `[DisableConcurrentExecution]` per job type, so a long embedding does not occupy `sync` and `arr` workers. Add a second `AddHangfireServer` with `WorkerCount = 1` and `Queues = ["ml"]`, and remove `ml` from the existing server. Jobs are registered in `SyncRuns` with `Kind`: `RecoFeature`, `RecoEmbedding`, `RecoBuild`, `RecoEval`.

| Job | Trigger | Purpose |
|---|---|---|
| `RecoFeatureJob` | after deploying keyword persistence, then after enrichment and every hour | For `Ready` titles without saved keywords/credits, or without `en-US` localization: complete the data. |
| `RecoEmbeddingJob` | after `RecoFeatureJob`, when the model is ready | Missing embeddings or ones with changed `TextHash`. |
| `RecoBuildJob` | rating (30 s debounce), after embedding, nightly | Profile and scores; publishes a version. |
| `RecoEvalJob` | manual | §17.3. |
| `RecoModelDownloadJob` | manual | §13.3. |

Idempotent, same idea as `SyncRuns` (status, error, trigger `Schedule` / `Manual`).

### 11.2 Features: extend enrichment, not a second catalog

`EnrichmentService` and `TmdbMetadataProvider` remain the only point that calls TMDB for a title.

Changes:

- Persist `TmdbTitleDetails.Keywords` in `Keywords` / `TitleKeywords` inside `EnrichCoreAsync` (today the list is used only by `TmdbStandUp`).
- Extend `TmdbCreditsDto` with person id and crew (`Director`) and, for series, `created_by`. Map them into `TmdbTitleDetails` and save them in `People` / `TitleCredits`. `CastJson` continues to be the name list for the dialog.
- If no `TitleLocalization` is `en-US`, an extra `GetMovie` / `GetTv` call with `language=en-US`, written into the same table (the key is already `(TitleId, Language)`). Applies to embeddings (BR-REC-09). The existing TMDB rate limiter also applies to this call.
- After details: `GET /movie|tv/{id}/recommendations` (page 1). Persist only TMDB ids already present as a local `Title` in `TitleTmdbRecommendations` (source→related arc + rank). `Title.TmdbRecommendationsFetchedAt` is written even if the intersection is empty, so the call is not repeated. No new titles.
- `RecoFeatureJob` selects `Ready` pool `Title` rows missing one of these pieces (keywords, credits, en-US, origin countries, or recommendations stamp) and reuses the service. It does not insert titles.

`OriginCountries`, if the column is added, is read from the same TMDB detail when the field is in the DTO.

### 11.3 `RecoEmbeddingJob`

```
text(t) = "{name} ({year}). {genres}. Keywords: {max 15}. {tagline}. {overview}"
           language: en-US if present, otherwise the localization already saved
           truncated to 256 tokens
for batches of titles without embedding or with different TextHash:
    tokenize → ONNX → mean pooling → L2
    save TitleEmbeddings
update RecoState.EmbeddingCoverage on the pool
```

### 11.4 `RecoBuildJob`

```
1. profile signals → s_i , N_eff   (BR-REC-02; UseImplicit/ExcludeInLibrary from profile)
2. sparse profile                      (§12.3)
3. embedding kNN                       (§12.4) if model ready and coverage ≥ threshold (model status = admin row)
4. TMDB prior over the whole pool      (§12.2)
5. household prior (if N_eff < N0)     (§12.2bis, BR-REC-15)
6. fusion → Score, Reasons             (§12.5, §12.7)
7. transaction: Recommendations(ProfileId, version+1), RecoState of the profile, delete old versions of that profile
```

- One rebuild at a time **per profile**. A rating queues the voting profile and others still under `N0`.
- Embedding coverage under `reco.embeddings.minCoverage` (default 0.8): level 2 is excluded and personal weight goes to level 1.
- Scope does not enter the build. Two scopes do not mean two scores.

---

## 12. Layered recommendation algorithm

### 12.1 Overview

```
raw      = min(1, N_eff / N0)                 N0 = 30
w        = min(raw, 1 − ε)                    ε = 0.1
h        = 0 if no other Love/Like, else 0.5 · (1 − raw)
c        = c_max · (1 − raw)                  c_max = 0.25 (reco.tmdbAmplifier.maxShare)
Prior'(t)= (1 − h) · Prior(t) + h · Household(t)
Score(t) = (1 − w) · Prior'(t) + w · Personal(t)
Base     = a · Sparse(t) + b · Knn(t)         b = 0 if the model is missing; a + b = 1
Personal = (1 − c) · Base + c · TmdbAmp(t)    only if t is reached by the graph; otherwise Base
```

`N_eff` and `FuseScore` remain those of `RecoLearning`. The household term only modifies the prior slot before fusion. Initial weights `a = 0.4`, `b = 0.6`. Without the model, `a = 1`. Component scores are normalized to [0, 1] on the pool before fusion. `Household` is sparse level 1 on the mean Love/Like of other profiles (BR-REC-15). `TmdbAmp` propagates local positive signals along TMDB `/recommendations` arcs already intersected with the pool (no out-of-catalog titles); at `N_eff ≥ N0`, `c = 0`.

| Level | What | Requires | Active from |
|---|---|---|---|
| 0 | Prior | `VoteTmdb`, `VoteTmdbCount`, `Popularity` | always |
| 1 | Sparse profile | signals + genres (keywords and credits when persisted) | `N_eff > 0` |
| 1b | TMDB amplifier | in-catalog arcs from `/recommendations` | `N_eff > 0` and `c > 0` |
| 2 | Embedding kNN | model + coverage | `N_eff > 0` and coverage ≥ 80% |
| 3 | Classifier | ≥ 100 explicit ratings and validation | optional (§12.6) |

The taste profile sums signals from **all** sources **of the profile**. A like on a title present only on Prime Video raises `Sparse` and `Knn` of Netflix Italy titles near that one. Those Prime Video titles do not appear in Netflix Italy’s `source` scope because the join on `Availability` stops them on read.

### 12.2 Level 0 — Prior

```
bayes(t) = (v/(v+m)) · R + (m/(v+m)) · C
           v = VoteTmdbCount, R = VoteTmdb, C = pool mean, m = 300
pop(t)   = log(1 + Popularity) normalized to [0, 1] on the pool
Prior(t) = 0.7 · norm(bayes) + 0.3 · pop
```

Title without TMDB vote: `bayes` falls to `C`, `pop` to 0. It stays in the pool. The prior can be computed per type (movie / series / stand-up). MMR (§12.8) completes variety.

### 12.2bis Household prior

Under `N0`, the non-personal slot becomes `Prior'` (formulas in §12.1). `Household(t)` is the sparse (level 1) score of the per-title mean of other profiles’ Love/Like weights; who has not rated the title does not enter the mean. Level 1 only: no kNN on the household vector. At `raw = 1` (or without other positive ratings) fusion with TMDB prior alone is recovered. At `raw = 0` without other positive ratings the prior is recovered.

### 12.3 Level 1 — Sparse profile

Terms, in order of available data:

| Prefix | Source | When |
|---|---|---|
| `g:{genreId}` | `TitleGenre` | already in DB; id = TMDB id |
| `l:{OriginalLanguage}` | `Title` | already in DB |
| `y:{decade}` | `ReleaseYear` | already in DB |
| `t:{Category}` | `Title.Category` | `movie` / `series` / `standup` |
| `k:{keywordId}` | `TitleKeywords` | after §11.2 |
| `p:{personId}` | `TitleCredits` role `Cast` | after §11.2 |
| `d:{personId}` | `TitleCredits` `Director` or `Creator` | after §11.2 |
| `c:{country}` | `OriginCountries` | if the column exists |

TF-IDF weight (IDF on the pool). Dimension weights: genres 1.0; keywords 1.0; directors/creators 0.8; cast 0.5; language and country 0.3; decade 0.3; type 0.5. L2 vector.

```
P = Σ_i s_i · x_i
Sparse(t) = cos(P, x_t)
```

Negative terms come from *Dislike*. The truncated profile (about 200 terms by absolute value) lives in `RecoState.ProfileJson`.

Until keywords and credits are persisted, level 1 uses genres, language, decade, and type. That is enough for initial R2; keywords and people turn on by themselves when the tables fill.

### 12.4 Level 2 — kNN

For every title in the pool, the **K** nearest titles with a signal (K = 10) by dot product (unit vectors):

```
neighbors(t) = top-K over {i : s_i ≠ 0} by sim(t, i) = e_t · e_i
Knn(t)       = Σ sim(t,i)^γ · s_i  /  Σ sim(t,i)^γ          γ = 3
```

- The result is in [−1, 1] and is mapped to [0, 1] before fusion.
- With fewer than K neighbors above `sim_min` (default 0.25), the score is damped toward 0.5.
- Neighbors may sit on another source: that is the single taste profile **per household profile**.

**Similar titles** (FR-REC-RC-09): for title `q`, neighbors in the pool by `e_q · e_t`. Without embeddings, sparse cosine of level 1. Then keep only titles with `enabled` membership (BR-REC-14), remove rated ones and, if the library filter is on, those in library. Order is similarity. The response includes `places`.

### 12.5 Fusion

```
Personal(t) = a · Sparse'(t) + b · Knn'(t)
Score(t)    = (1 − w) · Prior'(t) + w · Personal(t)     // FuseScore; Prior' includes any household
```

- `N_eff = 0` → `w = 0` → only `Prior'` (TMDB and, if any, household).
- `N_eff ≥ N0` → `h = 0` and the TMDB prior remains a residual `ε = 0.1`: `Score = 0.1·Prior + 0.9·Personal`.
- Global parameters in `Settings` (§16); implicit/library toggles on the profile (BR-REC-16).

### 12.6 Level 3 — optional

With at least `reco.ml.minRatings` explicit ratings (default 100, at least 20 per class), regularized logistic in ML.NET on features `[x_t hashed, Sparse, Knn, Prior, year, vote, popularity]`, target *Like* versus *Dislike*.

Replaces `Personal` only if in offline evaluation it beats level 2 with a sufficient margin and sample. Outside the MVP (D-REC-06).

### 12.7 Reasons

Up to 3, in `ReasonsJson`, computed in the build:

| Type | Origin | Text (built by the frontend) |
|---|---|---|
| `similarTo` | up to 2 neighbors with a positive signal and similarity above threshold | “Similar to *Dark*” |
| `terms` | profile terms with the largest contribution | genres, keywords, people |
| `popular` | high prior, low personal contribution | “Highly rated by audiences” |
| `explore` | exploration quota | “Something different” |

The JSON holds keys and ids, not translated phrases.

### 12.8 Diversity and exploration

**MMR** (FR-REC-RC-10), on read, over the first 200 results after scope and filters:

```
MMR(t) = λ · Score(t) − (1 − λ) · max_{u already chosen} sim(t, u)     λ = 0.7
```

`sim` is the embedding cosine, or the sparse one. The first-page list is kept in memory for the filter combination, with a cursor for later pages.

**Exploration** (FR-REC-RC-11): fraction `reco.explore.rate` (default 0.10) of positions for mid-high score titles far from profile terms (genres never touched by signals). Deterministic seed from `Version`. Reason `explore`.

### 12.9 Cold start

- `N_eff = 0`: prior, with MMR.
- Onboarding: 24 high-popularity pool titles, chosen to cover genres and eras (greedy on genres; on embeddings if present). Only titles with `enabled` membership. A refresh passes `exclude` with ids already shown and repeats selection on the rest of the pool; ratings already given stay in session until *Done*.
- Similar does not require ratings.

---

## 13. Local model (embedding)

### 13.1 Starting choice

| Aspect | Choice |
|---|---|
| Model | `sentence-transformers/all-MiniLM-L6-v2`, ONNX, int8 quantized variant |
| Files | about 23 MB; variants `avx2`, `avx512`, `avx512_vnni`, `arm64` (to confirm, SP-R2) |
| Vector | 384 |
| Length | 256 tokens |
| License | Apache-2.0 |
| Runtime | `Microsoft.ML.OnnxRuntime`, one session, `IntraOpNumThreads` default 2 |
| Tokenizer | WordPiece. .NET choice in spike SP-R2 (`Microsoft.ML.Tokenizers` or other library) |
| Pooling | mean of tokens with attention mask, then L2 |

Preferred text is English because the model is English. The `en-US` localization is added in `TitleLocalizations`; meanwhile the text already present is used (BR-REC-09), accepting a less stable embedding until English arrives.

The downloader chooses `arm64` on `linux/arm64` and, on `amd64`, `avx512_vnni` > `avx512` > `avx2`. SP-R2 verifies the quantized binary with ONNX Runtime.

### 13.2 Text

```
"{name} ({year}). {genres}. Keywords: {k1, … max 15}. {tagline}. {overview}"
```

`TextHash` = SHA-256 of the text.

### 13.3 Distribution

- Folder `/config/models/{modelId}/`: `model.onnx`, tokenizer, `model.json` (dimension, pooling, length, hash).
- Download from configurable URL, SHA-256 fixed in code, resumable.
- Self-test: embedding of 3 reference phrases, cosine ≥ 0.99 versus expected vectors. Otherwise `Error` status.
- Manual copy and *Detect* action.
- Image `:latest-ml` with the model inside: possible later, not in the MVP.

### 13.4 Replacement

`reco.embeddings.modelId` selects the descriptor. `TitleEmbeddings` keeps multiple `ModelId`. Under `minCoverage` the previous model is used, if present, otherwise level 1. *Clear derived data* deletes embeddings.

### 13.5 Resources

- Batch 16–32, short pause between batches if `reco.embeddings.throttle` is on.
- Vectors in memory as contiguous `float[]`. Loaded on first use and invalidated when the embedding set changes.
- Speed to measure (SP-R2). The job is background on the `ml` queue.

---

## 14. API contracts

Prefix `/api/v1`. RFC 7807 errors like the rest of the API. New field on existing list and detail DTOs:

```json
"rating": "love" | "like" | "dislike" | "not_interested" | null
```

Added to `TitleListItemDto` and the detail DTO, so catalog, list, and dialog show the same rating.

### 14.1 Rating

| Method | Path | Body / effect |
|---|---|---|
| `PUT` | `/titles/{id}/rating` | `{ "value": "love" \| "like" \| "dislike" \| "not_interested" }`. Idempotent. `204`. |
| `DELETE` | `/titles/{id}/rating` | Removes. Idempotent. `204`. |
| `GET` | `/ratings?value=love\|like\|dislike\|not_interested&q=&page=` | Paged list, same shape as `TitleListItem` plus `rating` and `places`. |
| `GET` | `/ratings/export` | JSON: `tmdbType`, `tmdbId`, name, value, date. Only titles that have the TMDB id. |
| `POST` | `/ratings/import` | `{ "mode": "merge" \| "replace", "items": [...] }`. Counters `imported`, `unknown`, `ignored`. `unknown` = TMDB id not present in `Titles`. No title inserts. |
| `POST` | `/ratings/onboarding` | List `{ "titleId", "value" }`. One rebuild at batch end. Null `value` = skip, does not write a rating. |

`id` is `Title.Id`. A rating set from the Netflix card is the same row read from the Prime Video card.

### 14.2 Recommendations

**`GET /recommendations`**

| Query | Values |
|---|---|
| `scope` | `source` (default) or `enabled` |
| `sourceId` | Required if `scope=source`. It is the `CatalogSource` id. With `scope=enabled` it is optional and only chooses the name language. |
| `type` | `movie`, `series`, `standup`, repeatable or CSV, like the catalog |
| `genre` | genre id, CSV |
| `yearFrom`, `yearTo` | |
| `includeInLibrary` | default `false` |
| `diversify` | default `true` |
| `page`, `pageSize` | default 48, max 100 |

Response: page like `PagedTitles`, plus `version`, `stage`, and on each item the `reco` block. With `scope=source` the item has that source’s `availability` (fields already used: `addedAt`, `removedAt`, `leavingDate`, `isNew`). With `scope=enabled` it has `places` for every enabled source of the title, even if `RemovedAt` is set: in that case the place includes `removedAt`.

```json
{
  "version": 42,
  "stage": "warming",
  "page": 1,
  "pageSize": 48,
  "total": 912,
  "items": [
    {
      "id": 205,
      "name": "Example",
      "originalTitle": "Example",
      "year": 2023,
      "category": "series",
      "posterUrl": "https://image.tmdb.org/t/p/w342/xyz.jpg",
      "genres": [{ "id": 80, "name": "Crime" }],
      "ratingTmdb": 8.1,
      "ratingImdb": null,
      "rating": null,
      "isFavorite": false,
      "availability": {
        "addedAt": "2024-01-10T00:00:00Z",
        "removedAt": null,
        "leavingDate": null,
        "isNew": false
      },
      "places": [{ "providerName": "Netflix", "countryCode": "IT", "removedAt": null }],
      "arr": { "kind": "sonarr", "state": "notInLibrary" },
      "reco": {
        "score": 0.83,
        "personalized": true,
        "reasons": [
          { "kind": "similarTo", "titleId": 101, "name": "Dark", "similarity": 0.78 },
          { "kind": "terms", "terms": ["Crime", "Mystery"] }
        ]
      }
    }
  ]
}
```

`places` in scope `source` may contain only the requested source. In scope `enabled` it contains all enabled sources of the title.

**`GET /titles/{id}/similar?limit=20`**

No scope parameter: membership is always `enabled` (BR-REC-14). Same item shape, with `reco.similarity`. Optional `sourceId` for the name language; if missing, use the selected source language if the client passes it, otherwise the English fallback already used by detail when the source is absent.

**`GET /recommendations/status`**

```json
{
  "enabled": true,
  "stage": "warming",
  "ratingCount": 14,
  "effectiveN": 17.4,
  "targetN": 30,
  "personalWeight": 0.58,
  "model": { "status": "ready", "id": "all-MiniLM-L6-v2@q8", "sizeBytes": 23100000 },
  "embeddings": { "coverage": 0.97, "count": 11840, "pending": 360 },
  "pool": { "titles": 9120 },
  "lastBuiltAt": "2026-11-05T21:30:12Z",
  "version": 42,
  "lastError": null
}
```

**`GET /recommendations/onboarding`** — the 24 pool titles (§12.9). Optional query `exclude` (CSV of `titleId`) for a new batch without titles already shown.

### 14.3 Administration

Under `/api/v1/admin/recommendations`, same style as `AdminEndpoints`.

| Method | Path | Effect |
|---|---|---|
| `POST` | `/rebuild` | Queues `RecoBuildJob`. `202` and `SyncRun` id. |
| `POST` | `/embeddings/rebuild` | Query `all=true` to recompute everything. |
| `POST` | `/model/download` | Starts the download. |
| `POST` | `/model/detect` | Detects files copied by hand. |
| `POST` | `/evaluate` | Starts `RecoEvalJob`. |
| `GET` | `/evaluations/latest` | Latest report. |
| `POST` | `/reset` | Required query `includeRatings=true|false`. |

Errors: `RECO_DISABLED` (409), `RECO_MODEL_UNAVAILABLE` (503, only on actions that require the model), `RECO_BUILD_IN_PROGRESS` (409).

### 14.4 UI refresh

After a rating the client invalidates queries `titles`, `title`, `recommendations`, `ratings` and retries status for a few seconds until `version` changes. An SSE `recommendations-updated` event hooks in if a `/events` channel already exists (FR-ST-08); otherwise refetch is enough.

---

## 15. Frontend

### 15.1 Files

Feature folders are added next to existing ones, without a new shell.

```
src/Pickarr.WebApp/src/features/ratings/   RatingButtons, MyRatingsPage, import/export
src/Pickarr.WebApp/src/features/reco/      ForYouPage, TastePage, LearningProgress, ReasonChips
src/Pickarr.WebApp/src/features/settings/  RecommendationsSettingsPage
```

`RatingButtons` mounts in `TitleCard` and `TitleDialog`, next to `FavoriteButton`.
`SimilarRow` mounts at the bottom of `TitleDialog` as a multi-row grid.
`ForYouPage` reuses `FilterBar` (provider/country selector), `TitleGrid`, `TitleDialog`.

### 15.2 Routes

In `AppRouter`, inside `AppShell`:

| Path | Page |
|---|---|
| `/for-you` | *For you*. `scope` in search params. `source` is the shared one. |
| `/for-you?title=:id` | Dialog on the grid, like `/catalog?title=`. |
| `/taste` | Onboarding |
| `/ratings` | My ratings |
| `/settings/recommendations` | Section in `SettingsLayout` |

`Header`: *For you* link between catalog and *My list*.

### 15.3 Shared source

`ForYouPage` calls `loadPreferredSourceId` / `savePreferredSourceId` and, when sources are loaded (`GET /catalog/sources`), writes `source` into search params as `CatalogPage` does. Changing provider or country updates the same key. Scope `enabled` does not change the remembered source: it is still needed for language and for the badge in *My ratings*.

### 15.4 State

- `useRecommendations` with `useInfiniteQuery`, key that includes `scope` and `sourceId`.
- `useSimilar(titleId)`.
- `useRecoStatus()`.
- Rating mutation on the same scheme as `FavoriteButton`: optimistic on keys `['titles']`, `['title', id]`, `['recommendations']`, `['ratings']`, rollback on error.
- A *Dislike* on a *For you* card removes it from the list immediately, because rated titles are not recommendations (FR-REC-RT-04).
- Chosen scope lives in the page search params (shareable). Source lives in `localStorage`, like the catalog.

### 15.5 Components

- **`RatingButtons`**: two buttons. The active rating is pressed. A second click on the same removes it.
- **`LearningProgress`**: `N_eff / targetN` and the stage, with link to `/taste`. Hidden entirely when `N_eff ≥ targetN`.
- **`ReasonChips`**: i18n keys `reco.reason.similarTo`, `reco.reason.terms`, `reco.reason.popular`, `reco.reason.explore`.
- **Scope**: two entries. With a single enabled source only *This source* remains.
- **`OnboardingTaste`**: 24 cards, *Show more titles*, *Done*, shortcuts `L` and `D`.

### 15.6 i18n

Namespaces `reco` and `ratings` in `en.json` and `it.json`. Missing key → English (FR-I18N-02).

---

## 16. Configuration

Keys in `AppSetting` / `SettingsKeys`, read like other settings.

| Key | Default | Notes |
|---|---|---|
| `reco.enabled` | `true` | FR-REC-ST-01 |
| `reco.useImplicit` | `true` | legacy; build reads `Profile.UseImplicit` |
| `reco.weights.love` | `1.5` | |
| `reco.weights.like` | `1.0` | |
| `reco.weights.dislike` | `-1.0` | |
| `reco.weights.notInterested` | `-0.75` | |
| `reco.weights.favorite` | `0.5` | |
| `reco.weights.arrAdd` | `0.6` | |
| `reco.weights.downloaded` | `0.4` | |
| `reco.weights.watched` | `0` | Hide: exclusion without taste mass |
| `reco.halfLifeDays` | `0` | 0 = no decay |
| `reco.n0` | `30` | |
| `reco.tmdbAmplifier.maxShare` | `0.25` | cap of the TMDB graph in the personal slot at cold start; 0 at `N0` |
| `reco.priorResidual` | `0.1` | ε |
| `reco.sparse.weight` | `0.4` | |
| `reco.knn.weight` | `0.6` | |
| `reco.knn.k` | `10` | |
| `reco.knn.gamma` | `3` | |
| `reco.knn.simMin` | `0.25` | |
| `reco.mmr.lambda` | `0.7` | 0 disables MMR |
| `reco.explore.rate` | `0.10` | 0 disables |
| `reco.excludeInLibrary` | `true` | legacy; build/read use `Profile.ExcludeInLibrary` |
| `reco.embeddings.enabled` | `true` | takes effect if the model is present |
| `reco.embeddings.modelId` | `all-MiniLM-L6-v2@q8` | |
| `reco.embeddings.threads` | `2` | |
| `reco.embeddings.batchSize` | `16` | |
| `reco.embeddings.minCoverage` | `0.8` | |
| `reco.embeddings.throttle` | `true` | |
| `reco.ml.minRatings` | `100` | level 3 |
| `reco.events.enabled` | `false` | |

Environment variables:

| Variable | Default | |
|---|---|---|
| `Pickarr__Models__Path` | `/config/models` | |
| `Pickarr__Models__DownloadBaseUrl` | model repository URL | internal mirror |
| `Pickarr__Reco__MaxThreads` | `2` | ONNX cap, wins if lower than the setting |

There is no separate display language: `CatalogSource.MetadataLanguage` is used.

---

## 17. Tests and evaluation

### 17.1 Automated

| Level | What |
|---|---|
| **Unit — algorithm** | Pure functions in `Pickarr.Core`: sparse vectors, TF-IDF, profile, kNN, prior, fusion, MMR, deterministic exploration. Synthetic dataset: only science fiction in likes → science fiction on top; a like does not worsen similar order; a dislike lowers neighbors; zero signals → order = prior. |
| **Property** | Components in [0, 1]. No rated title on the recommendations page. Same input → same order. |
| **Unit — membership** | Scope `source` keeps titles with `Availability` on that source even if `RemovedAt` is set. Scope `enabled` keeps the union of enabled sources, one row per `Title`. A disabled source does not contribute. A title without `Availability` does not enter. |
| **Unit — rating** | Second `PUT` replaces the value. `DELETE` removes the row and writes an event with null value. Two sources, same `Title.Id`: one rating. |
| **Unit — signals** | Favorite, successful `Add`, and `Downloaded` sum and clip to 1. An explicit rating replaces them. `Delete` does not negate the `Add`. |
| **Unit — import** | Unknown TMDB id → `unknown`, no new `Titles` row. |
| **Integration** | `WebApplicationFactory`: rating → rebuild → `GET /recommendations` for both scopes. Title on Netflix IT and Prime Video: once in scope `enabled`, with two places; in Netflix IT scope once, and the like given “from the other side” is the same `rating`. Similar: a neighbor only on Prime Video is returned. |
| **Model** | Conditional test, skipped if the file is missing: reference phrases, cosine ≥ 0.99; same text → same vector. |
| **Frontend** | Optimistic mutation and rollback. Scope. `ReasonChips`. Per-profile source: changing provider on *For you* updates `pickarr.sourceId.{profileId}`. Profile header on every personal call. |

### 17.2 Data

`tests/fixtures/reco/`: titles with genres and, where needed, synthetic embeddings. No real data.

### 17.3 Offline evaluation

Leave-one-out on ratings: recompute the profile without that title and compare its score with a sample of unrated pool titles.

Metrics: AUC, hit@10, AUC *Like* versus *Dislike*. Comparison among prior, level 1, level 2, combination, level 3. The report includes the count. Under about 30 ratings it warns that the number is low.

Used to tune `a`, `b`, `N0`, `γ`, `K` and to decide level 3. Not to chase decimals with few ratings.

### 17.4 Events

If `reco.events.enabled`: (likes or Sonarr/Radarr adds among titles shown in *For you*) / (titles shown), by version and stage. Local only.

---

## 18. Build, container, and CI

- Dependencies: `Microsoft.ML.OnnxRuntime` (CPU, native `linux-x64` and `linux-arm64`), a WordPiece tokenizer (SP-R2), `System.Numerics.Tensors`. `Microsoft.ML` only if level 3 is implemented.
- Dockerfile: verify native library loading on the `aspnet` image for amd64 and arm64, and that `/config/models` is writable by the non-root user (SP-5).
- CI: algorithm and API tests in the backend test job; a manual job downloads the model and runs conditional tests; image smoke loads a minimal ONNX session; OpenAPI schema includes the new paths.
- `README` / `NOTICE`: model license. No JustWatch attribution: watch providers are not used.

---

## 19. Risks, spikes, and open decisions

### 19.1 Spikes

| ID | Questions |
|---|---|
| **SP-R1** | Does the already-requested TMDB payload suffice for keywords, person id, crew, and `created_by` without changing the URL? Cost of the extra `language=en-US` call on the current rate limiter. |
| **SP-R2** | .NET tokenizer, pooling, reference vectors, titles/second on x64 and arm64, quantized variant, RAM, image size, similar quality on a sample of the real catalog. |
| **SP-R3** | Rebuild time on the install pool (one and multiple sources) with 500 signals. `GET /recommendations` time with the join on `Availabilities` and the library filter. MMR on read. |
| **SP-R4** | With 30–50 real ratings: compare levels 0/1/2 and first tuning of `a`, `b`, `N0`, `γ`, `K`, `m`. |
| **SP-R5** | TMDB terms of use for local inference on texts already in cache, and model license. Manual check before release. |

### 19.2 Risks

| ID | Risk | Mitigation |
|---|---|---|
| RR-01 | Few ratings. | Prior, similar without ratings, onboarding, implicit signals, indicator. |
| RR-02 | Filter bubble. | Exploration, MMR, residual prior, reset. |
| RR-03 | Title with thin metadata (not enriched, no genres). | Stays in the pool with a weak prior. Level 1 treats it as a nearly empty vector. |
| RR-04 | Weak CPU (ARM NAS). | Small model, one `ml` worker, minimum coverage before level 2, level 1 always available. |
| RR-05 | Model download impossible. | Manual copy, hash, self-test. The app works without. |
| RR-06 | The *For you* query copies the catalog’s `RemovedAt == null` filter and hides titles that should be recommended. | BR-REC-07 and dedicated membership tests. Predicate written in one place, different from `TitleQueryService` on the grid view. |
| RR-07 | Two `Titles` rows for the same film, because catalog merge did not unite them. | Rating follows `Title.Id` (BR-REC-08). No merge is added in this feature. |
| RR-08 | Misleading implicit signals (downloaded for someone else). | Weights below the rating, switch, explicit rating that prevails. |
| RR-09 | TMDB terms on use of texts for a local model. | SP-R5. Inference only, no redistributed model trained on those texts. |
| RR-10 | SQLite writes during rebuild. | Single-worker `ml` queue, publish transaction, WAL. |

### 19.3 Decisions

| ID | Decision |
|---|---|
| D-REC-01 | Default *For you* scope: the selected source (`source`). |
| D-REC-02 | The other scope is the union of enabled sources. There is no scope beyond imported catalogs. |
| D-REC-03 | The profile is unique. The catalog filter applies to the list, not to learning. |
| D-REC-04 | Similar always uses the union of enabled sources and shows `places`. |
| D-REC-05 | `RemovedAt` does not exclude. |
| D-REC-06 | Ratings: *Love*, *Like*, *Dislike*, *Not interested*; *Hide* separate. |
| D-REC-07 | Dedicated page `/for-you`. *For you* sorting in the catalog grid is out of scope. |
| D-REC-08 | *My ratings* is global, with a badge on the selected source. Onboarding draws from the enabled-catalogs pool. |
| D-REC-09 | Titles already in library excluded by default, with a switch. |
| D-REC-10 | Feature on by default. The local model downloads on request. |
| D-REC-11 | Starting model: quantized `all-MiniLM-L6-v2`, to revisit after SP-R2 and SP-R4. |
| D-REC-12 | Level 3 outside the MVP. |
| D-REC-13 | Implicit signals on by default, disableable. |

---

## 20. Release phases

| Phase | Content | Value |
|---|---|---|
| **R1 — Rating and prior** | `Ratings`, `RatingEvents`, buttons, `rating` field on DTOs, ratings page, export/import, `/for-you` with prior, both scopes, shared source, indicator. | Rating starts immediately. First recommendations are the strong titles of the catalog being browsed. |
| **R2 — Profile** | Keyword/credits and `en-US` persistence on existing enrichment, level 1, implicit signals, reasons, similar via sparse profile, rebuild with debounce. | Ratings, including those given on another service, move the order inside the current catalog. |
| **R3 — Model** | Download, embedding, kNN, similar via embedding, settings panel, MMR, exploration, onboarding. | Finer recommendations when tastes span more than one genre. |
| **R4 — Measure** | Offline evaluation, tuning, events, level 3 if the numbers support it. | You can see whether a level is better than the previous one. |

Order: **R1 → R2 → R3 → R4**. SP-R1 before R2. SP-R2 before R3.

---

## 21. Impact on existing documents

**`functional-spec.md`**

- Scope: personalized recommendations are planned, with the constraint of this document (rating on the title, lists filtered to imported catalogs).
- Decisions: report D-REC-01 … D-REC-13.
- Phases: R1–R4 after the catalog MVP.
- FR-DET-04: rating actions in the dialog. FR-UI-04: rating on the card.
- FR-FAV-02 remains the model of global data: the rating behaves like the favorite with respect to source, with *Like* / *Dislike* values instead of the boolean.

**`technical-spec.md`**

- §4: tables of §10. No `Origin` column on `Titles`.
- §3: `Pickarr.Core` module for the pure algorithm; persistence, ONNX, and metadata completion in Infrastructure, hooked to `EnrichmentService`.
- §6: Hangfire `ml` queue, second one-worker server, new `SyncRunKinds`.
- §8: paths of §14, `rating` field on list and detail DTOs.
- §9: routes and features of §15.
- §12: keys of §16.
- §14: dependencies of §18.
- §17: spikes SP-R1 … SP-R5.
- §10: `/config/models` folder and hash of the downloaded file.
