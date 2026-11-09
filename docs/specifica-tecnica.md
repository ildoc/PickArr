# PickArr — Specifica tecnica

| | |
|---|---|
| **Versione** | 0.2 |
| **Data** | 2026-11-06 |
| **Lingua** | Italiano |
| **Documenti collegati** | `documento-funzionale.md` (FR-/BR-/D- catalogo); `raccomandazioni.md` (FR-REC-*, D-REC-*, BR-REC-*) |

---

## Indice

1. Introduzione e convenzioni
2. Stack tecnologico
3. Architettura
4. Modello dati
5. Integrazioni esterne
6. Job e sincronizzazione
7. Logica di dominio
8. Contratti API
9. Frontend
10. Sicurezza
11. Osservabilità
12. Configurazione
13. Strategia di test
14. Build, packaging e CI/CD
15. Estensione multi-servizio
16. Gestione errori
17. Rischi tecnici e spike iniziali
18. Ordine di implementazione consigliato
19. Appendici

---

## 1. Introduzione e convenzioni

- **Obiettivo:** tradurre il documento funzionale in decisioni tecniche implementabili.
- **Stato delle verifiche:**
  - I dati su piani, crediti e limiti di Watchmode derivano dalla documentazione pubblica consultata il 2026-11-03.
  - I nomi esatti dei parametri Watchmode e il formato delle risposte, così come i payload di Sonarr/Radarr, sono riportati **a memoria e da validare** con gli spike (§17) contro la documentazione OpenAPI di Watchmode e la pagina `/swagger` delle istanze Sonarr/Radarr in uso. Dove un dato è da confermare è indicato con **(da verificare)**.
- **Convenzioni:**
  - Date/ora in UTC nel DB e nelle API (ISO 8601). Il fuso `TZ` serve solo per interpretare i cron.
  - Enumerazioni serializzate come stringhe in `camelCase` nelle API e in `PascalCase` nel DB.
  - JSON in `camelCase`. Errori in formato RFC 7807 (`application/problem+json`).
  - Nome del progetto provvisorio: `PickArr` (namespace `PickArr.*`).

---

## 2. Stack tecnologico

Le versioni sono quelle stabili all'avvio del progetto, da fissare in `Directory.Packages.props` e `package-lock.json`.

### Backend

| Area | Scelta |
|---|---|
| Runtime | .NET 10 (LTS), C# 14 |
| Web | ASP.NET Core **Minimal API**, gruppi di endpoint per feature |
| Persistenza | EF Core 10 + SQLite (`Microsoft.EntityFrameworkCore.Sqlite`), migrazioni EF |
| Job | Hangfire (`Hangfire.AspNetCore`) con storage SQLite su **file separato** (pacchetto di comunità, vedi SP-4) |
| HTTP client | `IHttpClientFactory` tipizzati + `Microsoft.Extensions.Http.Resilience` (retry, circuit breaker, rate limiter) |
| Validazione | FluentValidation (o validazione manuale con `IValidatableObject`) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` (nativo) + UI Scalar su `/docs` |
| Logging | Serilog (console JSON + file rotante) |
| Segreti | ASP.NET Core Data Protection (key ring persistito) |
| ML locale | `Microsoft.ML.OnnxRuntime` + `Microsoft.ML.Tokenizers` (embedding); `Microsoft.ML` (livello 3 logistico, opzionale/gated); `System.Numerics.Tensors` |
| Test | xUnit, Shouldly o AwesomeAssertions, NSubstitute, WireMock.Net, `WebApplicationFactory` |

### Frontend

| Area | Scelta |
|---|---|
| Build | Vite, TypeScript `strict` |
| UI | React 19, Tailwind CSS, componenti Radix/shadcn (dialog, select, tooltip) |
| Dati | TanStack Query v5 (`useInfiniteQuery` per la griglia) |
| Routing | React Router (route modale per la scheda) |
| Form | react-hook-form + zod |
| Virtualizzazione | `@tanstack/react-virtual` |
| i18n | `i18next` + `react-i18next` (fallback `en`) |
| API client | `openapi-typescript` + `openapi-fetch`, generato dall'OpenAPI del backend |
| Test | Vitest, Testing Library, MSW, Playwright (e2e) |
| Qualità | ESLint, Prettier |

---

## 3. Architettura

### 3.1 Topologia a runtime

Un solo container, un solo processo:

```
┌──────────────────────── container PickArr ────────────────────────┐
│  Kestrel :8080                                                       │
│   ├─ /            → SPA React (file statici in wwwroot)              │
│   ├─ /api/v1/*    → Minimal API                                      │
│   ├─ /docs        → OpenAPI + Scalar                                 │
│   └─ /health      → health check                                     │
│  Hangfire Server (in-process): code "default", "sync", "arr", "ml"   │
│    (secondo server WorkerCount=1 solo coda "ml")                     │
│  SQLite: /config/PickArr.db   /config/jobs.db   /config/keys/        │
│  Modelli ONNX: /config/models (hash verificato al download)          │
└──────────────────────────────────────────────────────────────────────┘
        │               │               │               │
     Watchmode         TMDB       Sonarr (v4)      Radarr (v5)
```

### 3.2 Struttura del repository (monorepo)

```
PickArr/
├─ PickArr.slnx
├─ Directory.Build.props
├─ Directory.Packages.props
├─ docker-compose.yml
├─ src/
│  ├─ PickArr.Core/              # dominio + casi d'uso + astrazioni (nessuna dipendenza da infrastruttura)
│  ├─ PickArr.Infrastructure/    # EF, client esterni, job, sicurezza
│  ├─ PickArr.Api/               # host, endpoint, DTO, composizione DI, Dockerfile
│  └─ PickArr.WebApp/            # SPA (vedi §9)
├─ tests/
│  ├─ PickArr.Core.Tests/
│  ├─ PickArr.Infrastructure.Tests/
│  └─ PickArr.Integration.Tests/
├─ docs/
│  ├─ documento-funzionale.md
│  └─ specifica-tecnica.md
├─ .github/workflows/ci.yml
├─ CHANGELOG.md
└─ README.md
```

**Regola di dipendenza:** `Api → Core`, `Api → Infrastructure`, `Infrastructure → Core`. `Core` non conosce EF, HTTP, Hangfire.

### 3.3 Struttura interna di `Core`

```
Core/
├─ Domain/            Entities, Enums, ValueObjects, Rules (BR-xx come funzioni pure)
├─ Abstractions/      ICatalogProvider, IMetadataProvider, IArrClient, ICreditBudget, IClock, ISettingsStore,
│                     IRatingService, IRecoBuildService, IRecoQueryService, IRecoEvalService, …
├─ Recommendations/   algoritmo puro: prior, segnali, sparse, kNN, MMR, explore, eval metrics, L3 features
├─ Catalog/           TitleQueryModels (campo rating nei DTO), membership helpers
├─ Metadata/          DTO TMDB (keywords, credits, created_by)
├─ Settings/          SettingsService, RecoSettingsDto
└─ …
```

Persistenza EF, client HTTP, Hangfire, ONNX e training L3 (`Microsoft.ML`) stanno in **Infrastructure** (es. `EnrichmentService`, `RecoBuildService`).
Gli endpoint di `Api` sono sottili: validano l'input, chiamano un servizio, mappano su DTO.

### 3.4 Registrazione e composizione

- `Program.cs`: `AddPickArrCore()`, `AddPickArrInfrastructure(config)`, `AddPickArrApi()`.
- Gli endpoint sono definiti in classi `static` con un metodo `MapXxxEndpoints(this IEndpointRouteBuilder)` per feature: `Catalog`, `Titles`, `Favorites`, `Ratings`, `Recommendations`, `RecoAdmin`, `Arr`, `Settings`, `Admin`, `Webhooks`, `System`.
- Il fallback SPA (`MapFallbackToFile("index.html")`) esclude `/api`, `/docs`, `/health`.

---

## 4. Modello dati

Database `PickArr.db` (SQLite). Convenzioni:

- Chiavi surrogate `Id` intere autoincrementali, salvo dove indicato.
- Timestamp come `DateTime` UTC con value converter (testo ISO 8601), per consentire ordinamenti e confronti. Non usare `DateTimeOffset` con SQLite.
- Enum salvati come stringa (`HasConversion<string>()`).
- Pragmi all'apertura connessione: `journal_mode=WAL`, `synchronous=NORMAL`, `foreign_keys=ON`, `busy_timeout=5000`.

### 4.1 Tabelle

**`Providers`** — servizi di streaming

| Colonna | Tipo | Note |
|---|---|---|
| Id | int PK | |
| Key | text UNIQUE | es. `netflix` |
| DisplayName | text | |
| ProviderType | text | es. `watchmode` (selettore dell'implementazione di `ICatalogProvider`) |
| ExternalId | text null | per Watchmode, il `source_id` (Netflix = 203) |
| IsEnabled | bool | |
| CreatedAt | datetime | |

**`CatalogSources`** — servizio + paese

| Colonna | Tipo | Note |
|---|---|---|
| Id | int PK | |
| ProviderId | int FK | |
| CountryCode | text(2) | ISO 3166-1 alpha-2 maiuscolo |
| MetadataLanguage | text | es. `it-IT`, `en-US` |
| IsEnabled | bool | |
| IncrementalCron | text | default `0 4 * * *` |
| FullSyncCron | text | default `0 3 * * 0` |
| IncrementalPages | int | default 3 |
| BaselineCompletedAt | datetime null | impostata alla prima sync completa riuscita |
| LastIncrementalAt, LastFullAt | datetime null | |
| LastFullTotalPages | int null | stima per budget |

Indice univoco `(ProviderId, CountryCode)`.

**`Titles`**

| Colonna | Tipo | Note |
|---|---|---|
| Id | int PK | |
| WatchmodeId | int UNIQUE null | id Watchmode |
| TmdbId | int null | |
| TmdbType | text null | `movie` / `tv` |
| ImdbId | text null | `tt…` |
| TvdbId | int null | solo serie |
| MediaType | text | `Movie` / `Series` (derivato dal tipo TMDB, o da Watchmode se TMDB assente) |
| Category | text | `Movie` / `Series` / `StandUp` |
| OriginalTitle | text null | |
| ReleaseYear | int null | |
| RuntimeMinutes | int null | film |
| SeasonCount, EpisodeCount | int null | serie |
| VoteTmdb | real null | |
| VoteTmdbCount | int null | |
| ImdbRating | real null | opzionale (OMDb) |
| Popularity | real null | |
| OriginalLanguage | text null | |
| PosterPath, BackdropPath | text null | percorsi TMDB neutri |
| MetadataStatus | text | `Pending` / `Ready` / `Unmapped` / `Failed` |
| MetadataFetchedAt | datetime null | |
| MetadataAttempts | int | |
| CreatedAt, UpdatedAt | datetime | |

Indici: `(TmdbType, TmdbId)` univoco parziale (`WHERE TmdbId IS NOT NULL`), `TvdbId`, `ImdbId`, `(MetadataStatus)`, `(ReleaseYear)`.

**`TitleLocalizations`** — PK `(TitleId, Language)`

| Colonna | Tipo | Note |
|---|---|---|
| TitleId, Language | PK | lingua BCP 47 (`it-IT`, `en-US`) |
| Name | text | |
| NameNormalized | text | minuscolo, senza diacritici, per la ricerca |
| Overview | text null | |
| Tagline | text null | |
| FetchedAt | datetime | |

Indice su `NameNormalized`.

**`Genres`** (PK = id TMDB) e **`GenreLocalizations`** `(GenreId, Language, Name)`; **`TitleGenres`** `(TitleId, GenreId)`.

**`Availabilities`** — relazione titolo/sorgente

| Colonna | Tipo | Note |
|---|---|---|
| Id | int PK | |
| TitleId | int FK | |
| CatalogSourceId | int FK | |
| FirstSeenAt | datetime | primo avvistamento |
| LastSeenAt | datetime | ultima conferma in una lettura |
| AddedAt | datetime null | data di aggiunta; null se baseline |
| IsBaseline | bool | |
| MissedFullScans | int | contatore di assenze nelle sync complete (BR-05) |
| RemovedAt | datetime null | |
| LeavingDate | date null | data di scadenza nota |

Indice univoco `(TitleId, CatalogSourceId)`; indice `(CatalogSourceId, RemovedAt, AddedAt)`.

**`AvailabilityEvents`**

| Colonna | Tipo | Note |
|---|---|---|
| Id | int PK | |
| AvailabilityId | int FK | |
| Type | text | `Added`, `Removed`, `Readded`, `LeavingAnnounced`, `LeavingCleared` |
| OccurredAt | datetime | |
| SyncRunId | guid null | |
| Details | text null | JSON |

**`Favorites`** — PK `TitleId`, `CreatedAt`.

**`Ratings`** — PK `TitleId`; `Value` (`Like`/`Dislike`), `RatedAt`. Voto globale sul titolo (nessuna colonna `Origin` su `Titles`).

**`RatingEvents`** — storico: `Id`, `TitleId`, `Value` (null = clear), `At`, `Origin` (`Ui`/`Onboarding`/`Import`).

**`Keywords`** (PK = id TMDB, `Name`), **`TitleKeywords`** `(TitleId, KeywordId)`.

**`People`** (PK = id TMDB, `Name`), **`TitleCredits`** `(TitleId, PersonId, Role, Order)` con `Role` ∈ `Cast`/`Director`/`Creator`.

**`TitleEmbeddings`** — PK `(TitleId, ModelId)`; `Vector` BLOB (float32 LE), `TextHash`, `UpdatedAt`.

**`Recommendations`** — PK `(Version, TitleId)`; `Score`, `Personal` null, `Prior`, `Rank`, `ReasonsJson`. Indice `(Version, Score DESC)`.

**`RecoState`** — singleton `Id=1`: `CurrentVersion`, `BuiltAt`, `RatingCount`, `EffectiveN`, `Stage`, `ModelStatus`, `ModelId`, `EmbeddingCoverage`, `ProfileJson`, `LastError`, `LastEvalJson`, `LastEvalAt`, `Level3Active`.

**`RecoEventLog`** (solo se `reco.events.enabled`): `Id`, `At`, `TitleId`, `Event` (`Shown`/`Opened`/`Liked`/`Disliked`/`AddedToArr`), `Version`, `Rank`, `Scope`.

**`ArrLibraryItems`** — cache della libreria Sonarr/Radarr

| Colonna | Tipo | Note |
|---|---|---|
| Id | int PK | |
| Kind | text | `Sonarr` / `Radarr` |
| ArrId | int | id nell'istanza |
| TmdbId, TvdbId | int null | |
| ImdbId | text null | |
| TitleId | int FK null | match con il catalogo (null se non presente) |
| ArrTitle | text | |
| Year | int null | |
| Monitored | bool | |
| State | text | `Missing` / `Downloading` / `Downloaded` / `Partial` |
| QueueProgress | real null | 0–100 |
| SizeOnDisk | bigint | |
| Path | text null | |
| EpisodeFileCount, EpisodeCount, TotalEpisodeCount | int null | serie |
| HasFile | bool null | film |
| IsAvailable | bool null | film (Radarr `isAvailable`) |
| SyncedAt | datetime | |

Indice univoco `(Kind, ArrId)`; indici su `TmdbId`, `TvdbId`, `TitleId`.

**`ArrSeasonStates`** — PK `(ArrLibraryItemId, SeasonNumber)`: `Monitored`, `EpisodeFileCount`, `EpisodeCount`, `TotalEpisodeCount`, `SizeOnDisk`, `State`, `QueueProgress`.

**`ArrAuditLog`**: `Id`, `At`, `Action` (`Add`/`Delete`), `Kind`, `ArrId`, `TitleId null`, `Options` (JSON), `Outcome`, `Error null`.

**`SyncRuns`**

| Colonna | Tipo | Note |
|---|---|---|
| Id | guid PK | |
| Kind | text | `CatalogIncremental`, `CatalogFull`, `Enrichment`, `ArrSync`, `Leaving`, `RecoFeature`, `RecoEmbedding`, `RecoBuild`, `RecoEval`, `RecoModelDownload` |
| CatalogSourceId | int null | |
| TriggeredBy | text | `Schedule` / `Manual` / `Resume` / `Webhook` |
| StartedAt, FinishedAt | datetime / null | |
| Status | text | `Running`, `Succeeded`, `PartiallySucceeded`, `Paused`, `NeedsReview`, `Failed`, `Cancelled` |
| PagesFetched, CreditsUsed | int | |
| TitlesAdded, TitlesUpdated, TitlesRemoved | int | |
| ErrorMessage | text null | |
| CheckpointJson | text null | cursore per la ripresa |

**`ApiUsage`**: `Id`, `Service` (`watchmode`/`tmdb`/`omdb`), `Endpoint`, `Timestamp`, `Credits` (int), `QuotaUsed` (int null), `QuotaLimit` (int null), `StatusCode`, `SyncRunId null`. Indice su `(Service, Timestamp)`.

**`Settings`**: `Key` PK, `ValueJson`, `IsSecret` (valore cifrato con Data Protection), `UpdatedAt`.

### 4.2 Database dei job

`jobs.db` è un file distinto, gestito interamente da Hangfire, per evitare contesa di lock con `PickArr.db`.

### 4.3 Migrazioni e seed

- Le migrazioni EF sono applicate all'avvio (`Database.MigrateAsync()`), prima dell'avvio dei job.
- Seed idempotente: `Providers(netflix)`, `CatalogSources(netflix, IT, it-IT)` e `(netflix, US, en-US)` (disabilitate finché non c'è la chiave Watchmode), impostazioni di default.
- Prima di ogni migrazione distruttiva si crea una copia di sicurezza del file (`PickArr.db.bak-<versione>`).

### 4.4 Ricerca

Fase 1: `NameNormalized LIKE '%term%'` su `TitleLocalizations` (lingua della sorgente + `en-US`) e `Titles.OriginalTitle` normalizzato. Con alcune migliaia di righe è adeguato. Se necessario (misurare con NFR-01), passare a una tabella virtuale **FTS5** mantenuta da trigger. L'API di ricerca non cambia.

---

## 5. Integrazioni esterne

Pattern comune a tutti i client:

- Client tipizzato con `HttpClient` da `IHttpClientFactory`, base address e header di autenticazione configurati in fase di richiesta (leggendo le impostazioni correnti, quindi senza riavvio).
- Pipeline di resilienza: timeout per tentativo, **retry** con backoff esponenziale e jitter su 5xx/timeout/429 (rispettando `Retry-After`), **circuit breaker**, **rate limiter** in uscita.
- Nessun segreto nei log: le chiavi viaggiano in header, non in query string, dove l'API lo consente.
- Ogni chiamata a Watchmode e TMDB passa da un `DelegatingHandler` che scrive in `ApiUsage`.

### 5.1 Watchmode

- **Base URL:** `https://api.watchmode.com/v1`
- **Autenticazione:** header `X-API-Key` (la documentazione prevede anche il parametro `apiKey`; si usa l'header per non esporre la chiave negli URL).
- **Crediti:** ogni risposta riporta `X-Account-Quota` e `X-Account-Quota-Used`; il costo di alcune chiamate è maggiore di 1 credito **(misurare, SP-1)**.
- **Rate limit:** limitatore in uscita di default 1 richiesta/secondo (configurabile), per essere conservativi.

Endpoint utilizzati:

| Uso | Endpoint | Note |
|---|---|---|
| Elenco servizi | `GET /sources/` (filtri `regions`, `types`) | Trova l'id del servizio (Netflix = 203). Risultato memorizzato in `Providers`. |
| Elenco paesi | `GET /regions/` | Valida i paesi e il livello di qualità dei dati. |
| Elenco titoli | `GET /list-titles/` | Cuore della sync. Parametri **(da verificare)**: `source_ids`, `regions`, `types` (es. `movie,tv_series,tv_miniseries,tv_special`), `sort_by`, `page`, `limit` (max 250). |
| Scadenze | endpoint "leaving dates" (`getTitleLeavingDates`) | Free: anteprima dei prossimi 10 per paese. Percorso e parametri da verificare. |
| Quota | `GET /status/` | Letta solo se i costi lo consentono; altrimenti si usano gli header. |
| Feed modifiche (piano a pagamento) | `GET /changes/titles_sources_changed`, `/changes/new_titles` | Usati solo con `WatchmodePlan = Paid` (FR-CAT-13). 1 credito per pagina. |

Forma attesa della risposta di `list-titles` **(da verificare)**:

```json
{
  "titles": [
    { "id": 1234567, "title": "Example", "year": 2024,
      "imdb_id": "tt1234567", "tmdb_id": 987654, "tmdb_type": "movie",
      "type": "movie" }
  ],
  "page": 1, "total_results": 6120, "total_pages": 25
}
```

Mappatura del tipo Watchmode in `MediaType`:

| Watchmode `type` / `tmdb_type` | MediaType |
|---|---|
| `movie` / `movie` | Movie |
| `tv_series`, `tv_miniseries` / `tv` | Series |
| `tv_special` | in base a `tmdb_type` (`movie` → Movie, `tv` → Series) |

### 5.2 TMDB

- **Base URL:** `https://api.themoviedb.org/3`. **Auth:** `Authorization: Bearer <read access token>`.
- **Rate limit:** limitatore in uscita di default 20 richieste/secondo.

| Uso | Endpoint |
|---|---|
| Dettaglio film | `GET /movie/{id}?language={lang}&append_to_response=external_ids,keywords` |
| Dettaglio serie | `GET /tv/{id}?language={lang}&append_to_response=external_ids,keywords` |
| Mappatura da IMDb | `GET /find/{imdb_id}?external_source=imdb_id` (fallback se manca `tmdb_id`) |
| Generi | `GET /genre/movie/list?language={lang}` e `GET /genre/tv/list?language={lang}` |
| Configurazione immagini | `GET /configuration` (base URL e dimensioni, cache 7 giorni) |

Regole:

- **Fallback lingua (BR-15):** si chiama con la lingua della sorgente. Se `overview` o titolo sono vuoti, si ripete con `en-US` e si memorizzano **due** localizzazioni.
- **Id TVDB:** da `external_ids.tvdb_id` per le serie. `imdb_id` da `external_ids` (serie) o dal dettaglio (film).
- **Stand-up:** si cerca nelle `keywords` il termine "stand-up comedy" (id TMDB da verificare, SP-2) oppure il genere indicato da Watchmode.
- **URL immagini:** costruiti dal backend (`secure_base_url` + dimensione + `PosterPath`); suggerite `w342` per la griglia, `w500` per la scheda, `w780` per il backdrop.
- **Attribuzione:** logo e dicitura richiesta mostrati in UI (FR-MET-07).

### 5.3 OMDb (opzionale, FR-MET-04)

`GET https://www.omdbapi.com/?i={imdb_id}&apikey={key}` → `imdbRating`. Eseguito solo se la chiave è impostata, con limite giornaliero configurabile (il piano free di OMDb ha un tetto giornaliero), a bassa priorità e solo per titoli con `ImdbId`.

### 5.4 Sonarr (API v3, header `X-Api-Key`) — da verificare con `/swagger`

Base: `{url}/api/v3`.

| Uso | Chiamata |
|---|---|
| Test connessione, versione | `GET /system/status` |
| Opzioni del dialog | `GET /qualityprofile`, `GET /rootfolder`, `GET /tag` |
| Libreria (con statistiche per stagione) | `GET /series` |
| Singola serie | `GET /series/{id}`; per TVDB id `GET /series?tvdbId={id}` |
| Lookup prima dell'aggiunta | `GET /series/lookup?term=tvdb:{tvdbId}` |
| Aggiunta | `POST /series` |
| Coda di download | `GET /queue?page=1&pageSize=…&includeSeries=true` (paginata) |
| Ricerca | `POST /command` con `{ "name": "SeriesSearch", "seriesId": id }` |
| Eliminazione | `DELETE /series/{id}?deleteFiles={bool}&addImportExclusion={bool}` |

Corpo di `POST /series`: si parte dall'oggetto restituito dal lookup (contiene `title`, `titleSlug`, `images`, `seasons`, `tvdbId`, …) e si sovrascrivono i campi scelti dall'utente:

```json
{
  "tvdbId": 121361,
  "title": "…",
  "qualityProfileId": 4,
  "rootFolderPath": "/tv",
  "seriesType": "standard",
  "seasonFolder": true,
  "monitored": true,
  "monitorNewItems": "all",
  "tags": [1],
  "addOptions": {
    "monitor": "all",
    "searchForMissingEpisodes": true,
    "searchForCutoffUnmetEpisodes": false
  }
}
```

Valori di `addOptions.monitor` **(da verificare)**: `all`, `future`, `missing`, `existing`, `firstSeason`, `lastSeason`, `latestSeason`, `pilot`, `recent`, `none`. Valori di `seriesType`: `standard`, `daily`, `anime`.

### 5.5 Radarr (API v3, header `X-Api-Key`) — da verificare con `/swagger`

| Uso | Chiamata |
|---|---|
| Test connessione, versione | `GET /system/status` |
| Opzioni del dialog | `GET /qualityprofile`, `GET /rootfolder`, `GET /tag` |
| Libreria | `GET /movie` |
| Per TMDB id | `GET /movie?tmdbId={id}` |
| Lookup | `GET /movie/lookup?term=tmdb:{tmdbId}` |
| Aggiunta | `POST /movie` |
| Coda | `GET /queue?page=1&pageSize=…&includeMovie=true` |
| Ricerca | `POST /command` con `{ "name": "MoviesSearch", "movieIds": [id] }` |
| Eliminazione | `DELETE /movie/{id}?deleteFiles={bool}&addImportExclusion={bool}` |

Corpo di `POST /movie`:

```json
{
  "tmdbId": 603,
  "title": "…",
  "qualityProfileId": 4,
  "rootFolderPath": "/movies",
  "monitored": true,
  "minimumAvailability": "released",
  "tags": [],
  "addOptions": { "monitor": "movieOnly", "searchForMovie": true }
}
```

`minimumAvailability`: `announced`, `inCinemas`, `released`.

### 5.6 Interfaccia `IArrClient`

Un'unica astrazione con due implementazioni (`SonarrClient`, `RadarrClient`) evita duplicazioni nei servizi:

```csharp
public interface IArrClient
{
    ArrKind Kind { get; }
    Task<ArrSystemInfo> GetSystemInfoAsync(CancellationToken ct);
    Task<ArrOptions> GetOptionsAsync(CancellationToken ct);              // profili, root folder, tag
    Task<IReadOnlyList<ArrItemSnapshot>> GetLibraryAsync(CancellationToken ct); // include statistiche e stagioni
    Task<IReadOnlyList<ArrQueueEntry>> GetQueueAsync(CancellationToken ct);
    Task<ArrItemSnapshot?> FindByExternalIdAsync(ArrExternalId id, CancellationToken ct);
    Task<ArrItemSnapshot> AddAsync(ArrAddRequest request, CancellationToken ct);
    Task SearchAsync(int arrId, CancellationToken ct);
    Task DeleteAsync(int arrId, bool deleteFiles, bool addImportExclusion, CancellationToken ct);
}
```

`ArrAddRequest` è un'unione discriminata: `SonarrAddRequest` e `RadarrAddRequest` con i campi specifici.

### 5.7 Webhook in ingresso

Configurati in Sonarr/Radarr (*Settings → Connect → Webhook*), metodo `POST`.

- URL: `{baseUrl}/api/v1/webhooks/sonarr?token={secret}` e `.../radarr?token={secret}`. Il token è generato dall'app (§12) e mostrato nelle impostazioni.
- Eventi da abilitare: Grab, Import/Download, Rename, Series/Movie Add, Series/Movie Delete, Episode/Movie File Delete.
- Il backend **non si fida del payload per lo stato**: lo usa solo per identificare l'elemento (`series.id` / `movie.id`, `tvdbId` / `tmdbId`) e accoda un `ArrRefreshItemJob` che interroga l'istanza. Evento `Test`: risponde 200.
- Debounce: più eventi sullo stesso elemento entro 5 secondi producono un solo refresh.

---

## 6. Job e sincronizzazione

### 6.1 Elenco job

| Job | Trigger | Coda | Concorrenza | Retry |
|---|---|---|---|---|
| `CatalogIncrementalSyncJob(sourceId)` | cron per sorgente (default `0 4 * * *`) | sync | 1 per sorgente | gestito internamente |
| `CatalogFullSyncJob(sourceId)` | cron per sorgente (default `0 3 * * 0`) | sync | 1 per sorgente | gestito internamente |
| `CatalogResumeJob` | giornaliero (e dopo il reset mensile) | sync | 1 | — |
| `EnrichmentJob` | accodato dopo le sync + ogni ora | default | 1 | 3 con backoff |
| `MetadataRefreshJob` | giornaliero, lotti | default | 1 | 3 |
| `LeavingSyncJob(sourceId)` | giornaliero | sync | 1 per sorgente | 2 |
| `ArrSyncJob(kind)` | intervallo (default 15 min) | arr | 1 per kind | 2 |
| `ArrRefreshItemJob(kind, arrId)` | dopo add/delete/webhook | arr | per elemento | 3 |
| `RecoFeatureJob` | dopo enrichment / orario | ml | 1 (`DisableConcurrentExecution`) | 2 |
| `RecoEmbeddingJob` | dopo feature / manuale | ml | 1 | 2 |
| `RecoBuildJob` | voto (debounce 30 s), dopo embedding, notturno, manuale | ml | 1 | 2 |
| `RecoEvalJob` | manuale | ml | 1 | 1 |
| `RecoModelDownloadJob` | manuale (download o detect) | ml | 1 | 1 |

Coda `ml`: secondo `AddHangfireServer` con `WorkerCount = 1` e `Queues = ["ml"]`, così embedding/build lunghi non saturano `sync`/`arr`.

Registrazione:

- All'avvio e a ogni modifica delle impostazioni, un `ScheduleReconciler` legge `CatalogSources` e impostazioni e registra/aggiorna/rimuove i job ricorrenti Hangfire (`RecurringJob.AddOrUpdate` / `RemoveIfExists`) con `TimeZoneInfo` locale.
- Dato che gli schedule si ricreano dal DB applicativo, lo storage Hangfire contiene solo stato transitorio (code, retry): la perdita di `jobs.db` non fa perdere configurazione.
- I job sono classi con `ExecuteAsync(…, CancellationToken)` richiamate tramite `IBackgroundJobClient`; la logica sta nei servizi di `Core`, non nei job.
- `[DisableConcurrentExecution]` (o lock su `SyncRuns` in stato `Running` per `(Kind, CatalogSourceId)`) impedisce esecuzioni concorrenti (UC-10).

### 6.2 Budget crediti (`ICreditBudget`)

Parametri (impostazioni): `MonthlyQuota` (default 2500), `ReservePercent` (default 10), `MaxCreditsPerRun` (default 80), `WatchmodePlan` (`Free`/`Paid`).

```
used        = ultimo X-Account-Quota-Used noto (altrimenti somma ApiUsage del mese di fatturazione)
quota       = X-Account-Quota se noto, altrimenti MonthlyQuota
reserve     = ceil(quota * ReservePercent / 100)
spendable   = max(0, quota - used - reserve)          // per job schedulati
spendableManual = max(0, quota - used)                // per operazioni manuali

TryReserve(cost, isManual):
    return cost <= (isManual ? spendableManual : spendable)
        && runCredits + cost <= MaxCreditsPerRun
```

- La verifica avviene **prima di ogni chiamata** Watchmode; dopo la risposta si aggiorna `used` con l'header reale.
- Se `TryReserve` fallisce, la run passa a `Paused` salvando il checkpoint (BR-14).
- Il ciclo di fatturazione parte dal giorno di iscrizione, non dal primo del mese: l'impostazione `BillingDayOfMonth` (default 1) permette di allinearlo; in ogni caso gli header sono la fonte di verità.
- Proiezione a fine mese (FR-ADM-04): `used / giorniTrascorsi * giorniTotali`.

**Stima di consumo** (da confermare con SP-1): con `limit=250` una lettura completa di una sorgente richiede `ceil(titoli/250)` pagine, quindi l'ordine di grandezza è alcune decine di crediti per sorgente a ogni sync completa. Con IT+US, sync completa settimanale e incrementale giornaliera di 3 pagine per sorgente, il consumo mensile atteso è di poche centinaia di crediti, ben sotto i 2.500 (se ogni pagina costa 1 credito).

### 6.3 Sync incrementale (strategia `ListPagesStrategy`)

```
run = StartRun(CatalogIncremental, source)
pages = source.IncrementalPages
for page in 1..pages:
    if !budget.TryReserve(cost): run.Pause(checkpoint = page); return
    result = provider.List(source, sort: release_date_desc, page)
    Upsert(result.Entries, run)          // vedi 6.5
run.Succeed()
EnqueueEnrichment()
```

Limite noto (R-02): un vecchio titolo aggiunto a Netflix compare in cima solo se ha data di uscita recente; gli altri sono rilevati dalla sync completa. Con piano a pagamento (`WatchmodePlan = Paid`) si usa `ChangeFeedStrategy`: `changes/titles_sources_changed` per il paese e aggiornamento dei soli id coinvolti. La strategia è scelta da una factory in base al piano; l'interfaccia è `ICatalogSyncStrategy`.

### 6.4 Sync completa

```
run = StartRun(CatalogFull, source, checkpoint = resume?.checkpoint)
for page in (checkpoint.page ?? 1)..total_pages:
    if !budget.TryReserve(cost): run.Pause(checkpoint = { page, seenIds? }); return
    result = provider.List(source, sort: stabile, page)
    Upsert(result.Entries, run)
    run.Checkpoint(page + 1)
// lettura completata
unseen = Availabilities attive della sorgente con LastSeenAt < run.StartedAt
guard  = unseen.Count / attive.Count > MaxRemovalRatio (default 0.10)
if guard: run.Status = NeedsReview; return        // nessuna rimozione (FR-CAT-10)
foreach a in unseen:
    a.MissedFullScans++
    if a.MissedFullScans >= RemovalConfirmations (default 2):
        a.RemovedAt = now; Event(Removed)
// i titoli visti azzerano MissedFullScans in Upsert
if source.BaselineCompletedAt == null:
    marca come IsBaseline tutte le disponibilità create in questa run; source.BaselineCompletedAt = now
source.LastFullAt = now; source.LastFullTotalPages = total_pages
run.Succeed()
```

Note:

- **Ripresa:** il checkpoint contiene la pagina successiva. `LastSeenAt` aggiornato dagli upsert di tutte le run (anche incrementali) dopo `run.StartedAt` evita rimozioni spurie dei titoli visti nel frattempo.
- **Ordinamento stabile:** si usa un ordinamento che non dipenda dalla data di uscita né dalla popolarità (es. per titolo o per id), se supportato **(SP-1)**. In ogni caso la doppia conferma (BR-05) assorbe gli spostamenti di pagina.
- **Run interrotta:** se il container si ferma, al riavvio le run `Running` orfane sono marcate `Paused` con l'ultimo checkpoint.
- **Baseline:** finché non è completata, `AddedAt` resta null, nessun titolo è "novità" e le rimozioni non sono calcolate.

### 6.5 Upsert del catalogo (`CatalogUpserter`)

Per ogni voce della pagina, in transazioni brevi per pagina:

1. Cerca il `Title` per `WatchmodeId`, poi per `(TmdbType, TmdbId)`. Se non esiste, lo crea con `MetadataStatus = Pending` (o `Unmapped` se non ha `tmdb_id` e non si trova via IMDb).
2. Cerca la `Availability (TitleId, SourceId)`:
   - assente → crea con `FirstSeenAt = LastSeenAt = now`; se la baseline è completata `AddedAt = now`, altrimenti `IsBaseline = true` e `AddedAt = null`; evento `Added`;
   - presente e attiva → `LastSeenAt = now`, `MissedFullScans = 0`;
   - presente e rimossa → `RemovedAt = null`, `AddedAt = now`, `IsBaseline = false`, `MissedFullScans = 0`; evento `Readded`.
3. Aggiorna i contatori della run.

Operazione idempotente: rieseguire la stessa pagina non cambia lo stato (salvo `LastSeenAt`).

### 6.6 Arricchimento (`EnrichmentJob`)

```
batch = Titles con MetadataStatus in (Pending, Failed con attempts < 5) ORDER BY CreatedAt LIMIT 100
foreach t in batch (parallelismo limitato, es. 4):
    details = tmdb.Get(t.TmdbType, t.TmdbId, lang = lingua della sorgente)
    if overview/name vuoti: details_en = tmdb.Get(..., "en-US")
    salva Title (anno, durata/stagioni, voti, id, path immagini, categoria), localizzazioni, generi
    t.MetadataStatus = Ready
on error: MetadataAttempts++, MetadataStatus = Failed
```

- La lingua è per sorgente: se un titolo è in più sorgenti con lingue diverse, si memorizza una localizzazione per ciascuna lingua richiesta.
- `MetadataRefreshJob`: ogni giorno rinfresca un lotto di titoli con `MetadataFetchedAt` più vecchio di 30 giorni e disponibilità attiva (FR-MET-05).
- Fallback di mappatura: `tmdb_id` assente e `ImdbId` presente → `/find`; altrimenti `Unmapped` (FR-CAT-12).

### 6.7 Sync libreria Sonarr/Radarr (`ArrSyncJob`)

```
snapshot = client.GetLibrary()   // serie con statistiche per stagione, oppure film
queue    = client.GetQueue()     // tutte le pagine
foreach item in snapshot:
    state = ArrStatusCalculator.Calculate(item, queue)   // §7.1
    upsert ArrLibraryItem (+ ArrSeasonStates)
    TitleId = ArrMatcher.Match(item, Titles)             // §7.2
delete da cache gli elementi non più presenti nello snapshot
```

- In caso di errore di rete/credenziali l'esecuzione fallisce **senza toccare la cache** (FR-ST-07); si registra l'errore in `SyncRuns`.
- Dopo il successo della sync si ricalcolano i match per i titoli arrivati dopo (`TitleId` null).

### 6.8 Sync delle scadenze (`LeavingSyncJob`, FR-CAT-11)

Chiama l'endpoint leaving dates per il paese (free: massimo 10 elementi). Per ogni voce risolve il titolo tramite id Watchmode, imposta `LeavingDate` e registra `LeavingAnnounced`; per i titoli che avevano una data e non risultano più, registra `LeavingCleared`. Su piano free il job è a basso costo e opzionale (impostazione).

---

## 7. Logica di dominio

Le regole sono funzioni pure in `Core/Domain/Rules`, coperte da unit test.

### 7.1 Calcolo dello stato libreria (BR-09, BR-10)

```csharp
public static ArrState ForMovie(ArrMovieSnapshot m, QueueIndex q) =>
    q.HasMovie(m.ArrId) ? ArrState.Downloading :
    m.HasFile            ? ArrState.Downloaded  :
                           ArrState.Missing;

public static ArrState ForSeason(SeasonStats s, bool inQueue) =>
    inQueue                           ? ArrState.Downloading :
    s.EpisodeCount == 0               ? ArrState.Missing :
    s.EpisodeFileCount >= s.EpisodeCount ? ArrState.Downloaded :
    s.EpisodeFileCount > 0            ? ArrState.Partial :
                                        ArrState.Missing;

public static ArrState ForSeries(IReadOnlyList<SeasonState> seasons, bool anyInQueue)
{
    if (anyInQueue) return ArrState.Downloading;
    var monitored = seasons.Where(s => s.Monitored && s.EpisodeCount > 0).ToList();
    if (monitored.Count == 0) return ArrState.Missing;
    if (monitored.All(s => s.State == ArrState.Downloaded)) return ArrState.Downloaded;
    if (monitored.Any(s => s.EpisodeFileCount > 0))         return ArrState.Partial;
    return ArrState.Missing;
}
```

- `EpisodeCount` è il numero di episodi attesi secondo le statistiche di Sonarr (episodi già andati in onda e monitorati). La semantica esatta va **verificata sulle istanze (SP-3)** e, se diversa, la funzione si adatta usando `TotalEpisodeCount`.
- La percentuale di download di una serie è la media pesata sulla `size/sizeleft` degli elementi in coda.

### 7.2 Match libreria ↔ catalogo (`ArrMatcher`, BR-08)

| Kind | Chiave primaria | Fallback |
|---|---|---|
| Radarr | `Titles.TmdbId` con `TmdbType = movie` | `ImdbId` |
| Sonarr | `Titles.TvdbId` | `Titles.TmdbId` con `TmdbType = tv`, poi `ImdbId` |

### 7.3 Capacità di azione di un titolo

```
kind      = (MediaType == Series) ? Sonarr : Radarr
requiredId= (kind == Sonarr) ? TvdbId : TmdbId
canAdd    = istanza configurata && !inLibreria && requiredId != null
canDelete = inLibreria
addBlockedReason ∈ { NotConfigured, MissingTvdbId, MissingTmdbId, AlreadyInLibrary }
```

### 7.4 Flusso di aggiunta (`ArrAddService`)

1. Carica il titolo; determina `kind` e l'id richiesto; se manca → errore `ARR_ID_UNMAPPED` (422).
2. `FindByExternalId` sull'istanza; se esiste → `ARR_ALREADY_EXISTS` (409), aggiorna la cache e restituisce lo stato.
3. Lookup sull'istanza; se vuoto → `ARR_REJECTED`.
4. Costruisce la richiesta partendo dal lookup e applicando le opzioni; esegue `POST`.
5. Se `searchNow` e l'istanza non lo gestisce già con `addOptions`, accoda `SearchAsync`.
6. Scrive `ArrAuditLog` e accoda `ArrRefreshItemJob` (oppure aggiorna subito la cache con la risposta).
7. Restituisce lo stato aggiornato.

Gli errori di validazione restituiti da Sonarr/Radarr (400 con array di `errorMessage`) sono mappati in `ARR_REJECTED` con i messaggi originali nel campo `errors`.

### 7.5 Flusso di eliminazione (`ArrDeleteService`)

1. Trova `ArrLibraryItem` per il titolo (o per `kind` + `arrId`).
2. `DELETE` con `deleteFiles` e `addImportExclusion` come scelti.
3. Rimuove dalla cache l'elemento, scrive l'audit, restituisce `NotInLibrary`.
4. In caso di errore nessuna modifica locale (BR-13); catalogo e preferiti non toccati.

---

## 8. Contratti API

Prefisso `/api/v1`. JSON `camelCase`. Paginazione con `page` (da 1) e `pageSize` (default 48, max 100). Gli errori seguono RFC 7807 con le estensioni `code` ed `errors` (§16). La specifica completa è generata come OpenAPI su `/openapi/v1.json` e consultabile su `/docs`.

### 8.1 Catalogo

**`GET /catalog/sources`** — sorgenti abilitate per il selettore.

```json
[
  { "id": 1, "providerKey": "netflix", "providerName": "Netflix",
    "countryCode": "IT", "metadataLanguage": "it-IT",
    "baselineCompleted": true, "lastFullSyncAt": "2026-11-01T03:12:44Z",
    "dataStale": false }
]
```

**`GET /catalog/sources/{sourceId}/titles`** — griglia/ricerca.

Query: `q`, `view` (`all`|`new`|`leaving`|`removed`), `type` (multi: `movie`,`series`,`standup`), `genre` (multi, id), `yearFrom`, `yearTo`, `arrState` (multi: `notInLibrary`,`missing`,`downloading`,`downloaded`,`partial`), `includeRemoved` (bool), `minRating`, `sort` (`relevance`|`added`|`year`|`rating`|`title`), `dir` (`asc`|`desc`), `page`, `pageSize`.

I preferiti non si filtrano sul catalogo: usare `GET /favorites` e la route UI `/list`. Un eventuale flag `favorite` sulla query catalogo è ignorato.

```json
{
  "page": 1, "pageSize": 48, "total": 1832,
  "items": [
    {
      "id": 101,
      "name": "Esempio",
      "originalTitle": "Example",
      "year": 2024,
      "category": "series",
      "posterUrl": "https://image.tmdb.org/t/p/w342/abc.jpg",
      "genres": [{ "id": 18, "name": "Dramma" }],
      "ratingTmdb": 7.8,
      "ratingImdb": null,
      "isFavorite": true,
      "rating": "like",
      "availability": { "addedAt": "2026-10-28T04:00:10Z", "removedAt": null, "leavingDate": null, "isNew": true },
      "arr": { "kind": "sonarr", "state": "downloading", "monitored": true, "progress": 42.5 }
    }
  ]
}
```

**`GET /catalog/sources/{sourceId}/genres`** — generi presenti nella sorgente, con conteggio.

**`GET /catalog/sources/{sourceId}/featured`** (C) — titolo in evidenza per l'hero.

### 8.2 Titolo

**`GET /titles/{id}?sourceId=1`** — scheda completa.

```json
{
  "id": 101,
  "name": "Esempio", "originalTitle": "Example", "year": 2024,
  "category": "series", "overview": "…",
  "runtimeMinutes": null, "seasonCount": 3, "episodeCount": 24,
  "genres": [{ "id": 18, "name": "Dramma" }],
  "posterUrl": "https://image.tmdb.org/t/p/w500/abc.jpg",
  "backdropUrl": "https://image.tmdb.org/t/p/w780/def.jpg",
  "ratings": { "tmdb": 7.8, "tmdbVotes": 1520, "imdb": null },
  "links": { "tmdb": "https://www.themoviedb.org/tv/987654", "imdb": "https://www.imdb.com/title/tt1234567/", "tvdb": null },
  "isFavorite": true,
  "rating": "like",
  "metadataStatus": "ready",
  "availability": [
    { "sourceId": 1, "countryCode": "IT", "addedAt": "2026-10-28T04:00:10Z", "removedAt": null, "leavingDate": null }
  ],
  "arr": {
    "kind": "sonarr", "arrId": 55,
    "state": "partial", "monitored": true, "progress": null,
    "sizeOnDisk": 18253611008, "path": "/tv/Esempio",
    "syncedAt": "2026-11-04T08:15:00Z",
    "seasons": [
      { "number": 1, "monitored": true, "state": "downloaded", "episodeFileCount": 8, "episodeCount": 8, "totalEpisodeCount": 8 },
      { "number": 2, "monitored": true, "state": "partial", "episodeFileCount": 3, "episodeCount": 8, "totalEpisodeCount": 8 }
    ],
    "canAdd": false, "canDelete": true, "addBlockedReason": null,
    "externalUrl": null
  }
}
```

Per un titolo non in libreria `arr` è `{ "kind": "radarr", "state": "notInLibrary", "canAdd": true, "canDelete": false, "addBlockedReason": null }`; senza istanza configurata `canAdd` è `false` e `addBlockedReason` è `notConfigured`.

### 8.3 Preferiti

| Metodo | Percorso | Note |
|---|---|---|
| `PUT` | `/favorites/{titleId}` | Idempotente. 204. |
| `DELETE` | `/favorites/{titleId}` | Idempotente. 204. |

La lista dei preferiti si ottiene con `GET /catalog/sources/{id}/titles?favorite=true` (con `includeRemoved=true` per mostrare anche i non più disponibili). Nei risultati, i preferiti non presenti nella sorgente hanno `availability: null`.

### 8.4 Sonarr/Radarr

**`GET /arr/options?kind=sonarr`** — dati per il dialog.

```json
{
  "kind": "sonarr",
  "configured": true, "reachable": true,
  "qualityProfiles": [{ "id": 4, "name": "HD-1080p" }],
  "rootFolders": [{ "path": "/tv", "freeSpace": 812345678901 }],
  "tags": [{ "id": 1, "label": "netflix" }],
  "defaults": { "qualityProfileId": 4, "rootFolderPath": "/tv" },
  "monitorOptions": ["all","future","missing","existing","firstSeason","lastSeason","latestSeason","pilot","recent","none"],
  "seriesTypes": ["standard","daily","anime"]
}
```

Per Radarr: `monitorOptions` assente, `minimumAvailabilityOptions: ["announced","inCinemas","released"]`. Risposta in cache lato server per 60 secondi.

**`POST /titles/{titleId}/arr`** — aggiunta (UC-06).

Richiesta Sonarr:

```json
{
  "qualityProfileId": 4, "rootFolderPath": "/tv",
  "monitor": "all", "monitorNewItems": "all",
  "seriesType": "standard", "seasonFolder": true,
  "tags": [1], "searchNow": true
}
```

Richiesta Radarr:

```json
{
  "qualityProfileId": 4, "rootFolderPath": "/movies",
  "monitored": true, "minimumAvailability": "released",
  "tags": [], "searchNow": true
}
```

Risposta `201` con lo stesso oggetto `arr` della scheda. Errori: `422 ARR_ID_UNMAPPED`, `409 ARR_ALREADY_EXISTS`, `424 ARR_REJECTED` (con `errors`), `503 ARR_NOT_CONFIGURED` / `ARR_UNREACHABLE`, `502 ARR_UNAUTHORIZED`.

**`DELETE /titles/{titleId}/arr?deleteFiles=false&addImportExclusion=false`** — eliminazione (UC-08). Risposta `200` con `arr` aggiornato (`notInLibrary`). La conferma è responsabilità della UI; l'API richiede i due parametri **espliciti** (se assenti, 400) per evitare cancellazioni accidentali via chiamate dirette.

**`POST /titles/{titleId}/arr/search`** — lancia la ricerca (S). 202.

**`POST /titles/{titleId}/arr/refresh`** — rilegge subito lo stato dall'istanza. 200 con `arr`.

### 8.5 Impostazioni

**`GET /settings`** — i segreti non sono restituiti.

```json
{
  "watchmode": { "apiKey": { "isSet": true }, "plan": "free",
                 "quota": 2500, "reservePercent": 10, "maxCreditsPerRun": 80 },
  "tmdb":      { "token": { "isSet": true } },
  "omdb":      { "apiKey": { "isSet": false } },
  "sonarr":    { "url": "http://sonarr:8989", "apiKey": { "isSet": true },
                 "defaults": { "qualityProfileId": 4, "rootFolderPath": "/tv" },
                 "syncIntervalMinutes": 15 },
  "radarr":    { "url": "http://radarr:7878", "apiKey": { "isSet": true },
                 "defaults": { "qualityProfileId": 6, "rootFolderPath": "/movies" },
                 "syncIntervalMinutes": 15 },
  "webhooks":  { "token": { "isSet": true } },
  "catalog":   { "newWindowDays": 14, "leavingWindowDays": 30, "removedWindowDays": 30,
                 "removalConfirmations": 2, "maxRemovalRatio": 0.10 },
  "ui":        { "language": "en" },
  "reco":      { "enabled": true, "useImplicit": true, "excludeInLibrary": true,
                 "n0": 30, "weightLike": 1, "weightDislike": -1,
                 "weightFavorite": 0.5, "weightArrAdd": 0.6, "weightDownloaded": 0.4,
                 "mlMinRatings": 100, "eventsEnabled": false }
}
```

**`PUT /settings`** — aggiornamento parziale (JSON merge). Un segreto è modificato solo se presente nel corpo con un valore; `null` lo cancella.

**`POST /settings/test/{target}`** con `target` ∈ `watchmode|tmdb|omdb|sonarr|radarr` — verifica con i valori salvati, oppure con quelli nel corpo (per testare prima di salvare). Risposta: `{ "ok": true, "version": "4.0.x", "detail": "…" }` oppure `{ "ok": false, "code": "ARR_UNAUTHORIZED", "detail": "…" }`.

**`POST /settings/webhook-token/rotate`** — rigenera il token e lo restituisce una sola volta.

**CRUD sorgenti (S, FR-ADM-07):** `GET /admin/providers/available` (servizi dalla fonte), `POST /admin/providers`, `GET/POST/PUT/DELETE /admin/catalog-sources`.

### 8.6 Amministrazione e job

| Metodo | Percorso | Descrizione |
|---|---|---|
| `GET` | `/admin/jobs` | Elenco job con stato, ultima e prossima esecuzione. |
| `POST` | `/admin/jobs/{jobKey}/trigger` | Corpo `{ "sourceId": 1, "mode": "incremental" }`. `202` con `runId`; `409 SYNC_ALREADY_RUNNING`. |
| `POST` | `/admin/runs/{runId}/cancel` | Annulla una run (S). |
| `POST` | `/admin/runs/{runId}/resume` | Riprende una run in pausa (S). |
| `GET` | `/admin/runs?kind=&status=&page=` | Storico paginato. |
| `GET` | `/admin/runs/{runId}` | Dettaglio con statistiche. |
| `GET` | `/admin/quota` | Crediti. |
| `GET` | `/admin/titles/problems?kind=unmapped|failed` | Titoli non mappati / arricchimento fallito. |
| `POST` | `/admin/titles/{id}/retry-metadata` | Riprova l'arricchimento. |
| `GET` | `/admin/arr-audit` | Log eliminazioni/aggiunte (S). |
| `POST` | `/admin/recommendations/rebuild` | Accoda `RecoBuildJob`. `202` + `hangfireJobId`. |
| `POST` | `/admin/recommendations/embeddings/rebuild` | Query `all=true` opzionale. |
| `POST` | `/admin/recommendations/model/download` | Download modello ONNX. |
| `POST` | `/admin/recommendations/model/detect` | Rileva file in `/config/models`. |
| `POST` | `/admin/recommendations/evaluate` | Accoda `RecoEvalJob`. |
| `GET` | `/admin/recommendations/evaluations/latest` | Ultimo report leave-one-out. |
| `POST` | `/admin/recommendations/reset` | Query obbligatoria `includeRatings=true\|false`. |

### 8.5bis Ratings e raccomandazioni

| Metodo | Percorso | Descrizione |
|---|---|---|
| `PUT` / `DELETE` | `/titles/{id}/rating` | Imposta o rimuove Like/Dislike. |
| `GET` | `/ratings` | Elenco voti (pagina). |
| `GET` | `/ratings/export` | Export JSON. |
| `POST` | `/ratings/import` | Import; id TMDB sconosciuti → `unknown`. |
| `POST` | `/ratings/onboarding` | Batch voto da `/taste`. |
| `GET` | `/recommendations` | *Per te* (scope `source`\|`enabled`, filtri, `diversify`, pagina). |
| `GET` | `/recommendations/status` | Stage, N_eff, modello, coverage, pool, version. |
| `GET` | `/recommendations/onboarding` | 24 titoli pool per onboarding. |
| `POST` | `/recommendations/events` | Log Shown/Opened/… se `reco.events.enabled`. |
| `GET` | `/titles/{id}/similar` | Simili sul pool abilitato. |

I DTO di elenco e dettaglio titolo espongono `rating` (`like`\|`dislike`\|null) e, dove applicabile, `reco` (score, reasons).

`GET /admin/quota`:

```json
{ "service": "watchmode", "quota": 2500, "used": 412, "reserve": 250,
  "spendable": 1838, "projectedMonthEnd": 1980, "lastUpdatedAt": "2026-11-04T04:05:00Z" }
```

### 8.7 Webhook e sistema

| Metodo | Percorso | Note |
|---|---|---|
| `POST` | `/webhooks/sonarr?token=…` | Risponde `202` (o `401` con token errato, confronto a tempo costante). |
| `POST` | `/webhooks/radarr?token=…` | idem |
| `GET` | `/system/status` | Versione, uptime, DB OK, istanze configurate, ora server. |
| `GET` | `/health`, `/health/ready` | Liveness e readiness (DB, Hangfire). Fuori da `/api/v1`. |
| `GET` | `/events` (C) | Server-Sent Events per aggiornamenti di stato (`arr-state-changed`, `run-updated`). |

---

## 9. Frontend

### 9.1 Struttura

```
src/PickArr.WebApp/src/
├─ app/            App.tsx, router.tsx, providers.tsx (QueryClient, i18n, toasts)
├─ api/            client.ts, types.ts, recommendations.ts, ratings.ts, …
├─ features/
│  ├─ catalog/     CatalogPage, TitleCard, FilterBar, QuickFilters
│  ├─ list/        MyListPage
│  ├─ title/       TitleDialog, SeasonTable, ArrStatusBadge, SimilarRow
│  ├─ arr/         AddToArrDialog, DeleteFromArrDialog
│  ├─ favorites/   FavoriteButton
│  ├─ ratings/     RatingButtons, MyRatingsPage
│  ├─ reco/        ForYouPage, TastePage, LearningProgress, ReasonChips
│  ├─ settings/    SettingsLayout, SettingsPage, CatalogSourcesPage, RecommendationsSettingsPage
│  └─ admin/       JobsPage (anche sotto /settings/jobs)
├─ components/     layout/ (Header, Footer, Attributions)
├─ i18n/           index.ts, locales/en.json   # inglese = sorgente di verità (AGENTS.md)
└─ main.tsx
```

> Nota: il path `frontend/` citato in bozze precedenti è obsoleto; la SPA vive in `src/PickArr.WebApp`.

### 9.2 Routing

| Percorso | Contenuto |
|---|---|
| `/` | Redirect a `/catalog` |
| `/catalog` | Griglia della sorgente selezionata; filtri (`q`, `view=all\|new\|leaving\|removed`, `type`, `genre`, …). Con reco attive e `view=all` senza `q`, riga *For you* (scope `source`, max 25). `view=favorites` reindirizza a `/list` |
| `/list` | Preferiti globali (cross-provider / cross-country); segnale implicito temporaneo per le reco |
| `/for-you` | Raccomandazioni sull’unione delle sorgenti abilitate (scope `enabled`) |
| `/taste` | Onboarding gusto |
| `/ratings` | Le mie valutazioni + export/import |
| `/settings` | Impostazioni generali |
| `/settings/catalogs` | Sorgenti di catalogo |
| `/settings/recommendations` | Stato/azioni raccomandazioni |
| `/settings/jobs` | Job e crediti (`/admin/jobs` reindirizza qui) |

La sorgente selezionata è salvata in `localStorage` (`pickarr.sourceId`) e usata dal catalogo (riga *For you* inclusa) e da *Per te* per preferenza/scheda.

### 9.3 Gestione dello stato

- **Dati dal server:** TanStack Query. Chiavi gerarchiche: `['titles', sourceId, filters]`, `['title', id, sourceId]`, `['arr-options', kind]`, `['settings']`, `['admin', …]`.
- **Filtri/ricerca:** fonte di verità = URL (`useSearchParams`); il campo di ricerca ha debounce 300 ms.
- **Griglia:** `useInfiniteQuery` + `@tanstack/react-virtual`. Le righe virtualizzate si calcolano dalla larghezza del contenitore (card 2:3). `placeholderData: keepPreviousData` per evitare sfarfallii al cambio filtro.
- **Preferiti:** mutazione con aggiornamento ottimistico su `titles` e `title`, rollback in caso di errore.
- **Voti:** `RatingButtons` con `onMutate` reale (non copiare il preferito as-is); dopo il voto si invalidano `titles`/`title`/`recommendations`/`ratings` e si rifà poll dello status finché cambia `version` (niente SSE obbligatorio).
- **Stato libreria:** `refetchInterval` di 30 s per la scheda aperta e `refetchOnWindowFocus` per la griglia; dopo add/delete si invalida `['title', id]` e `['titles']`. Se SSE è attivo, gli eventi invalidano le stesse chiavi e il polling si spegne.
- **Errori:** un interceptor trasforma i `problem+json` in errori tipizzati con `code`; la UI mappa il `code` su messaggi tradotti (`errors.ARR_UNREACHABLE`, …) e ricade su `detail`.

### 9.4 Componenti chiave

- **`TitleCard`**: poster con `loading="lazy"`, placeholder durante il caricamento, badge tipo e stato (colori: grigio *non in libreria*, giallo *mancante*, azzurro *in download* con progress, verde *scaricato*, arancione *parziale*), icona preferito.
- **`AddToArrDialog`**: legge `arr-options`, genera il form con react-hook-form + zod in base a `kind`, preseleziona i default, abilita "Aggiungi" solo a form valido.
- **`DeleteFromArrDialog`**: riepilogo (percorso, dimensione), due checkbox spente, pulsante `destructive` con testo che cambia se è attiva "elimina file"; richiede un click esplicito.
- **`SeasonTable`**: righe per stagione con icona monitorato, `episodeFileCount/episodeCount`, badge di stato.
- **`Attributions`**: logo TMDB + dicitura, link a Watchmode.com, nel piè di pagina fisso della UI.

### 9.5 i18n

- `react-i18next` con `fallbackLng: 'en'`; namespace unico `translation` più `errors`.
- `en.json` è la sorgente di verità per questo prodotto. Una eventuale lingua aggiuntiva ricade su `en` per chiavi mancanti.
- Formattazione con `Intl.DateTimeFormat` / `Intl.NumberFormat` secondo la lingua corrente.
- La lingua della UI è persistita nelle impostazioni del backend (`ui.language`) ed è indipendente dalla lingua dei metadati.

### 9.6 Tema e layout

- Solo tema scuro: `class="dark"` fissa su `<html>`, token colore in Tailwind (`surface`, `surface-2`, `accent`, `muted`).
- Larghezza minima 1280 px: sotto la soglia compare un avviso non bloccante.
- Griglia con colonne `repeat(auto-fill, minmax(180px, 1fr))`.
- Accessibilità di base: dialog con focus trap (Radix), alternative testuali sui poster, contrasto adeguato nei badge.

### 9.7 Build e sviluppo

- Dev: `vite` su :5173 con proxy `/api` e `/health` verso `http://localhost:5000`.
- Client API: `npm run api:generate` esegue `openapi-typescript http://localhost:5000/openapi/v1.json -o src/api/schema.d.ts`. Il file generato è committato; la CI verifica che sia aggiornato (§14).
- Produzione: `vite build` produce `dist/`, copiato in `wwwroot` dell'immagine Docker (nessun server separato).

---

## 10. Sicurezza

| Tema | Misura |
|---|---|
| Segreti a riposo | Chiavi Watchmode/TMDB/OMDb, token Sonarr/Radarr e token webhook sono cifrati con **Data Protection** (key ring in `/config/keys`, perso il quale i segreti vanno reimmessi). |
| Segreti in transito verso la UI | Mai restituiti: solo `isSet`. La rotazione del token webhook mostra il valore una volta sola. |
| Log | Nessun segreto nei log; redazione degli header `X-Api-Key` e `Authorization`, e dei parametri `apiKey`/`token` negli URL registrati. |
| Webhook | Token casuale ≥ 32 byte, confronto a tempo costante, rate limit (es. 30 req/min), nessun effetto oltre al refresh dell'elemento. |
| Input | Validazione di tutti i DTO; URL delle istanze: solo `http`/`https`, senza credenziali incorporate, normalizzati senza slash finale. |
| SSRF | Le uniche destinazioni in uscita configurabili sono gli URL delle istanze Sonarr/Radarr impostati dall'unico utente; nessun altro endpoint accetta URL arbitrari. |
| Azioni distruttive | L'API di eliminazione richiede parametri espliciti; la UI richiede conferma. Audit log. |
| Accesso | v1 **senza login** (D-02): documentare l'uso in LAN o dietro reverse proxy con autenticazione. Predisporre un middleware di autenticazione opzionale (password singola con cookie, abilitabile da variabile d'ambiente) come evoluzione. |
| Hangfire dashboard | **Disabilitata** di default (`Hangfire:Dashboard:Enabled=false`); la pagina Admin usa la propria API. Se abilitata, solo con autenticazione attiva. |
| Modelli ONNX | File sotto `/config/models` (o `PickArr__Models__Path`); download con verifica hash; self-test prima di marcare `Ready`. Nessun dato di gusto esce dal container. |
| Intestazioni | CSP con `img-src 'self' https://image.tmdb.org data:`, `X-Content-Type-Options`, `Referrer-Policy: no-referrer`, nessun CORS (stessa origine). |
| Container | Esecuzione come utente non root, filesystem scrivibile solo su `/config`. |
| Dipendenze | Aggiornamenti periodici, `dotnet list package --vulnerable` e `npm audit` in CI. |

---

## 11. Osservabilità

- **Logging:** Serilog, formato JSON su console (`docker logs`) e file rotante in `/config/logs` (7 giorni). Livello configurabile (`Logging__Level`). Proprietà di contesto: `RunId`, `SourceId`, `Job`, `Kind`.
- **Health:** `/health` (liveness, sempre 200 se il processo risponde) e `/health/ready` (verifica apertura DB e storage Hangfire).
- **Business metrics:** `SyncRuns` e `ApiUsage` sono la fonte per la pagina Admin; non serve uno stack di metriche esterno.
- **OpenTelemetry (C):** export OTLP opzionale con variabile d'ambiente, spento di default.
- **Diagnostica:** `GET /system/status` riporta versione, hash di build, dimensione dei DB, ultime run per tipo.

---

## 12. Configurazione

### 12.1 Variabili d'ambiente (bootstrap)

| Variabile | Default | Descrizione |
|---|---|---|
| `ASPNETCORE_URLS` | `http://+:8080` | Indirizzo di ascolto. |
| `PickArr__DataPath` | `/config` | Cartella dati (DB, chiavi, log). |
| `TZ` | `UTC` | Fuso per cron e log. |
| `PickArr__BaseUrl` | (vuoto) | URL pubblico per generare gli URL dei webhook. |
| `PickArr__PathBase` | (vuoto) | Prefisso se dietro reverse proxy con sottopercorso. |
| `Logging__Level` | `Information` | Livello di log. |
| `PickArr__Auth__Password` | (vuoto) | Abilita l'autenticazione opzionale. |
| `Hangfire__Dashboard__Enabled` | `false` | Dashboard Hangfire. |
| `PickArr__Models__Path` | `/config/models` | Directory modelli ONNX. |
| `PickArr__Models__DownloadBaseUrl` | (repo modello) | Mirror download. |
| `PickArr__Reco__MaxThreads` | `2` | Tetto thread ONNX (vince se più basso del setting). |

Le chiavi di §12.2, tranne lo stato operativo `catalog.initialImportStarted`, si dichiarano con nomi flat `SCREAMING_SNAKE` derivati dalla chiave (`.` e camelCase → `_`). Esempi: `WATCHMODE_API_KEY`, `WATCHMODE_QUOTA`, `TMDB_TOKEN`, `SONARR_URL`, `SONARR_API_KEY`, `RADARR_URL`, `RECO_ENABLED`, `RECO_WEIGHTS_LIKE`. Resta accettata la forma legacy `PickArr__Settings__` con ogni `.` sostituito da `__`. Se entrambe sono impostate, vince il nome flat. I default di Sonarr e Radarr si passano come `SONARR_DEFAULTS_QUALITY_PROFILE_ID` / `SONARR_DEFAULTS_ROOT_FOLDER_PATH` oppure come JSON in `SONARR_DEFAULTS` (e analoghi per Radarr).

Se la variabile è impostata e non vuota, quel valore è quello effettivo per tutta l'esecuzione: vince sul valore salvato e un aggiornamento dalla UI non lo sostituisce. I segreti dichiarati così non vengono scritti nel database e le API continuano a esporre solo `isSet`. Un numero o un booleano non interpretabile, o una chiave nested sconosciuta, impedisce l'avvio. I booleani sono `true` o `false`; i numeri usano il punto decimale. Senza la variabile resta il valore nel database (o il default).

### 12.2 Impostazioni applicative (tabella `Settings`)

Chiavi con il loro default: `watchmode.apiKey`, `watchmode.plan=free`, `watchmode.quota=2500`, `watchmode.reservePercent=10`, `watchmode.maxCreditsPerRun=80`, `watchmode.rateLimitPerSecond=1`, `watchmode.billingDay=1`, `tmdb.token`, `omdb.apiKey`, `omdb.dailyLimit=900`, `sonarr.url`, `sonarr.apiKey`, `sonarr.defaults`, `sonarr.syncIntervalMinutes=15`, `radarr.*` (come Sonarr), `webhooks.token`, `catalog.newWindowDays=14`, `catalog.leavingWindowDays=30`, `catalog.removedWindowDays=30`, `catalog.removalConfirmations=2`, `catalog.maxRemovalRatio=0.10`, `catalog.staleWarningDays=21`, `ui.language=en`, più chiavi reco: `reco.enabled=true`, `reco.useImplicit=true`, `reco.excludeInLibrary=true`, `reco.n0=30`, `reco.weights.*`, `reco.embeddings.*`, `reco.mmr.lambda=0.7`, `reco.explore.rate=0.1`, `reco.ml.minRatings=100`, `reco.events.enabled=false` (elenco completo in `raccomandazioni.md` §16).

Alla modifica delle impostazioni rilevanti per le schedulazioni, `SettingsService` emette un evento che attiva `ScheduleReconciler`.

---

## 13. Strategia di test

### 13.1 Piramide

| Livello | Cosa | Strumenti |
|---|---|---|
| **Unit — Core** | Regole BR (stati arr, novità, rimozione, budget), `CatalogUpserter` con repository in memoria, `LanguageFallback`, mapping dei tipi. Target di copertura elevato (≥ 85% su `Core`). | xUnit, Shouldly, NSubstitute |
| **Unit — Infrastructure** | Client HTTP contro **WireMock.Net** con fixture JSON: parsing, gestione 429/5xx, redazione segreti, scrittura `ApiUsage`. | WireMock.Net |
| **Integrazione API** | `WebApplicationFactory` con SQLite su file temporaneo e WireMock al posto di Watchmode, TMDB, Sonarr, Radarr. Flussi: sync → enrichment → lista/ricerca → add → stato → delete. | xUnit, `WebApplicationFactory` |
| **Scenari di sync** | Prima sync completa (baseline), incrementale con nuove uscite, sync completa con rimozione a doppia conferma, guardia anti-anomalia, pausa/ripresa per budget, riavvio con run orfana, riscomparsa di un titolo. | integrazione |
| **Contratto** | Fixture registrate da risposte reali (anonimizzate) di Watchmode/TMDB/Sonarr/Radarr, in `tests/fixtures/`; test che verificano il parsing e rilevano deviazioni. | xUnit |
| **Frontend unit/component** | Form dei dialog (validazione, default), `ArrStatusBadge`, utilità URL e formattazione, fallback i18n; chiamate simulate con MSW. | Vitest, Testing Library, MSW |
| **E2E** (S) | Stack `docker compose` con WireMock: ricerca, aggiunta, stato, eliminazione, impostazioni. | Playwright |

### 13.2 Regole

- Nessun test chiama servizi reali; gli spike (§17) sono script/manuali separati e producono le fixture.
- Tempo e GUID via `IClock`/`IIdGenerator` per rendere deterministici i test di sync.
- Test di migrazione: da DB vuoto e da snapshot della versione precedente.
- La CI fallisce sotto la soglia di copertura (configurabile) e se i test sono instabili vengono messi in quarantena con ticket.

---

## 14. Build, packaging e CI/CD

### 14.1 Dockerfile (singola immagine)

Il Dockerfile in `src/PickArr.Api/Dockerfile` è quello eseguito dalla pipeline. In sintesi:

- `npm ci` e `dotnet restore` stanno su layer propri, prima dei sorgenti. Un cambiamento di codice non riscarica i pacchetti; un cambiamento solo del frontend non ripubblica l'API, e viceversa.
- La pubblicazione è framework-dependent (`--no-self-contained`) per `linux-x64` o `linux-arm64`, senza simboli di debug e senza un secondo runtime. Il frontend e `dotnet publish` girano sull'architettura del builder (`$BUILDPLATFORM`) per evitare l'emulazione QEMU; l'immagine finale è quella della piattaforma target.
- L'immagine di runtime resta `aspnet:10.0`: ONNX ha bisogno di `libgomp`, che le immagini chiseled non contengono. Non si installa `curl`. L'healthcheck usa un `wget` statico copiato da BusyBox.
- Lo stage `base` prepara `/config` di proprietà di `app` per la fast mode di Visual Studio e per i volumi nuovi. Un volume già esistente e di proprietà di root non viene riscritto.

L'utente non root e i permessi sul volume `/config` vanno verificati (SP-5); l'immagine `aspnet` usa l'utente `app`, quindi la cartella montata deve essere scrivibile da esso (o si imposta `user:` nel compose).

### 14.2 `docker-compose.yml`

```yaml
services:
  PickArr:
    image: ghcr.io/<owner>/pickarr:latest
    container_name: PickArr
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - TZ=Europe/Rome
      - PickArr__BaseUrl=http://PickArr:8080   # usato per generare gli URL dei webhook
      # WATCHMODE_API_KEY / TMDB_TOKEN / SONARR_URL / … (default DataPath=/config, URLS=:8080)
    volumes:
      - ./config:/config
```

### 14.3 Pipeline (`.github/workflows/ci.yml`)

Pull request, push sul branch predefinito e tag. Lint e test girano in parallelo; l'immagine viene costruita solo se sono verdi. Piattaforma pubblicata: `linux/amd64`.

| Job | Contenuto |
| --- | --- |
| Backend lint | `dotnet format --verify-no-changes`; report vulnerabilità NuGet non bloccante |
| Frontend lint | `npm run lint`, `npm run typecheck` |
| Backend test | `dotnet test` in Release; artefatto TRX |
| Frontend test | `npm test`, `npm run build` |
| Immagine | Build Dockerfile. Push su GHCR dal branch predefinito (`X.Y.Z.N` e `edge`) e dai tag SemVer (`X.Y.Z` e `latest`). |

Dettagli:

- **Versionamento:** un tag `vX.Y.Z` o `X.Y.Z` fissa la versione `X.Y.Z` (immagine `X.Y.Z` e `latest`). Le build successive usano `X.Y.Z.N`, dove N è il numero di commit dopo quel tag, fino al tag successivo. Senza tag la versione è `0.0.0.N`. Il branch predefinito pubblica anche `edge`. La stringa è `Version` e `InformationalVersion` dell'assembly; `/system/status` riporta la versione senza l'eventuale suffisso di commit. `CHANGELOG.md` aggiornato a ogni release.
- **Cache:** pacchetti NuGet e npm tramite le action di setup; layer Docker con la cache di GitHub Actions.
- **Registry:** GitHub Container Registry (`ghcr.io`), autenticazione con `GITHUB_TOKEN`. Nessun secret di registry da configurare.
- **Documentazione (FR/NFR-08):** l'OpenAPI è generata dal backend; i documenti in `docs/` restano versionati con il codice.
- **Immagine di test E2E (S):** job opzionale, non presente nella pipeline attuale, che avvia lo stack con WireMock ed esegue Playwright.

---

## 15. Estensione multi-servizio

### 15.1 Astrazione

Il modello (§4) è già multi-servizio: `Providers` → `CatalogSources (provider+paese)` → `Availabilities`. L'astrazione di accesso ai dati è:

```csharp
public interface ICatalogProvider
{
    string Type { get; }                                   // "watchmode"
    CatalogProviderCapabilities Capabilities { get; }      // SupportsChangeFeed, SupportsLeavingDates, MaxPageSize, ...

    Task<IReadOnlyList<ProviderInfo>> GetAvailableProvidersAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetCountriesAsync(string providerExternalId, CancellationToken ct);

    Task<CatalogPage> ListTitlesAsync(CatalogListRequest request, CancellationToken ct);
    Task<IReadOnlyList<LeavingEntry>> GetLeavingAsync(CatalogSourceRef source, CancellationToken ct);
    Task<IReadOnlyList<CatalogEntry>> GetChangedTitlesAsync(CatalogSourceRef source, DateOnly from, DateOnly to, CancellationToken ct);
}

public sealed record CatalogListRequest(
    string ProviderExternalId, string CountryCode,
    IReadOnlyList<CatalogEntryType> Types, CatalogSort Sort, int Page, int PageSize);

public sealed record CatalogPage(
    IReadOnlyList<CatalogEntry> Entries, int Page, int TotalPages, int CreditsUsed);

public sealed record CatalogEntry(
    string ExternalId, string Title, int? Year, CatalogEntryType Type,
    int? TmdbId, string? TmdbType, string? ImdbId);
```

- `WatchmodeCatalogProvider` implementa l'interfaccia ed è **parametrizzata dai dati**: l'id del servizio (`Providers.ExternalId`) e il paese arrivano dalla sorgente. Per questo aggiungere Amazon Prime o Disney+ non richiede codice.
- Il risolutore sceglie l'implementazione da `Providers.ProviderType` (`IProviderResolver`). Una futura fonte diversa è un'altra classe registrata in DI con un `Type` diverso.
- Il resto del sistema (upsert, enrichment, API, UI) non conosce Watchmode: lavora con `CatalogEntry`.

### 15.2 Procedura per aggiungere un servizio o un paese

1. **Admin → Servizi:** il backend chiama `GetAvailableProvidersAsync` (Watchmode `/sources/`, filtrato per paese) e propone l'elenco.
2. L'utente sceglie il servizio (es. Amazon Prime, Disney+) → creazione riga `Providers` con `ExternalId` = `source_id` Watchmode.
3. L'utente sceglie paese e lingua metadati → riga `CatalogSources` (abilitata).
4. `ScheduleReconciler` registra i job ricorrenti; viene accodata una sync completa (baseline).
5. A baseline completata la sorgente compare nel selettore.

Vincoli e controlli:

- Piano free: massimo 3 paesi (distinti) tra le sorgenti abilitate → avviso (FR-PRV-04); la verifica è sul numero di paesi, non di servizi.
- Prima di abilitare una nuova sorgente, il sistema stima i crediti necessari (`LastFullTotalPages` se noto, altrimenti un valore di default) e li confronta col budget disponibile.
- Gli id dei servizi vengono letti da `/sources/`, mai cablati nel codice (l'unico valore noto, Netflix = 203, è nel seed).

### 15.3 Dati condivisi tra sorgenti

Un titolo è unico e riusato tra sorgenti: i metadati TMDB si scaricano una volta (per lingua), la scheda mostra la disponibilità in tutte le sorgenti (FR-DET-07), i preferiti e lo stato libreria sono globali al titolo.

---

## 16. Gestione errori

### 16.1 Formato

```json
{
  "type": "https://PickArr.local/errors/arr-unreachable",
  "title": "Sonarr non raggiungibile",
  "status": 503,
  "detail": "Timeout durante la connessione a http://sonarr:8989.",
  "code": "ARR_UNREACHABLE",
  "traceId": "00-…",
  "errors": { "rootFolderPath": ["Path is not valid"] }
}
```

### 16.2 Codici

| Code | HTTP | Quando |
|---|---|---|
| `VALIDATION_ERROR` | 400 | DTO non valido. |
| `NOT_FOUND` | 404 | Titolo, sorgente o run inesistente. |
| `ARR_NOT_CONFIGURED` | 503 | URL o chiave dell'istanza mancanti. |
| `ARR_UNREACHABLE` | 503 | Timeout o errore di rete. |
| `ARR_UNAUTHORIZED` | 502 | Chiave rifiutata dall'istanza. |
| `ARR_ID_UNMAPPED` | 422 | TVDB/TMDB id mancante per il titolo. |
| `ARR_ALREADY_EXISTS` | 409 | Titolo già in libreria. |
| `ARR_REJECTED` | 424 | L'istanza ha rifiutato la richiesta (con `errors`). |
| `SYNC_ALREADY_RUNNING` | 409 | Job già in esecuzione. |
| `QUOTA_EXHAUSTED` | 429 | Budget crediti insufficiente per un'operazione manuale. |
| `UPSTREAM_ERROR` | 502 | Errore non classificato di un servizio esterno. |
| `INTERNAL_ERROR` | 500 | Errore imprevisto (dettagli solo nei log). |

### 16.3 Principi

- Un `ExceptionHandler` globale converte le eccezioni di dominio/infrastruttura in `ProblemDetails`; nessuno stack trace verso il client.
- I job non rilanciano eccezioni al framework: registrano l'esito in `SyncRuns` (`Failed`, con messaggio) e nei log, e lasciano i dati coerenti (transazioni per pagina, nessuna rimozione su run non completa).
- Le sync Sonarr/Radarr in errore non svuotano mai la cache (FR-ST-07).
- Gli errori transitori usano la resilienza HTTP (§5); quelli permanenti (401, 403, configurazione) non sono ritentati.

---

## 17. Rischi tecnici e spike iniziali

Gli spike sono attività brevi da fare **prima** dell'implementazione, con uscita scritta (note + fixture in `tests/fixtures/`).

| ID | Spike | Domande a cui rispondere |
|---|---|---|
| **SP-1** | **Watchmode** | Costo in crediti di `list-titles` (per pagina) e delle altre chiamate; nomi esatti dei parametri (`source_ids`, `regions`, `types`, `sort_by`, `page`, `limit`); `limit` massimo; formato di risposta; ordinamenti disponibili e stabilità (per la sync completa); numero di titoli Netflix IT/US per stimare le pagine; risposta a quota esaurita; endpoint e costo delle scadenze (anteprima free); se `releases` è disponibile sul piano free. |
| **SP-2** | **TMDB** | Copertura del `tvdb_id` per le serie Netflix IT/US; comportamento di `language` con traduzioni mancanti; classificazione stand-up (id keyword "stand-up comedy", genere Watchmode); casi di `tv_special`. |
| **SP-3** | **Sonarr/Radarr** | Versioni in uso; payload reali di `POST /series` e `POST /movie`; valori ammessi per `monitor` e `monitorNewItems`; semantica di `statistics.episodeCount` vs `totalEpisodeCount`; formato della coda e paginazione; parametri di eliminazione; payload e autenticazione dei webhook; presenza di un default esplicito per profilo/root folder (D-01). |
| **SP-4** | **Hangfire su SQLite** | Stabilità del pacchetto di storage con concorrenza e riavvii; in caso negativo, storage in memoria (gli schedule si ricreano) oppure altro storage. |
| **SP-5** | **SQLite e container** | Prestazioni di ricerca e griglia con 10k titoli e join con `ArrLibraryItems`; comportamento WAL su volumi di rete/Docker; permessi del volume `/config` con utente non root. |
| **SP-6** | **Frontend** | Fluidità della griglia virtualizzata; CSP con immagini TMDB; comportamento della route modale con filtri nell'URL. |
| **SP-R1** | **TMDB reco** | Il payload già richiesto basta per keyword, person id, crew, `created_by`? Costo chiamata extra `en-US`. |
| **SP-R2** | **ONNX** | Tokenizer .NET, pooling, vettori di riferimento, throughput x64/arm64, variante q8, RAM. |
| **SP-R3** | **Latency reco** | Rebuild pool+≤500 segnali &lt;10 s; GET recommendations p95 &lt;300 ms (join availability + filtro libreria); impatto MMR/similar. |
| **SP-R4** | **Taratura** | Con 30–50 voti: confronto livelli 0/1/2/(3) e defaults `a`/`b`/`N0`/`γ`/`K`/`m`. |
| **SP-R5** | **Licenze** | Termini TMDB su inferenza locale; licenza modello. Controllo manuale pre-rilascio (nota rischio, non task bloccante). |

Rischi residui rilevanti:

| ID | Rischio | Mitigazione |
|---|---|---|
| TR-01 | Variazioni non documentate delle API esterne. | Test di contratto con fixture; gestione tollerante dei campi sconosciuti (`JsonUnmappedMemberHandling.Skip`). |
| TR-02 | Esaurimento crediti per bug nel ciclo di paginazione. | Hard limit per run, controllo prima di ogni chiamata, test di scenario dedicati. |
| TR-03 | Corruzione o blocco SQLite. | WAL, backup prima delle migrazioni, volume dedicato, database dei job separato. |
| TR-04 | Perdita del key ring di Data Protection (segreti illeggibili). | Documentare che `/config/keys` fa parte del backup; la UI segnala segreti non decifrabili e chiede di reinserirli. |
| TR-05 | Cancellazione involontaria di file. | Opzioni disattivate di default, conferma esplicita, parametri obbligatori nell'API, audit log. |

---

## 18. Ordine di implementazione consigliato

1. **Fondamenta:** repository, soluzione .NET, Vite, Dockerfile, CI base, health, migrazioni e seed, `Settings` con cifratura dei segreti. Spike SP-1…SP-6 in parallelo.
2. **Catalogo:** client Watchmode, `ICatalogProvider`, `CatalogUpserter`, budget crediti, sync incrementale e completa con checkpoint, `SyncRuns`/`ApiUsage`.
3. **Metadati:** client TMDB, `EnrichmentJob`, localizzazioni con fallback, generi, id esterni.
4. **API di lettura e UI:** endpoint titoli/ricerca/dettaglio, griglia, filtri, scheda, preferiti, i18n, attribuzioni.
5. **Sonarr/Radarr:** `IArrClient`, `ArrSyncJob`, calcolo stati, match, badge e dettaglio per stagione.
6. **Azioni:** dialog di aggiunta, eliminazione con conferma, audit.
7. **Amministrazione:** pagina job, esecuzione manuale, crediti, titoli non mappati.
8. **Raccomandazioni R1–R4:** voto + prior + *Per te*; enrichment keyword/crediti; sparse/simili; ONNX/kNN/MMR; eval + L3 gated + event log (vedi `raccomandazioni.md`).
9. **Rifiniture:** webhook, scadenze, OMDb, filtri in URL, SSE, traduzione italiana, E2E.
10. **Nuovi servizi:** UI di gestione servizi/paesi e validazione con Amazon Prime/Disney+.

Ogni passo termina con test automatici e una pipeline verde.

---

## 19. Appendici

### A. Mappa requisiti → componenti

| Requisiti | Componenti principali |
|---|---|
| FR-CAT-* | `CatalogSyncService`, `CatalogUpserter`, `ICatalogProvider`, `ICreditBudget`, `ScheduleReconciler`, `SyncRuns`, `Availabilities` |
| FR-MET-* | `EnrichmentService`, `TmdbClient`, `OmdbClient`, `TitleLocalizations` |
| FR-UI/SRC/DET/FAV-* | `TitleQueryService`, endpoint `titles`/`favorites`, feature frontend `catalog`, `title`, `favorites` |
| FR-ADD/ST/DEL-* | `IArrClient`, `ArrAddService`, `ArrDeleteService`, `ArrLibrarySyncService`, `ArrStatusCalculator`, `ArrMatcher`, webhook |
| FR-SET/ADM-* | `SettingsService`, endpoint `settings`/`admin`, feature frontend `settings`, `admin` |
| FR-I18N-* | `i18n/` frontend |
| FR-PRV-* | `Providers`, `CatalogSources`, `IProviderResolver`, §15 |
| NFR-* | Docker, Serilog, health, test, CI, sicurezza (§10–§14) |

### B. Glossario tecnico

- **Checkpoint:** stato serializzato di una run (pagina successiva) che ne consente la ripresa.
- **Baseline:** primo import completo di una sorgente; i suoi titoli non sono "novità".
- **Strategia di sync:** implementazione di `ICatalogSyncStrategy` (`ListPagesStrategy` per il piano free, `ChangeFeedStrategy` per il piano a pagamento).
- **Run orfana:** `SyncRun` in stato `Running` dopo un riavvio; viene marcata `Paused`.
