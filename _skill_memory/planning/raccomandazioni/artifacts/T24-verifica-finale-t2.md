# T24 — Verifica finale indipendente (tentativo 2)

> Data: 2026-11-06
> Esito complessivo: **PASS_WITH_DEFERRALS**
> Revisore: subagente explore verify-only `ses_e4ef58dfdffeaTHjbBiJ8CiQYO` + evidenza comandi parent post-T25

## Versione verificata

| Campo | Valore |
| --- | --- |
| git HEAD | `58acd390c989de2f68e789c03b1bc01c9d683aab` |
| Worktree | ~127 path; digest mandato `84427dd73d8ccde9` (algoritmo hash non riprodotto 1:1 dal revisore; HEAD+conteggio path confermati) |
| Isolamento | subagente read-only post-T25; parent ha rieseguito suite Reco/Rating (Integration in sequenza per collezione) |
| Precendente | tentativo 1 FAIL — `artifacts/T24-verifica-finale.md`; correttivo T25 |

## Requisiti ANALYSIS §1

| ID | Esito | Evidenza |
| --- | --- | --- |
| R-RT | PASS | Rating API/DTO/UI ottimistica; gate reco.enabled |
| R-RT-EXT | PASS | /ratings export/import; /taste; L/D |
| R-SG | PASS | RecoSignals + useImplicit |
| R-RC | PASS | /for-you scopes; includeInLibrary UI→API; similar; rebuild |
| R-CD | PASS | Membership senza RemovedAt; no Watchmode in reco |
| R-ML | PASS | ONNX opzionale; fallback L0/L1; download/self-test |
| R-AD | PASS | Settings/admin/eval/L3; events default off |
| R-ST | PASS | T25: embeddingsEnabled + exploreRate DTO/API/UI; Avanzate pesi/MMR/N0 |
| R-DOCS | PASS | funzionale/tecnica + path WebApp |
| R-SPIKE | PASS | SP-R1…R4 artifacts; SP-R5 risk-only |

## Hard checks T25 (ex F-006)

Tutti PASS: DTO EmbeddingsEnabled/ExploreRate/MmrLambda; SettingsService GET/PUT; UI toggles + Avanzate; ForYou includeInLibrary; membership/events.

## Comandi (parent, post-T25)

| Comando | Esito |
| --- | --- |
| Core Settings+Reco+Rating | **37 PASS** |
| Infra Reco+Rating | **25 PASS** |
| Integration Recommendations / Ratings / RecoAdmin (sequenziali) | **9 PASS** |
| `npm test -- --run` | **31 PASS** |
| `npm run typecheck` | OK |

Nota: Integration in parallelo sullo stesso filtro può flakeare (Hangfire Memory multi-factory); in sequenza per collezione è verde.

## Difetti bloccanti

Nessuno.

## Non bloccanti

- `reco.ml.minRatings` non in Avanzate UI (solo seed/API)
- Explore toggle ripristina 0.1 (non conserva rate custom)
- Rebuild debounce solo da RatingService (implicits via job orario)

## Rinvii / rischi approvati

1. SP-R5 licenze — D-003
2. K-013 NFR L1 bordo host — D-013

## Verdetto

**PASS_WITH_DEFERRALS** sulla versione HEAD + worktree post-T25.
