# raccomandazioni STATUS

> Ultimo aggiornamento: 2026-11-06
> Branch: `main`
> Storico operativo: `ARCHIVE.md`

---

## 0. Quick Context

- Obiettivo: voto globale su Title + raccomandazioni locali R1→R4 (`docs/raccomandazioni.md` v0.2); requisiti in ANALYSIS.md §1
- Stato dominio: **CHIUSO CON RINVII APPROVATI** (PASS_WITH_DEFERRALS T24 t2)
- Codebase: HEAD `58acd390…` + worktree reco R1–R4 + T25 (non committed)
- Snapshot locale: verifica t2 OK; digest mandato `84427dd73d8ccde9`
- Tranche/task candidato: nessuno (dominio chiuso)
- Dettagli: ARCHIVE A-004/A-005; artifacts/T24-verifica-finale-t2.md
- Blocchi e findings critici: tutti RESOLVED; rinvii SP-R5 + K-013
- Input umano: nessuno
- Handoff: dominio chiuso; commit solo su richiesta utente
- Verifica finale: §9 PASS_WITH_DEFERRALS

---

## 1. Roadmap

### TR-01 — Spike SP-R1

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T01 | Spike SP-R1 payload TMDB / en-US | M | 🟡 | DONE | — | PLAN.md TR-01 T01; artifacts/SP-R1.md |

### TR-02 — R1 schema e API voto

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T02 | Migrazione Ratings/RatingEvents + settings reco base | M | 🟡 | DONE | — | PLAN.md TR-02 T02; migrazione AddRatings |
| T03 | RatingService + API + campo rating DTO | M | 🟡 | DONE | T02 | PLAN.md TR-02 T03; test Rating* PASS |

### TR-03 — R1 prior e API Per te

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T04 | Membership + prior Core | M | 🔴 | DONE | — | PLAN.md TR-03 T04; Core.Tests Reco* PASS |
| T05 | Reco tables + coda ml + BuildJob L0 | L | 🔴 | DONE | T02, T04 | PLAN.md TR-03 T05; migrazione AddRecommendations; RecoBuild* PASS |
| T06 | GET /recommendations + /status + test scope | L | 🔴 | DONE | T03, T05 | PLAN.md TR-03 T06; RecommendationsApiTests PASS |

### TR-04 — R1 UI

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T07 | RatingButtons + gating + scorciatoie L/D | M | 🟡 | DONE | T03 | PLAN.md TR-04 T07; RatingButtons* PASS |
| T08 | ForYouPage + Header + source + LearningProgress | L | 🟡 | DONE | T06, T07 | PLAN.md TR-04 T08; ForYouPage* PASS |
| T09 | /ratings + export/import | L | 🟡 | DONE | T03 | PLAN.md TR-04 T09; RatingsApi* PASS |

### TR-05 — R2 enrichment

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T10 | Persist keyword/crediti/en-US + RecoFeatureJob | L | 🔴 | DONE | T01, T05 | PLAN.md TR-05 T10; Enrichment* PASS |

### TR-06 — R2 profilo e simili

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T11 | Segnali + sparse + build L1 + reasons + debounce | L | 🔴 | DONE | T10 | PLAN.md TR-06 T11; RecoSparse*/RecoBuild* PASS |
| T12 | Simili + ReasonChips + settings toggles | M | 🟡 | DONE | T11 | PLAN.md TR-06 T12; Similar API + SimilarRow |

### TR-07 — Spike SP-R2 / SP-R3

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T13 | Spike SP-R2 ONNX/tokenizer/perf | L | 🔴 | DONE | — (prima di TR-08) | artifacts/SP-R2.md; RecoOnnxEmbedderTests |
| T14 | Spike SP-R3 rebuild/GET latency | M | 🟡 | DONE | T11 | artifacts/SP-R3.md; SpR3PerformanceSmokeTests; K-013 RESOLVED |

### TR-08 — R3 modello

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T15 | Download modello + self-test + detect | L | 🔴 | DONE | T13 | RecoModelService + RecoModelDownloadJob; RecoOnnxEmbedderTests |
| T16 | TitleEmbeddings + RecoEmbeddingJob | L | 🔴 | DONE | T15, T10 | migrazione AddTitleEmbeddings; RecoEmbeddingText hash |
| T17 | kNN L2 + similar embedding | L | 🔴 | DONE | T16, T11 | RecoKnn* + coverage gate; similar emb-first + genre cap |
| T18 | MMR + explore + onboarding /taste | M | 🟡 | DONE | T17 | RecoMmr/Explore; /taste + TastePage.test; MMR Take(cap) |
| T19 | Settings recommendations + admin actions | L | 🟡 | DONE | T15, T06 | RecommendationsSettingsPage*; RecoAdminApiTests |

### TR-09 — R4 misura e L3

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T20 | RecoEvalJob + report | L | 🟡 | DONE | T17 | RecoEvalService + Job; POST evaluate / GET evaluations/latest; RecoEval* PASS |
| T21 | Livello 3 ML.NET | L | 🔴 | DONE | T20 | Microsoft.ML Lbfgs; gate Level3Active; RecoLevel3ModelTests PASS |
| T22 | RecoEventLog + SP-R4 taratura | M | 🟡 | DONE | T20 | events default off; artifacts/SP-R4.md; migrazione AddRecoEvalAndEvents |

### TR-10 — Docs prodotto

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T23 | Aggiornare documento-funzionale + specifica-tecnica | M | 🟢 | DONE | TR-02..TR-09 | docs v0.2; D-REC in funzionale; F-005 RESOLVED |

### TR-11-FIX — Correttivo post T24 FAIL

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T25 | Settings model/explore + Avanzate + include library UI | M | 🟡 | DONE | T24 FAIL | Settings DTO/API + Reco settings UI + ForYou includeInLibrary; SettingsServiceTests + npm PASS |

### TR-11 — Verifica finale indipendente

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli/evidenze |
| --- | --- | --- | --- | --- | --- | --- |
| T24 | Verifica finale indipendente | L | 🔴 | DONE | TR-01..TR-10, T25 | PASS_WITH_DEFERRALS t2; artifacts/T24-verifica-finale-t2.md; §9 |

**Effort tranche (stima):** TR-01 M · TR-02 M · TR-03 L · TR-04 L · TR-05 L · TR-06 L · TR-07 L · TR-08 XL (più sessioni) · TR-09 L · TR-10 M · TR-11-FIX M · TR-11 L

---

## 2. Flussi — stato per area

| Flusso | Stato | Note sintetiche |
| --- | --- | --- |
| FLUSSO-01 Catalogo query | ✅ as-is | Non modificare predicato RemovedAt per reco |
| FLUSSO-02 Preferiti | ✅ as-is | Template UI non ottimistico |
| FLUSSO-03 Enrichment TMDB | ✅ esteso | Keywords/credits/en-US + RecoFeatureJob (T10) |
| FLUSSO-04 Merge titolo | ✅ as-is | Voto segue Title.Id |
| FLUSSO-05 Raccomandazioni | ✅ R4 | L0–L3 gated + eval + events off + admin evaluate |
| FLUSSO-06 Embedding ONNX | ✅ | download/detect/embed/kNN; coverage gate |

---

## 3. Findings aperti

| ID | Gravità | Descrizione sintetica | Task target | Stato |
| --- | --- | --- | --- | --- |
| F-001 | Alta | Rischio riuso filtro RemovedAt catalogo in reco (RR-06 / A-001) | T04, T06 | RESOLVED |
| F-002 | Alta | Keywords/credits non persistiti; cast senza person id (A-002). Spike: payload OK senza nuovo URL; resta T10 | T10 | RESOLVED |
| F-003 | Media | FavoriteButton non ottimistico; rating deve implementare onMutate (A-003) | T07 | RESOLVED |
| F-004 | Media | CatalogPlaceDto senza removedAt (A-004) | T06, T12 | RESOLVED |
| F-005 | Bassa | Path doc `frontend/` vs WebApp (A-006) | T23 | RESOLVED |
| F-006 | Alta | FR-REC-ST-02: toggles model/explore assenti da settings DTO/API/UI | T25 | RESOLVED |
| F-007 | Media | FR-REC-ST-04 Avanzate UI (pesi/MMR/explore rate) assente | T25 | RESOLVED |
| F-008 | Media | FR-REC-RC-07: UI includeInLibrary su `/for-you` non collegata | T25 | RESOLVED |

---

## 4. Decisioni architetturali

| ID | Data | Decisione | Owner | Motivazione/alternative | Fonte approvazione |
| --- | --- | --- | --- | --- | --- |
| D-001 | 2026-11-06 | Dominio nuovo `raccomandazioni`; non modificare KB pickarr-mvp | utente | Isolamento skill | chat planning |
| D-002 | 2026-11-06 | Perimetro R1→R4 + tutti M/S/C | utente | Piano completo documento | chat planning |
| D-003 | 2026-11-06 | Spike SP-R1..R4 come task; SP-R5 solo rischio ANALYSIS | utente | Gate tecnici vs legale | chat planning |
| D-004 | 2026-11-06 | i18n solo en.json; reco.enabled in R1 | utente | AGENTS.md + FR-REC-ST-01 | chat planning |
| D-005 | 2026-11-06 | Livello 3 implementato in R4 (override D-REC-12 “fuori MVP”); eventi in R4 default off | utente | Completare Could | chat planning |
| D-006 | 2026-11-06 | Docs prodotto (§21) in TR-10 prima di verifica finale | utente | Allineamento specifiche | chat planning |
| D-007 | 2026-11-06 | Membership reco in helper dedicato; non riusare TitleQueryService grid filter | agente | A-001 / RR-06 | assessment |
| D-008 | 2026-11-06 | Rating UI con onMutate reale; non copiare FavoriteButton as-is | agente | FR-REC-RT-07 / F-003 | assessment |
| D-009 | 2026-11-06 | Nessun SSE: invalidate/refetch dopo voto | agente | K-008; §14.4 fallback | assessment |
| D-010 | 2026-11-06 | Taglio 11 tranche / 24 task (T01–T24) allineato R* + spike + docs + verifica | agente | richiesta “lascia decidere analisi”; task ≤ L | assessment |
| D-011 | 2026-11-06 | Piano approvato; FASE 2 sospesa fino a richiesta esplicita | utente | Controllo avvio esecuzione | chat planning |
| D-012 | 2026-11-06 | SP-R1 GO: T10 estende DTO/mapping/persistenza sullo stesso client; en-US anche se overview piena; fixtures da arricchire | agente | artifacts/SP-R1.md | esecuzione T01 |
| D-013 | 2026-11-06 | K-013 RESOLVED: NFR-REC-01 PASS post TR-08; NFR-REC-02 OK per intent (L1 primario; L1/L2 bordo host ±3%); no ulteriore micro-ottimizzazione L1 | utente | rivalidazione SpR3 2 run; opzione «RESOLVED consigliato» | chat SpR3/K-013 |
| D-014 | 2026-11-06 | SP-R4: mantenere defaults a/b/N0/γ/K/m e reco.ml.minRatings=100; nessun seed Settings cambiato | agente | fixture sintetica insufficiente per tarare su voti reali | artifacts/SP-R4.md |

---

## 5. Activity log

| Data | Task | Azione | Esito | Evidenza |
| --- | --- | --- | --- | --- |
| 2026-11-06 | INIT | Creati ANALYSIS/PLAN/STATUS/BASELINE/AGENT_KB/ARCHIVE; assessment codice | DONE | questa KB; explore ses_e4fdbae0 |
| 2026-11-06 | PLAN | Utente conferma piano; non avviare ancora FASE 2 | DONE | chat: «Confermo piano, non eseguire ancora» |
| 2026-11-06 | T01 | Spike SP-R1: ispezione DTO/fixtures + docs TMDB; report GO | DONE | artifacts/SP-R1.md |
| 2026-11-06 | T02 | Ratings/RatingEvents + settings reco.* seed + SettingsResponse.Reco | DONE | migrazione 20261106093517_AddRatings; DatabaseInitializerTests |
| 2026-11-06 | T03 | RatingService + PUT/DELETE rating + DTO rating; test unit/integration | DONE | `dotnet test` filter Rating*/RatingsApi* PASS |
| 2026-11-06 | T04 | Membership reco + prior bayes/pop + stage/weight Core | DONE | `dotnet test` Core filter Reco PASS (8) |
| 2026-11-06 | T05 | Recommendations/RecoState + Hangfire ml + RecoBuildJob L0 | DONE | migrazione AddRecommendations; RecoBuildServiceTests PASS |
| 2026-11-06 | T06 | GET /recommendations + /status; places.removedAt; scope tests | DONE | RecommendationsApiTests PASS |
| 2026-11-06 | T07 | RatingButtons onMutate+rollback; gating; L/D in dialog | DONE | npm test RatingButtons PASS |
| 2026-11-06 | T08 | ForYouPage + Header + LearningProgress + ReasonChips + source | DONE | npm test ForYouPage/router PASS |
| 2026-11-06 | T09 | GET/export/import ratings + MyRatingsPage | DONE | RatingsApiTests PASS (4) |
| 2026-11-06 | T10 | Keywords/People/Credits + en-US + RecoFeatureJob | DONE | EnrichmentServiceTests PASS; migrazione AddRecoFeatures |
| 2026-11-06 | T11 | Segnali impliciti + sparse TF-IDF + build L1 + reasons + debounce 30s | DONE | RecoSignals/Sparse/Build* PASS; RecoRebuildScheduler |
| 2026-11-06 | T12 | GET similar + SimilarRow + ReasonChips + settings toggles | DONE | RecommendationsApiTests Similar*; npm ReasonChips/Settings |
| 2026-11-06 | T13 | SP-R2 ONNX+BertTokenizer+q8 GO | DONE | artifacts/SP-R2.md; self-test PASS su host |
| 2026-11-06 | T15–T19 | Modello, embeddings, kNN, MMR/explore/taste, settings/admin | DONE | Core Reco* 23; Infra Reco* 14; Integration Recommendations PASS |
| 2026-11-06 | T14 | SP-R3 latency 5k/500: L1 8.4s PASS; GET plain 114ms PASS; MMR/similar/L2 gap | DONE | artifacts/SP-R3.md |
| 2026-11-06 | TR-08 | Chiusura incompleta: MMR Take(cap), similar emb-first+genre, kNN maxSignals=64; test Taste/Settings/Admin/L2 gate | DONE | Core Reco 24; Infra Reco 16; RecoAdminApi + RecommendationsApi PASS; npm Taste/RecoSettings PASS |
| 2026-11-06 | K-013 | Rivalidazione SpR3 post TR-08; utente chiude RESOLVED (bordo L1 accettato) | DONE | artifacts/SP-R3.md §4–7; D-013 |
| 2026-11-06 | T20 | RecoEvalJob leave-one-out + report + warning &lt;30 | DONE | RecoEvalServiceTests; RecoAdmin evaluate/latest |
| 2026-11-06 | T21 | L3 ML.NET + gate batte L2; inattivo sotto minRatings | DONE | RecoLevel3ModelTests; RecoBuild usa Level3Active |
| 2026-11-06 | T22 | RecoEventLog default off + SP-R4 defaults tenuti | DONE | RecoEventLogServiceTests; artifacts/SP-R4.md; D-014 |
| 2026-11-06 | T23 | Docs prodotto §21: funzionale+tecnica v0.2; path WebApp; D-REC; F-005 | DONE | documento-funzionale.md; specifica-tecnica.md; raccomandazioni §15 |
| 2026-11-06 | T24 | Verifica finale indipendente tentativo 1 | FAIL | artifacts/T24-verifica-finale.md; F-006; T24 BLOCKED; T25 aperto |
| 2026-11-06 | T25 | embeddingsEnabled/exploreRate/MMR/pesi in settings + Avanzate UI; includeInLibrary For you | DONE | SettingsServiceTests; RecommendationsSettingsPage/ForYou npm PASS; F-006..F-008 RESOLVED |
| 2026-11-06 | T24 | Verifica finale indipendente tentativo 2 | PASS_WITH_DEFERRALS | artifacts/T24-verifica-finale-t2.md; ARCHIVE A-004 |
| 2026-11-06 | CLOSE | FASE 3 chiusura dominio raccomandazioni | DONE | ARCHIVE A-005 retrospettiva; §0 CHIUSO CON RINVII APPROVATI |

---

## 6. KPI

| KPI | Baseline | Target | Attuale | Comando misura |
| --- | --- | --- | --- | --- |
| Attributi test backend [Fact]/[Theory] | ≥100 (truncati in grep) | non regredire + suite reco | = baseline | grep test attributes |
| File CS in src+tests | 230 | — | 230 | find |
| File TS/TSX WebApp src | 51 | — | 51 | find |
| NFR GET /recommendations p95 | n/d | <300 ms | plain ~105 / MMR ~150 / similar ~77 / enabled ~105 PASS | SpR3PerformanceSmokeTests |
| NFR rebuild pool+500 segnali | n/d | <10 s | L1 ~10.1s bordo host; L2 ~10s con maxSignals=64; K-013 RESOLVED | SpR3PerformanceSmokeTests |

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
| Branch strategy | feature opzionale `reco/*`; non obbligatorio |
| Commit | solo su richiesta utente |
| Test backend | `dotnet test` / filtri FullyQualifiedName~Reco\|Rating |
| Test frontend | `cd src/Pickarr.WebApp && npm test -- --run` |
| Typecheck/lint | `npm run typecheck` && `npm run lint` |
| DataPath debug | `.data` via launchSettings; non mischiare DB |
| Stop API | prima di build se DLL locked |

---

## 9. Verifica finale indipendente

| Campo | Valore |
| --- | --- |
| Task finale | T24 |
| Esito | **PASS_WITH_DEFERRALS** (tentativo 2; tentativo 1 FAIL → T25) |
| Revisore e sessione | explore `ses_e4ef58dfdffeaTHjbBiJ8CiQYO` + parent tests post-T25 |
| Isolamento effettivo | subagente read-only nuovo contesto; parent suite sequenziali |
| Versione verificata | HEAD `58acd390…` + worktree post-T25 (digest mandato `84427dd73d8ccde9`) |
| Rapporto | `artifacts/T24-verifica-finale-t2.md`; ARCHIVE A-004 |
| Rinvii/rischi approvati | SP-R5 (D-003); K-013 (D-013) |

> Storico tranche, log datati, finding risolti: → `ARCHIVE.md`
