# ARCHIVE — pickarr-mvp

> Documento append-only: non modificare sezioni già scritte.
> Stato vivo → `STATUS.md`
> Analisi tecnica → `ANALYSIS.md`
> Creato: 2026-11-04

---

## Indice delle sezioni

| ID | Data | Argomento/task | Motivazione | Destinazione |
| --- | --- | --- | --- | --- |
| A-001 | 2026-11-04 | TR-02 / T06–T07 spike Watchmode + Hangfire | Tranche chiusa; dettagli lunghi fuori STATUS | § sotto |
| A-002 | 2026-11-04 | TR-03 / T08–T12 Catalogo | Tranche chiusa | § sotto |
| A-003 | 2026-11-05 | TR-04 / T13–T14 Metadati | Tranche chiusa | § sotto |
| A-004 | 2026-11-05 | TR-09 / T24–T26 Leaving + packaging | Tranche chiusa; MVP implementativo | § sotto |
| A-005 | 2026-11-05 | TR-10 / T27 Verifica finale | PASS_WITH_DEFERRALS | § sotto + artifacts/T27-verifica-finale.md |

---

## A-001 — TR-02 Spike Watchmode e Hangfire (2026-11-04)

- T06: contratto Watchmode validato su OpenAPI 1.10.1 (`list-titles` 1 credito/regione, `limit`≤250, `title_asc`, leaving preview free). Artifact `artifacts/SP-1.md`, fixture in `backend/tests/fixtures/watchmode/`. Nessuna API key in sessione → `total_pages` Netflix IT/US stimati, non misurati live.
- T07: `Hangfire.Storage.SQLite` 0.4.3 stabile su `jobs.db`; fallback `Hangfire.InMemory`; `ScheduleReconcilerStub` dimostra re-apply cron. Artifact `artifacts/SP-4.md`. Decisione D-019.
- Test: `dotnet test` → 7 PASS.
- Finding: F-002 RESOLVED; F-001 ridotto al solo Arr (+ opz. live Netflix).

---

## A-002 — TR-03 Catalogo (2026-11-04)

- T08: `ICatalogProvider` + `WatchmodeCatalogProvider` (auth header, rate limit, resilience, ApiUsage handler/migrazione); WireMock su fixture SP-1.
- T09: `CreditBudgetRules`/`CreditBudgetService`, `SyncRuns` + pause/checkpoint, `WatchmodeQuotaUsageReader` (header o somma periodo billing).
- T10: entità `Titles`/`Availabilities`/`AvailabilityEvents`; `CatalogUpserter` idempotente; `CatalogIncrementalSync` ListPages + budget pause.
- T11: `CatalogFullSync` `title_asc`, baseline, MissedFullScans/RemovalConfirmations, guard `MaxRemovalRatio` → NeedsReview.
- T12: Hangfire jobs incr/full/resume + `ScheduleReconciler` via `IRecurringJobManager` (no static RecurringJob in host).
- Test: `dotnet test` → **40 PASS** (14 Core + 24 Infra + 2 Integration).

---

## A-003 — TR-04 Metadati (2026-11-05)

- T13: artifact `artifacts/SP-2.md`; fixture `backend/tests/fixtures/tmdb/`; regola stand-up D-020 (keyword 9715 / nome); TVDB da `external_ids`; fallback lingua BR-15 documentato. Nessun token live.
- T14: `IMetadataProvider`/`TmdbMetadataProvider`, `EnrichmentService`, `TitleLocalizations`/`Genres`/`TitleGenres`, image URL builder, `EnrichmentJob` + `MetadataRefreshJob` in `ScheduleReconciler`, ApiUsage service `tmdb`.
- Test: `dotnet test` → **51 PASS** (21 Core + 28 Infra + 2 Integration).

---

## A-004 — TR-09 Scadenze e packaging MVP (2026-11-05)

- T24: `LeavingSync` + `LeavingSyncJob` (cron `30 5 * * *` per source), admin list/trigger `catalog-leaving-{id}`; free plan non cancella date assenti dalla preview; paid/scadute → `LeavingCleared`; UI filtro Leaving + nota limite free; `leavingWindowDays` in query.
- T25: artifact `artifacts/SP-5.md`; seed 10k + Arr join; p95 lista/ricerca ~50 ms ≪ 300 ms; WAL in `DatabaseInitializer`; FTS5 non necessario.
- T26: README avvio/dev/Docker/CI; compose con build+healthcheck+volume; smoke `docker build` → `pickarr:local` `/health` **200**. Pipeline GitLab non eseguita su runner (YAML buildah presente).
- Test: `dotnet test` → **90 PASS** (35+45+10); frontend Vitest **8 PASS**; build OK.

---

## A-005 — TR-10 Verifica finale indipendente (2026-11-05)

- Esito: **PASS_WITH_DEFERRALS** su versione `pickarr:local` `2bac92ff0821` (parent HEAD `4869916ee25b…`, tree untracked).
- Revisore: subagente explore read-only; R-02…R-10 PASS; R-01 deferred CI remota; R-11 chiuso da questo rapporto.
- Comandi: backend **90 PASS**, frontend **8 PASS** + build OK, `/health` **200**.
- Nessun difetto bloccante; nessuna correzione applicata in T27.
- Rapporto completo: `artifacts/T27-verifica-finale.md`.
