# PLAN — pickarr-mvp

## TR-01 — Fondamenta

### T01 — Scaffold backend .NET 10

- Scope: `backend/PickArr.slnx`, `Directory.Packages.props`, `Directory.Build.props`, progetti `PickArr.Core`, `PickArr.Infrastructure`, `PickArr.Api`, test `PickArr.Core.Tests` / `Infrastructure.Tests` / `Integration.Tests`; `Program.cs` minimo
- Requisiti: ANALYSIS R-01; specifica §2–§3
- Acceptance: soluzione compila; test vuoto/verde; dipendenze `Api→Core/Infra`, `Infra→Core`, Core senza EF/HTTP
- Verifica: `dotnet build` / `dotnet test` in `backend/`
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: rimuovere `backend/` creato nel task

### T02 — Scaffold frontend semplificato

- Scope: `frontend/` Vite + React 19 + TS strict + Tailwind base + i18next stub `en`; proxy `/api`; page placeholder
- Requisiti: ANALYSIS R-01; V-05; D-016
- Acceptance: `npm run build` e `npm run typecheck` OK; nessuna dipendenza non necessaria (virtualizzazione/shadcn deferibili)
- Verifica: `npm ci && npm run build` in `frontend/`
- Contesto pertinente: D-016
- Assunzioni: nessuna
- Rollback: rimuovere `frontend/`

### T03 — Persistenza, seed, Settings cifrati

- Scope: DbContext, migrazioni, seed Provider netflix + CatalogSources IT/US, tabella Settings, Data Protection key ring path, SettingsService base
- Requisiti: R-01, R-10; FR-SET-01..09 Must; NFR-04/06; specifica §4, §10, §12
- Acceptance: migrate all'avvio; seed idempotente; segreti cifrati e API settings espone solo `isSet`
- Verifica: test integrazione DB temp; avvio Api crea `PickArr.db`
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: revert migrazioni/progetti toccati

### T04 — Health, logging, Docker amd64

- Scope: `/health`, `/health/ready`, Serilog, `docker/Dockerfile` (solo amd64), `docker-compose.example.yml`, copia SPA in wwwroot in publish
- Requisiti: R-01; NFR-05/07; V-06
- Acceptance: container locale risponde 200 su `/health`; volume `/config`
- Verifica: buildah/docker build + run smoke
- Contesto pertinente: D-012; F-004
- Assunzioni: nessuna
- Rollback: rimuovere docker/ e config host

### T05 — GitLab CI + buildah + registry

- Scope: `.gitlab-ci.yml` stages lint/test/package/release con **buildah** push su GitLab registry; no arm64
- Requisiti: R-01; NFR-08; V-06; D-012
- Acceptance: pipeline definisce job coerenti; Dockerfile buildabile da CI; F-004 risolto in CI
- Verifica: pipeline su MR/main (quando remoto disponibile) o validazione YAML + build locale equivalente
- Contesto pertinente: F-004
- Assunzioni: progetto GitLab esistente o creabile dall'utente
- Rollback: rimuovere/ripristinare `.gitlab-ci.yml`

## TR-02 — Spike Watchmode e Hangfire

### T06 — SP-1 Watchmode

- Scope: script/note spike + fixture JSON in `backend/tests/fixtures/watchmode/`; report costi/params
- Requisiti: R-02; D-05 docs; K-007/K-008
- Acceptance: documentati costo/pagina, params `list-titles`, sort, leaving endpoint, Netflix IT/US pages stimate; fixture anonimizzate
- Verifica: note in `docs/` o `_skill_memory/.../artifacts/SP-1.md` + fixture presenti; ANALYSIS §7 aggiornato
- Contesto pertinente: F-001
- Assunzioni: K-007, K-008 — controllo con API key utente
- Rollback: nessuna modifica runtime obbligatoria

### T07 — SP-4 Hangfire storage

- Scope: prova pacchetto SQLite community Hangfire; decisione wiring storage
- Requisiti: V-09; D-013
- Acceptance: scelta documentata (SQLite file `jobs.db` **oppure** memory); ScheduleReconciler ricrea cron da DB app in entrambi i casi
- Verifica: mini host Hangfire avvia/riavvia senza perdita schedule applicativi
- Contesto pertinente: F-002
- Assunzioni: nessuna
- Rollback: rimuovere wiring sperimentale

## TR-03 — Catalogo

### T08 — Provider Watchmode

- Scope: `ICatalogProvider`, `WatchmodeCatalogProvider`, HttpClient+resilience, ApiUsage handler
- Requisiti: R-02; FR-PRV-05 Must; specifica §5.1, §15
- Acceptance: list titles/sources/regions tipizzati; crediti da header registrati; test WireMock
- Verifica: `dotnet test` Infrastructure con WireMock
- Contesto pertinente: T06 fixture; F-001
- Assunzioni: risultati SP-1
- Rollback: rimuovere client/provider

### T09 — Budget e SyncRuns

- Scope: `ICreditBudget`, entità/SyncRuns, pause su quota, max per run
- Requisiti: FR-CAT-07, BR-14; FR-CAT-09
- Acceptance: TryReserve rispetta quota/riserva/max-run; manuale può usare riserva; test unit
- Verifica: Core.Tests budget scenarios
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: revert servizi/entità

### T10 — Upsert + sync incrementale

- Scope: `CatalogUpserter`, `CatalogIncrementalSync` / ListPagesStrategy
- Requisiti: FR-CAT-04, FR-CAT-06 (eventi add); UC-01 incr
- Acceptance: idempotente; nuovi titoli Pending; eventi Added post-baseline; test scenario
- Verifica: test integrazione sync incr con WireMock
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: revert sync incr

### T11 — Sync completa, baseline, rimozioni, guard

- Scope: full sync, MissedFullScans, BR-05, FR-CAT-08/10, checkpoint resume
- Requisiti: FR-CAT-05,08,10; BR-05; NFR-02
- Acceptance: baseline solo prima full OK; rimozione dopo N full; NeedsReview se ratio &gt; max; no rimozioni su fail; test scenari
- Verifica: Integration.Tests scenari tecnica §13
- Contesto pertinente: nessuno
- Assunzioni: K-008 sort stabile
- Rollback: revert full sync

### T12 — Job Hangfire catalogo

- Scope: job incr/full/resume, ScheduleReconciler, DisableConcurrentExecution / lock SyncRuns
- Requisiti: FR-ADM-02 (parte job); UC-10; specifica §6.1
- Acceptance: cron da CatalogSources; no doppia run; resume da Paused
- Verifica: test/manual smoke job trigger API stub
- Contesto pertinente: T07
- Assunzioni: nessuna
- Rollback: disregistrare job

## TR-04 — Metadati

### T13 — SP-2 TMDB

- Scope: fixture TMDB + note TVDB coverage, language fallback, stand-up keyword/genere
- Requisiti: R-03; D-008; D-06
- Acceptance: regola stand-up proposta documentata; fixture movie/tv
- Verifica: artifact SP-2 + ANALYSIS §7
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: n/a

### T14 — Enrichment

- Scope: `EnrichmentService`, EnrichmentJob, MetadataRefreshJob, TitleLocalizations, generi, image URL builder, attribuzioni dati
- Requisiti: FR-MET-01,02,05,06,07,08(S fuori—minimo show dati Watchmode già da upsert); BR-15
- Acceptance: fallback en; TVDB da external_ids; status Ready/Failed/Unmapped; test WireMock
- Verifica: `dotnet test`
- Contesto pertinente: T13
- Assunzioni: nessuna
- Rollback: revert enrichment

## TR-05 — API lettura e UI

### T15 — Query + API + filtro Arr — DONE 2026-11-05

- Scope: `TitleQueryService`, endpoint catalog/titles/genres/favorites; applicazione V-08 (MediaType in base a Sonarr/Radarr configurati); empty se nessuno
- Requisiti: R-04, R-05; FR-SRC-01..03 Must; FR-FAV Must; D-011
- Acceptance: filtri/ordinamenti Must; ricerca accent-insensitive; preferiti globali; senza Arr → lista vuota con contratto chiaro; test API
- Verifica: Integration.Tests API — PASS (`CatalogApiTests` + `TitleQueryServiceTests`); totale 62
- Contesto pertinente: F-003; D-011
- Note: risposta lista espone `emptyReason=arrNotConfigured` e `arrFilter`; `arr` stub `notInLibrary` fino a TR-06; migrazione Favorites + OriginalTitleNormalized
- Assunzioni: nessuna
- Rollback: revert endpoint/query

### T16 — UI catalogo MVP — DONE 2026-11-05

- Scope: features catalog/title/favorites/settings base; tema scuro desktop; i18n en; attribuzioni Watchmode/TMDB; empty-state Arr non configurato
- Requisiti: FR-UI-01..06,08 Must; FR-DET Must; FR-I18N-01..04; FR-MET-07; D-016
- Acceptance: griglia+filtri rapidi Must; dettaglio modale; preferiti toggle; nessun requisito Could
- Verifica: Vitest 5 PASS (`ArrStatusBadge`, `FavoriteButton`, format); `npm run build` OK; CSP middleware
- Contesto pertinente: F-003 → RESOLVED
- Note: Arr add/delete UI stub fino a TR-07; virtualizzazione non inclusa (D-016)
- Assunzioni: nessuna
- Rollback: revert UI features

### T17 — SP-6 smoke UI — DONE 2026-11-05

- Scope: verifica fluidità griglia base; CSP img TMDB; route dettaglio
- Requisiti: NFR-01 (qualitativo); specifica SP-6
- Acceptance: note SP-6; finding se virtualizzazione necessaria
- Verifica: artifacts/SP-6.md — virtualizzazione **deferred** (pageSize 48 + infinite scroll sufficiente)
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: n/a

## TR-06 — Stato Sonarr/Radarr

### T18 — SP-3 Arr docs ufficiali — DONE 2026-11-05

- Scope: fixture da documentazione ufficiale Sonarr/Radarr API v3; mapping monitor/delete/queue/stats
- Requisiti: R-06; D-010; D-009
- Acceptance: fixture + note versioni target “latest”; gap documentati in ANALYSIS §7
- Verifica: artifacts/SP-3.md; fixtures `tests/fixtures/{sonarr,radarr}/`
- Contesto pertinente: F-001 → RESOLVED; K-009 → FACT
- Assunzioni: K-009
- Rollback: n/a

### T19 — Client Arr + sync stato — DONE 2026-11-05

- Scope: `IArrClient`, SonarrClient, RadarrClient, ArrSyncJob, ArrStatusCalculator, ArrMatcher, cache ArrLibraryItems/Seasons
- Requisiti: FR-ST-01..07 Must (no ST-08); BR-09..11; specifica §7
- Acceptance: stati corretti; match id; errore rete non svuota cache; test unit+WireMock
- Verifica: `dotnet test` 71 PASS; ArrClientAndSyncTests
- Contesto pertinente: T18
- Note: `POST /titles/{id}/arr/refresh` sync kind-wide MVP; Add stub fino a T21
- Assunzioni: K-009
- Rollback: revert Arr sync

### T20 — UI stato + settings Arr — DONE 2026-11-05

- Scope: badge card/scheda, season table, settings Sonarr/Radarr, test connessione, defaults D-018
- Requisiti: FR-SET-02,03; FR-ST-02,03; R-05 empty-state coerente
- Acceptance: UI riflette cache; test connessione mostra versione; defaults selezionabili
- Verifica: `npm test` 5 PASS; `npm run build` OK
- Contesto pertinente: D-018
- Assunzioni: nessuna
- Rollback: revert UI settings/badge

## TR-07 — Azioni Arr

### T21 — Aggiunta Arr — DONE 2026-11-05

- Scope: ArrAddService, POST titles/{id}/arr, AddToArrDialog, options endpoint
- Requisiti: FR-ADD-01..09 Must; UC-06; BR-08
- Acceptance: dialog opzioni Must; blocchi id/config/già presente; errori Arr leggibili
- Verifica: `dotnet test` 79 PASS; ArrAddDeleteTests + ArrActionApiTests; Vitest 7; `npm run build` OK
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: revert add flow
- Note: lookup+POST Sonarr/Radarr; cache upsert immediato; FR-ADD-10 (ricorda ultime opzioni) fuori Must

### T22 — Eliminazione Arr — DONE 2026-11-05

- Scope: ArrDeleteService, DELETE con params obbligatori, DeleteDialog, ArrAuditLog
- Requisiti: FR-DEL-01..06 Must; D-007; BR-13
- Acceptance: conferma esplicita; options default off; catalogo/preferiti intatti; audit scritto
- Verifica: delete WireMock + audit; API 400 senza query params; UI warning deleteFiles
- Contesto pertinente: D-007
- Assunzioni: nessuna
- Rollback: revert delete flow

## TR-08 — Amministrazione

### T23 — Admin jobs e quota — DONE 2026-11-05

- Scope: endpoint admin jobs/runs/quota; UI JobsPage + QuotaPanel; trigger manuale incr/full
- Requisiti: FR-ADM-01..04 Must; UC-10; FR-CAT-09
- Acceptance: lista job con last/next/esito; storico; quota usati/riserva/proiezione; 409 se già running
- Verifica: `dotnet test` 85 PASS (AdminApiTests + QuotaProjection); Vitest 8; `npm run build` OK
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: revert admin
- Note: FR-ADM-05 cancel/resume e FR-ADM-06/07 (problems/CRUD sorgenti) restano Should fuori MVP Must

## TR-09 — Scadenze e packaging MVP

### T24 — Leaving dates

- Scope: LeavingSyncJob, campi LeavingDate/eventi, UI filtro/sezione In scadenza + nota limite free
- Requisiti: R-09; D-014; BR-07; D-07
- Acceptance: sezione nascosta o con nota se pochi dati; sync non rompe budget
- Verifica: WireMock leaving + UI smoke
- Contesto pertinente: D-014
- Assunzioni: endpoint da SP-1
- Rollback: revert leaving

### T25 — SP-5 performance smoke

- Scope: seed ~10k titoli (fixture/synth), misura p95 lista/ricerca; note WAL/volume
- Requisiti: NFR-01; KPI STATUS §6
- Acceptance: misura registrata; se &gt;300ms finding + opzione FTS5 documentata (non obbligatoria in-task se out of time: BLOCKED o finding)
- Verifica: script misura + output sintetico in artifact
- Contesto pertinente: nessuno
- Assunzioni: nessuna
- Rollback: rimuovere DB synth

### T26 — Harden DoD packaging

- Scope: README avvio, compose, allineamento CI, smoke finale container+health
- Requisiti: R-01; D-017; V-07
- Acceptance: istruzioni chiare; immagine amd64 avviabile; pipeline verde (o evidenza equivalente se runner assente: dichiarato)
- Verifica: comandi README eseguiti; log CI
- Contesto pertinente: D-017
- Assunzioni: accesso GitLab runners
- Rollback: n/a documentale

## TR-10 — Verifica finale indipendente

### T27 — Verifica finale indipendente

- Scope: intero perimetro MVP implementato, test, config, requisiti ANALYSIS §1 + §7
- Requisiti: ANALYSIS §1 R-01…R-11 e modifiche §7
- Acceptance: criteri obbligatori verificati su versione identificata; nessun difetto bloccante; rinvii espliciti; rapporto PASS in ARCHIVE e STATUS §9
- Verifica: protocollo skill — revisore in contesto separato, senza correzioni; se indisponibile handoff nuova sessione (task BLOCKED)
- Evidenze: rapporti tentativi in ARCHIVE.md; riepilogo in STATUS.md §9
