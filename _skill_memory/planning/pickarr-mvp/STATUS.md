# pickarr-mvp STATUS

> Ultimo aggiornamento: 2026-11-05 (TR-10 chiusa — PASS_WITH_DEFERRALS)
> Branch: repo padre `idee` (pickarr sotto `todo/pickarr`, worktree non committato)
> Storico operativo: `ARCHIVE.md`

---

## 0. Quick Context

- Obiettivo: implementare Pickarr MVP (Netflix IT/US + TMDB + UI + Sonarr/Radarr + admin + Docker/CI); requisiti in ANALYSIS.md §1
- Stato dominio: **MVP chiuso** — TR-01…TR-10 completate (T27 PASS_WITH_DEFERRALS)
- Codebase: HEAD padre `4869916ee25b013223e302c12885c3b3d9458bd2`; codice Pickarr ancora untracked
- Snapshot locale: invariato post-verifica (nessuna correzione in T27); nessuno staged
- Tranche/task candidato: nessuno (MVP); opz. commit/push + CI remota
- Dettagli: `artifacts/T27-verifica-finale.md`
- Blocchi e findings critici: F-001..F-004 RESOLVED
- Input umano: commit non richiesto finché non chiesto
- Handoff: verifica finale registrata; rinvio esplicito pipeline GitLab remota
- Verifica finale: vedi §9

---

## 1. Roadmap

### TR-01 — Fondamenta

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T01 | Scaffold backend .NET 10 (slnx, Core/Infra/Api, test projects) | M | 🟢 | DONE | — | PLAN.md TR-01 T01; `dotnet build` OK |
| T02 | Scaffold frontend Vite+React+TS (semplificato) | M | 🟢 | DONE | — | PLAN.md TR-01 T02; `npm run build` OK |
| T03 | EF SQLite, migrazioni, seed Netflix IT/US, Settings + Data Protection | L | 🟡 | DONE | T01 | PLAN.md TR-01 T03; test seed+settings PASS |
| T04 | Health, Serilog, Dockerfile amd64, compose example | M | 🟢 | DONE | T01, T02 | PLAN.md TR-01 T04; `pickarr:local` `/health` 200 |
| T05 | GitLab CI lint/test + buildah package → registry | L | 🟡 | DONE | T01–T04 | PLAN.md TR-01 T05; `.gitlab-ci.yml` buildah; pipeline remota non ancora eseguita |

### TR-02 — Spike Watchmode e Hangfire

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T06 | SP-1 Watchmode: crediti, params, fixture | M | 🔴 | DONE | T01 (opz. parallelo post-T01) | artifacts/SP-1.md; fixtures watchmode; K-007/K-008→FACT |
| T07 | SP-4 Hangfire storage SQLite vs memory | M | 🟡 | DONE | T01 | artifacts/SP-4.md; HangfireBootstrap + 2 test PASS |

### TR-03 — Catalogo

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T08 | ICatalogProvider + Watchmode client + ApiUsage | L | 🟡 | DONE | T06 | PLAN.md TR-03 T08; WireMock 6+mapping PASS |
| T09 | ICreditBudget + SyncRuns + checkpoint pause/resume | M | 🟡 | DONE | T03, T08 | PLAN.md TR-03 T09; Core budget tests PASS |
| T10 | CatalogUpserter + sync incrementale | L | 🟡 | DONE | T09 | PLAN.md TR-03 T10; upsert+WireMock incr PASS |
| T11 | Sync completa: baseline, rimozioni, guard anomalia | L | 🔴 | DONE | T10 | PLAN.md TR-03 T11; NeedsReview + MissedFullScans |
| T12 | Hangfire jobs catalogo + ScheduleReconciler + ResumeJob | M | 🟡 | DONE | T07, T11 | PLAN.md TR-03 T12; IRecurringJobManager |

### TR-04 — Metadati

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T13 | SP-2 TMDB + fixture (TVDB, stand-up, lingue) | M | 🟡 | DONE | T08 | artifacts/SP-2.md; fixtures/tmdb |
| T14 | EnrichmentService + jobs enrichment/refresh + localizzazioni | L | 🟡 | DONE | T13, T10 | WireMock enrichment PASS; D-008 regola |

### TR-05 — API lettura e UI

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T15 | TitleQueryService + API titles/favorites + filtro Arr (V-08) | L | 🟡 | DONE | T14 | PLAN.md TR-05 T15; emptyReason arrNotConfigured |
| T16 | UI catalogo: griglia, filtri, dettaglio, preferiti, i18n en, attribuzioni | L | 🟡 | DONE | T02, T15 | PLAN.md TR-05 T16; Vitest 5 PASS; `npm run build` OK |
| T17 | SP-6 smoke UI (fluidità base; virtualizzazione opzionale) | S | 🟢 | DONE | T16 | artifacts/SP-6.md; virtualizzazione deferred |

### TR-06 — Stato Sonarr/Radarr

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T18 | SP-3 Sonarr/Radarr da docs ufficiali + fixture | M | 🟡 | DONE | T01 | artifacts/SP-3.md; fixtures sonarr/radarr |
| T19 | IArrClient + ArrSyncJob + StatusCalculator + Matcher | L | 🟡 | DONE | T18, T03 | WireMock sync; cache FR-ST-07; 71 backend PASS |
| T20 | UI badge/stato/stagioni + settings Arr + test connessione | M | 🟡 | DONE | T16, T19 | SeasonTable; test connection; defaults D-018 |

### TR-07 — Azioni Arr

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T21 | ArrAddService + dialog add + endpoint | L | 🟡 | DONE | T19, T20 | PLAN.md TR-07 T21; WireMock add + API 201/422/503 |
| T22 | ArrDeleteService + dialog delete + ArrAuditLog | M | 🔴 | DONE | T21 | PLAN.md TR-07 T22; audit + params obbligatori |

### TR-08 — Amministrazione

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T23 | Admin API/UI: jobs, trigger, runs, quota crediti | L | 🟡 | DONE | T12, T16 | PLAN.md TR-08 T23; 409 SYNC_ALREADY_RUNNING |

### TR-09 — Scadenze e chiusura packaging MVP

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T24 | LeavingSyncJob + UI In scadenza (nota limite free) | M | 🟡 | DONE | T08, T16 | LeavingSync + job/admin; free no false clear; UI nota |
| T25 | SP-5 smoke perf ricerca/griglia (o misura post-seed) | M | 🟡 | DONE | T15 | artifacts/SP-5.md; p95 list/search ~50 ms @10k |
| T26 | Harden Docker/README; verifica DoD pipeline+container | M | 🟢 | DONE | T05, T23, T24 | README+compose; `pickarr:local` `/health` 200; CI YAML presente (runner GitLab non eseguito qui) |

### TR-10 — Verifica finale indipendente

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T27 | Verifica finale indipendente | L | 🔴 | DONE | TR-01…TR-09 | PASS_WITH_DEFERRALS; artifacts/T27-verifica-finale.md; §9 |

---

## 2. Flussi — stato per area

| Flusso | Stato | Note sintetiche |
| --- | --- | --- |
| FLUSSO-01 Sync catalogo | ✅ DONE (MVP) | incr+full+budget+jobs; trigger manuale admin |
| FLUSSO-02 Arricchimento TMDB | ✅ DONE (MVP core) | Enrichment+refresh jobs; UI attribuzione in TR-05 |
| FLUSSO-03 Esplorazione UI | ✅ DONE (MVP) | griglia/filtri/dettaglio/preferiti + Arr add/delete |
| FLUSSO-04 Sync stato Arr | ✅ DONE (MVP) | periodico Hangfire; refresh manuale; webhook in evoluzione |
| FLUSSO-05 Add/Delete Arr | ✅ DONE (MVP) | add options Must; delete conferma + audit; catalogo intatto |
| FLUSSO-06 Admin job/crediti | ✅ DONE (MVP) | jobs list/trigger; runs; quota+proiezione; cancel/resume Should fuori |
| FLUSSO-07 Leaving dates | ✅ DONE (MVP) | free preview; nota UI; clear solo paid/scadute |

---

## 3. Findings aperti

| ID | Gravità | Descrizione sintetica | Task target | Stato |
| --- | --- | --- | --- | --- |
| F-001 | Alta | Contratto Arr validato su docs/OpenAPI pubblici (SP-3); live istanza opzionale | T18 | RESOLVED |
| F-002 | Media | Hangfire su SQLite community da validare | T07 | RESOLVED |
| F-003 | Media | Regola V-08 in API + empty-state Settings link in UI | T15–T16 | RESOLVED |
| F-004 | Bassa | Specifica CI discordante (buildx/multi-arch vs buildah/amd64) | T05 | RESOLVED |

---

## 4. Decisioni architetturali

| ID | Data | Decisione | Owner | Motivazione/alternative | Fonte approvazione |
| --- | --- | --- | --- | --- | --- |
| D-001 | 2026-11-04 | Dominio KB `pickarr-mvp`; scope solo MVP Netflix | utente | Q1–Q2 | chat 2026-11-04 |
| D-002 | 2026-11-04 | Seguire ordine specifica §18 | utente | Q3 | chat |
| D-003 | 2026-11-04 | Nome `Pickarr` / `Pickarr.*` | utente | Q5 | chat |
| D-004 | 2026-11-04 | Nessun login v1 | utente | D-02 funzionale; Q6 | chat |
| D-011 | 2026-11-04 | **Filtro catalogo**: Sonarr→serie, Radarr→film, nessuno→vuoto; stand-up per MediaType | utente | Q7 | chat |
| D-018 | 2026-11-04 | Default profilo/root Arr: impostati in settings; se assenti → primo elemento lista istanza | docs D-01 (adottato) | non contraddetto in Q1–Q27 | assessment + funzionale D-01 |
| D-006 | 2026-11-04 | OMDb fuori MVP | utente | Q8 | chat |
| D-007 | 2026-11-04 | Delete options off di default | utente | D-04; Q9 | chat |
| D-008 | 2026-11-04 | Classificazione stand-up dopo SP-2 | utente | D-06; Q10 | chat |
| D-020 | 2026-11-05 | Stand-up: `Category=StandUp` se keyword TMDB id **9715** o nome “stand-up comedy”; MediaType invariato | agente SP-2 | artifacts/SP-2.md; conferma live keyword opz. |
| D-014 | 2026-11-04 | Sezione In scadenza **in MVP** | utente | Q11; eleva FR-CAT-11 | chat |
| D-009 | 2026-11-04 | Target Sonarr/Radarr: ultime disponibili | utente | Q12 | chat |
| D-013 | 2026-11-04 | Hangfire: SQLite se ok, altrimenti memory | utente | Q13 | chat |
| D-010 | 2026-11-04 | Spike Arr da documentazione ufficiale (no swagger istanza) | utente | Q15 | chat |
| D-015 | 2026-11-04 | Solo Must (+ leaving); Could/Should restanti fuori; spike multi-tranche | utente | Q16,Q21–Q24 | chat |
| D-016 | 2026-11-04 | Frontend semplificabile nell'MVP | utente | Q17 | chat |
| D-012 | 2026-11-04 | CI GitLab + buildah; GitLab registry; solo linux/amd64 | utente | Q18–Q20 | chat |
| D-017 | 2026-11-04 | DoD iniziale: pipeline verde + Docker avviabile; KPI sì | utente | Q26–Q27 | chat |
| D-019 | 2026-11-04 | Hangfire default **SQLite** `jobs.db`; fallback Memory/InMemory | agente SP-4 | test restart + V-09 | artifacts/SP-4.md |

---

## 5. Activity log

| Data | Task | Azione | Esito | Evidenza |
| --- | --- | --- | --- | --- |
| 2026-11-04 | INIT | Creati ANALYSIS/STATUS/PLAN/ARCHIVE/BASELINE/AGENT_KB; Q1–Q27 incorporate | DONE | questo file |
| 2026-11-04 | T01–T05 | Completata TR-01 fondamenta (backend/frontend/DB/settings/Docker/CI) | DONE | `dotnet test` 5 PASS; `npm run build` OK; docker `pickarr:local` health 200 |
| 2026-11-04 | F-004 | CI allineata a buildah + amd64 in `.gitlab-ci.yml` | RESOLVED | file CI |
| 2026-11-04 | T06 | SP-1 Watchmode da OpenAPI 1.10.1 + fixture (no live key) | DONE | artifacts/SP-1.md; tests/fixtures/watchmode/ |
| 2026-11-04 | T07 | SP-4 Hangfire SQLite + reconciler stub + test restart | DONE | artifacts/SP-4.md; `dotnet test` 7 PASS |
| 2026-11-04 | F-002 | Storage Hangfire deciso: SQLite default | RESOLVED | D-019 |
| 2026-11-04 | T08 | ICatalogProvider + WatchmodeCatalogProvider + ApiUsage handler/migrazione + WireMock | DONE | `dotnet test` 20 PASS (2+16+2) |
| 2026-11-04 | T09 | ICreditBudget + SyncRuns + pause/checkpoint + billing day | DONE | Core 11 + Infra SyncRun tests; totale 30 PASS |
| 2026-11-04 | T10 | CatalogUpserter + CatalogIncrementalSync (ListPages) | DONE | upsert idempotente; incr WireMock |
| 2026-11-04 | T11 | CatalogFullSync: baseline, MissedFullScans, guard ratio NeedsReview | DONE | fake provider scenarios PASS |
| 2026-11-04 | T12 | Catalog jobs + ScheduleReconciler + CatalogResumeJob | DONE | Hangfire IRecurringJobManager; 40 PASS total |
| 2026-11-05 | T13 | SP-2 TMDB: fixture + regola stand-up/TVDB/lingue | DONE | artifacts/SP-2.md; fixtures/tmdb |
| 2026-11-05 | T14 | EnrichmentService + Tmdb client + localizzazioni/generi + jobs | DONE | WireMock; `dotnet test` 51 PASS |
| 2026-11-05 | T15 | TitleQueryService + API catalog/titles/favorites + V-08 + Favorites | DONE | `dotnet test` 62 PASS (26+33+3) |
| 2026-11-05 | T16 | UI catalogo/dettaglio/preferiti/settings + i18n en + attribuzioni + CSP | DONE | Vitest 5 PASS; `npm run build` OK |
| 2026-11-05 | T17 | SP-6 smoke UI; virtualizzazione non necessaria per MVP | DONE | artifacts/SP-6.md |
| 2026-11-05 | F-003 | V-08 riflessa in query + empty-state UX | RESOLVED | CatalogPage emptyArr |
| 2026-11-05 | T18 | SP-3 Sonarr/Radarr docs + fixture | DONE | artifacts/SP-3.md; K-009→FACT |
| 2026-11-05 | T19 | IArrClient + ArrLibrarySync + calculator/matcher + jobs | DONE | WireMock; FR-ST-07 cache intact |
| 2026-11-05 | T20 | UI seasons/refresh/test Arr + defaults settings | DONE | frontend build OK |
| 2026-11-05 | F-001 | Contratto Arr chiuso da SP-3 | RESOLVED | docs pubblici v3 |
| 2026-11-05 | T21 | ArrAddService + POST /titles/{id}/arr + AddToArrDialog | DONE | WireMock lookup/add; options dialog |
| 2026-11-05 | T22 | ArrDeleteService + DELETE params + DeleteFromArrDialog + ArrAuditLog | DONE | audit Add/Delete; Vitest delete warning |
| 2026-11-05 | T23 | Admin jobs/runs/quota API + JobsPage/QuotaPanel | DONE | AdminApiTests; Vitest QuotaPanel; build OK |
| 2026-11-05 | T24 | LeavingSync + job/admin + UI nota free | DONE | LeavingSyncTests 4 PASS; filtro leavingWindowDays |
| 2026-11-05 | T25 | SP-5 perf 10k titoli | DONE | artifacts/SP-5.md; p95 ~50 ms |
| 2026-11-05 | T26 | README/compose harden + docker health smoke | DONE | `pickarr:local` HTTP 200; CI runner non eseguito |
| 2026-11-05 | T27 | Verifica finale indipendente R-01…R-11 | DONE | PASS_WITH_DEFERRALS; 90+8 PASS; health 200; rinvio CI remota |

---

## 6. KPI

| KPI | Baseline | Target | Attuale | Comando misura |
| --- | --- | --- | --- | --- |
| Test unit Core | 0 | ≥ copertura regole BR critiche; mira ≥85% Core a fine MVP | 35 PASS | `dotnet test` Core.Tests |
| Test automatici totali | 0 | piramide minimo unit+integrazione sync/arr | 90 backend + 8 frontend PASS | `dotnet test`; `npm test` |
| p95 lista/ricerca 10k titoli | n/d | &lt; 300 ms (NFR-01) | ~50 ms list / ~50 ms search | SP-5 / T25 |
| Pipeline CI | assente | verde su main | YAML presente; non ancora run su GitLab | `.gitlab-ci.yml` |
| Container avviabile | assente | `/health` 200 | OK locale `pickarr:local` 2026-11-05 | docker build+run smoke |
| Crediti/mese stimati IT+US | n/d | &lt;&lt; 2500 con riserva | stima SP-1 ~decine–centinaia; live pages pending | SP-1 + Admin quota |

---

## 7. Checklist archiviazione

| Condizione | Verificata? |
| --- | --- |
| Activity log ha > 10 righe DONE | ☐ |
| Bootstrap/pacchetto supera budget orientativo | ☐ |
| ≥ 3 finding RESOLVED/ACCEPTED nella tabella §3 | ☐ |
| Una decisione nuova rende obsolete note precedenti | ☐ |

---

## 8. Tooling

| Aspetto | Convenzione |
| --- | --- |
| Branch strategy | feature per tranche opzionale; main greenfield |
| Commit convention | Conventional Commits `feat(pickarr): …` solo su richiesta utente |
| Test command | `dotnet test` (backend); `npm test` (frontend) |
| Build command | `dotnet publish`; `npm run build`; buildah per immagine |
| Lint command | `dotnet format --verify-no-changes`; `npm run lint` |
| CI | `.gitlab-ci.yml` + buildah → `$CI_REGISTRY_IMAGE` |

---

## 9. Verifica finale indipendente

| Campo | Valore |
| ----- | ------ |
| Task finale | T27 |
| Esito | **PASS_WITH_DEFERRALS** |
| Revisore e sessione | subagente explore `[T27 verify](393e4150-61ab-4275-9fc2-e20e780739fd)` + rievidenza comandi parent 2026-11-05 |
| Isolamento effettivo | Revisore read-only separato; nessuna correzione codice in T27 |
| Versione verificata | HEAD padre `4869916ee25b…`; `pickarr:local` `2bac92ff0821` / sha256 `2bac92ff0821…343b896` |
| Rapporto | `artifacts/T27-verifica-finale.md` |
| Rinvii/rischi approvati | (1) pipeline GitLab remota non eseguita; (2) Dockerfile senza `--platform` pin; (3) tree Pickarr untracked; (4) no live API keys in verifica |

> Storico tranche, log datati, finding risolti: → `ARCHIVE.md`
