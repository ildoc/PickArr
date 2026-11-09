# test-coverage STATUS

> Ultimo aggiornamento: 2026-11-09
> Branch: `main`
> Storico operativo: `ARCHIVE.md`

---

## 0. Quick Context

- Obiettivo: aumentare copertura test sensata (unit/integration FE+BE) e accuratezza esistenti — ANALYSIS.md §1
- Stato dominio: **CHIUSO CON RINVII APPROVATI** — TR-06 / T14 **PASS_WITH_DEFERRALS**
- Codebase: HEAD `c576634b3809b2c1f598ce6c1ae114b37f17f49c` (main)
- Snapshot verificato: worktree digest `6fe33ca59776439a` (30 path; TR-01…TR-05 + KB uncommitted)
- Tranche/task candidato: nessuno (roadmap completa)
- Dettagli: PLAN.md TR-06 T14; `artifacts/T14-verifica-finale.md`
- Blocchi e findings critici: nessuno OPEN (F-001…F-012 RESOLVED)
- Input umano: nessuno
- Handoff: dominio chiuso; commit worktree a discrezione utente
- Verifica finale: §9 PASS_WITH_DEFERRALS (FLUSSO-07)

---

## 1. Roadmap

### TR-01 — Tooling coverage e igiene suite esistente (M)

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T01 | Abilitare misura coverage e report baseline | M | 🟢 | DONE | — | artifacts/T01-coverage-baseline.md |
| T02 | Isolare integration test con stato mutabile | M | 🟡 | DONE | T01 | Integration 29/29 PASS; catalog-api + arr-action-api |
| T03 | Accuratezza test unitari deboli esistenti | M | 🟡 | DONE | T01 | Delete/Jobs/Quota FE + TextNormalizer; Core 80 PASS, FE 41 PASS |

### TR-02 — Gap backend servizi critici (L)

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T04 | Test CatalogConfigurationService | L | 🟡 | DONE | T01 | CatalogConfigurationServiceTests 8 Fact PASS |
| T05 | Edge SeriesGuide (+ favoriti service) | M | 🟢 | DONE | T01 | SeriesGuide + FavoriteServiceTests PASS |
| T06 | Admin trigger invalidi e quota | M | 🟢 | DONE | T01 | AdminJob/Quota Infra + AdminTriggerValidation API 31 Integration PASS |

### TR-03 — Gap API integration (L)

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T07 | Titles seasons/404/similar API | L | 🟡 | DONE | T02, T05 | TitleDetailApiTests 5 PASS |
| T08 | Settings catalogs + favorites mutate + genres/types | L | 🟡 | DONE | T02, T04 | CatalogSetupAndFavoritesApiTests 3 PASS |
| T09 | Arr API happy path WireMock | L | 🟡 | DONE | T02 | ArrHappyPathApiTests 3 PASS; Integration 42/42 |

### TR-04 — Gap frontend flussi core (L)

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T10 | api/client + lib util | M | 🟢 | DONE | T01 | client/sourcePreference/useDebouncedValue tests |
| T11 | CatalogPage empty/filtri/quick filters | L | 🟡 | DONE | T10 | CatalogPage.test: emptyArr, view=leaving, preferred source |
| T12 | TitleDialog seasons + AddToArr errori | L | 🟡 | DONE | T10 | SeriesSeasons + AddToArrDialog; FE **56/56** PASS |

### TR-05 — Frontend residuo e harden (M)

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T13 | Pagine residue + harden FE deboli | M | 🟢 | DONE | T11, T12 | MyRatings/Similar/Learning + Quota error; FE **63/63**; typecheck PASS |

### TR-06 — Verifica finale indipendente (obbligatoria)

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T14 | Verifica finale indipendente | M | 🔴 | DONE | TR-01…TR-05 | PASS_WITH_DEFERRALS; artifacts/T14-verifica-finale.md; ARCHIVE A-006; §9 |

---

## 2. Flussi — stato per area

| Flusso | Stato | Note sintetiche |
| --- | --- | --- |
| FLUSSO-01 Catalogo titoli | ✅ Completato | FE CatalogPage empty/quick filter/source (T11) |
| FLUSSO-02 My list | ✅ Completato | PUT/DELETE + genres/types HTTP (T08) |
| FLUSSO-03 Detail/seasons/similar | ✅ Completato | HTTP (T07) + FE seasons/similar (T12/T13) |
| FLUSSO-04 Arr | ✅ Completato | Happy HTTP (T09) + FE Add/Delete (T12/T03) |
| FLUSSO-05 Settings/catalog setup | ✅ Completato | Service + HTTP catalogs (T04/T08) |
| FLUSSO-06 Admin | ✅ Completato | Jobs trigger + Quota figures/error (T06/T13) |
| FLUSSO-07 Sync/enrichment | ⏸ Accettato/Deferred (parziale) | Già ampio; solo edge residuali se emergono |
| FLUSSO-08 Ratings/reco | ✅ Completato | MyRatings/Similar/Learning (T13) |
| FLUSSO-09 Health/system | ✅ Completato | Adeguato in baseline |

---

## 3. Findings aperti

| ID | Gravità | Descrizione sintetica | Task target | Stato |
| --- | --- | --- | --- | --- |
| F-001 | Media | Coverage non misurata (coverlet/vitest) | T01 | RESOLVED |
| F-002 | Alta | Collection `"api"` condivisa + seed/settings mutabili | T02 | RESOLVED |
| F-003 | Media | Assert deboli Delete/Jobs/Quota + TextNormalizer (residui → F-012) | T03 | RESOLVED |
| F-004 | Alta | CatalogConfiguration + `/settings/catalogs` | T04, T08 | RESOLVED |
| F-005 | Bassa | Admin INVALID_TRIGGER / quota service poco coperti | T06 | RESOLVED |
| F-006 | Alta | Seasons/episodes/title 404/similar senza integration | T07 | RESOLVED |
| F-007 | Media | Favorites PUT/DELETE HTTP e genres lista | T08 | RESOLVED |
| F-008 | Media | Arr add/delete happy path HTTP assente | T09 | RESOLVED |
| F-009 | Media | `api/client` e lib senza test | T10 | RESOLVED |
| F-010 | Alta | CatalogPage/QuickFilters uncovered (line% fuorviante da router.test) | T11 | RESOLVED |
| F-011 | Alta | AddToArr + seasons UI uncovered | T12 | RESOLVED |
| F-012 | Bassa | FE secondario (MyRatings/Similar/Learning) | T13 | RESOLVED |

---

## 4. Decisioni architetturali

| ID | Data | Decisione | Owner | Motivazione/alternative | Fonte approvazione |
| --- | --- | --- | --- | --- | --- |
| D-001 | 2026-11-09 | Nuovo dominio `test-coverage` (non estendere mvp/reco) | agente | skill: nuova richiesta → nuovo dominio | utente: procedi TR-01 |
| D-002 | 2026-11-09 | Priorità confidence (contratti/errori/filtri) non vanity % coverage | agente | alternative gate 90% scartata ANALYSIS §6.3 | utente: procedi TR-01 |
| D-003 | 2026-11-09 | FLUSSO-07/08 trattati come “parzialmente adeguati”: solo gap residuali se emergono | agente | assessment: suite già ampia | utente: procedi TR-01 |
| D-004 | 2026-11-09 | CatalogApi + ArrAction fuori da collection `"api"`; seed per titolo nominato | agente | TC-06 / A-002 | esecuzione T02 |

---

## 5. Activity log

| Data | Task | Azione | Esito | Evidenza |
| --- | --- | --- | --- | --- |
| 2026-11-09 | INIT | Creati ANALYSIS/STATUS/PLAN/BASELINE/ARCHIVE/AGENT_KB | DONE | questa cartella |
| 2026-11-09 | T01 | coverlet.runsettings + vitest coverage; report baseline | DONE | artifacts/T01-coverage-baseline.md; Core/Infra/Int PASS con coverlet; FE 65.07% |
| 2026-11-09 | T02 | Collection `catalog-api`/`arr-action-api`; seed idempotente; ClearArr | DONE | `dotnet test tests/PickArr.Integration.Tests` 29/29 |
| 2026-11-09 | T03 | Harden DeleteFromArr/Jobs/Quota + TextNormalizer empty | DONE | Core 80 PASS; FE 41 PASS |
| 2026-11-09 | F-001→RESOLVED | Coverage misurabile | DONE | T01 |
| 2026-11-09 | F-002→RESOLVED | Isolation integration | DONE | T02 |
| 2026-11-09 | F-003→RESOLVED | Assert deboli mirati (residui F-012) | DONE | T03 |
| 2026-11-09 | T04 | CatalogConfigurationService: happy + 5 eccezioni + paid/quota | DONE | filtro CatalogConfiguration 8 PASS |
| 2026-11-09 | T05 | SeriesGuide missing/movie/null TMDB; Favorite KeyNotFound | DONE | SeriesGuide+FavoriteService PASS |
| 2026-11-09 | T06 | AdminJob ArgumentException; AdminQuota DTO; API INVALID_TRIGGER | DONE | Infra Admin* + Integration 31/31 |
| 2026-11-09 | F-005→RESOLVED | Admin trigger/quota | DONE | T06 |
| 2026-11-09 | T07 | Title detail/seasons/episodes/similar API | DONE | TitleDetailApiTests 5 PASS |
| 2026-11-09 | T08 | catalogs GET/PUT + favorites mutate + genres/types | DONE | CatalogSetupAndFavoritesApiTests 3 PASS |
| 2026-11-09 | T09 | Arr add/delete WireMock + refresh 503 + INVALID_BODY | DONE | ArrHappyPathApiTests 3 PASS; Integration 42/42 |
| 2026-11-09 | F-004,F-006,F-007,F-008→RESOLVED | Gap API TR-03 | DONE | T07–T09 |
| 2026-11-09 | T10 | client ApiError/204/buildQuery; sourcePreference; debounce | DONE | 9 test util |
| 2026-11-09 | T11 | CatalogPage emptyArr, Leaving→view, preferred source | DONE | CatalogPage.test 3 PASS |
| 2026-11-09 | T12 | SeriesSeasons order + AddToArr seasons/error | DONE | FE suite **56/56** |
| 2026-11-09 | F-009,F-010,F-011→RESOLVED | Gap FE TR-04 | DONE | T10–T12 |
| 2026-11-09 | T13 | MyRatings/Similar/Learning + Quota error; TitleDialog arr fixture | DONE | FE **63/63**; typecheck+lint PASS |
| 2026-11-09 | F-012→RESOLVED | FE residuo | DONE | T13 |
| 2026-11-09 | T14 | Verifica finale indipendente R-01…R-07 | DONE | PASS_WITH_DEFERRALS; artifacts/T14-verifica-finale.md; ARCHIVE A-006 |

---

## 6. KPI

| KPI | Baseline | Target | Attuale | Comando misura |
| --- | --- | --- | --- | --- |
| Fact/Theory Core (casi eseguiti) | 64 marcatori / 77 run | ≥70 edge sensati | **80** (T14) | `dotnet test tests/PickArr.Core.Tests` |
| Fact/Theory Infrastructure | 106 | ≥120 | **131** (T14) | Infra.Tests |
| Fact Integration | ~29 | ≥40 | **42 PASS** (T14) | Integration.Tests |
| Casi Vitest | 39 | ≥60 | **63** (T14) | `npm test -- --run` |
| Line coverage Core (Core.Tests coverlet) | n/d | misurata | 48.9% | artifacts/T01-coverage-baseline.md |
| Line coverage Infra (Infra.Tests) | n/d | misurata | 92.3% baseline T01 | idem |
| Line coverage FE (vitest) | n/d | misurata | 65.07% baseline T01 | `npm run test:coverage` |
| Finding P0/P1 OPEN | 8+ | 0 bloccanti | **0 OPEN** | STATUS §3 |

---

## 7. Checklist archiviazione

| Condizione | Verificata? |
| --- | --- |
| Activity log ha > 10 righe DONE | ☑ |
| Bootstrap/pacchetto supera budget orientativo | ☐ |
| ≥ 3 finding RESOLVED/ACCEPTED nella tabella §3 | ☑ (archiviare F-012) |
| Una decisione nuova rende obsolete note precedenti | ☐ |

**Trigger archiviazione:** finding RESOLVED → archiviare F-012 in ARCHIVE.md.

---

## 8. Tooling

| Aspetto | Convenzione |
| --- | --- |
| Branch strategy | a discrezione utente |
| Commit convention | solo su richiesta utente |
| Test backend | `dotnet test` / filtro FQN; API ferma se lock |
| Test frontend | `cd src/PickArr.WebApp && npm test -- --run` |
| Typecheck/lint FE | `npm run typecheck` / `npm run lint` |
| Coverage backend | `dotnet test PickArr.slnx --settings tests/coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/coverage-baseline` |
| Coverage frontend | `cd src/PickArr.WebApp && npm run test:coverage` |

---

## 9. Verifica finale indipendente

| Campo | Valore |
| --- | --- |
| Task finale | T14 |
| Esito | **PASS_WITH_DEFERRALS** |
| Revisore e sessione | 2026-11-09 sessione TR-06 + subagente verify-only `5a47ee9f-dfa5-4747-87da-7de80ab522ed` |
| Isolamento effettivo | Contesto post TR-05; revisore senza correzioni a codice/test/piano |
| Versione verificata | HEAD `c576634b3809b2c1f598ce6c1ae114b37f17f49c` + digest `6fe33ca59776439a` |
| Rapporto | `artifacts/T14-verifica-finale.md`; ARCHIVE A-006 |
| Rinvii/rischi approvati | FLUSSO-07 sync/enrichment (D-003) |

> Storico tranche, log datati, finding risolti: → `ARCHIVE.md`
