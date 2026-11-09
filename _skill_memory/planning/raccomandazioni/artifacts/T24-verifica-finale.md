# T24 — Verifica finale indipendente (tentativo 1)

> Data: 2026-11-06
> Esito complessivo: **FAIL**
> Revisore: subagente explore verify-only `ses_e4effbee9ffeW8quNQhIoT5TxA` + evidenza comandi in sessione parent (nessuna modifica codice durante la revisione)

## Versione verificata

| Campo | Valore |
| --- | --- |
| git HEAD | `58acd390c989de2f68e789c03b1bc01c9d683aab` |
| Worktree | 127 path modificati/untracked (R1–R4 reco + docs); digest `74da903437fd55e6` |
| Isolamento | Revisore codice in contesto subagente separato (sola lettura); suite test rieseguite dal parent senza fix |
| Materiali ereditati | mandato T24, ANALYSIS §1, rinvio SP-R5 (D-003), K-013/D-013 |

## Requisiti ANALYSIS §1

| ID | Esito | Evidenza |
| --- | --- | --- |
| R-RT | PASS | Rating API/DTO/UI ottimistica; gate `reco.enabled`; Rating*/RatingButtons tests |
| R-RT-EXT | PASS | `/ratings`, export/import, L/D, `/taste`; RatingsApi + TastePage |
| R-SG | PASS | RecoSignals + useImplicit; RecoSignalsTests |
| R-RC | PASS | `/for-you`, scopes, exclude rated, reasons, similar, rebuild; RecommendationsApi* |
| R-CD | PASS | Membership senza RemovedAt; keywords/credits; no Watchmode in reco; RecoMembership*/RecoBuild* |
| R-ML | PASS | ONNX download/self-test/embed/kNN; fallback L0/L1 |
| R-AD | PASS | Settings/admin/eval/L3/events default off |
| R-ST | **FAIL** | Missing Must FR-REC-ST-02: nessun campo/UI per `reco.embeddings.enabled` e esplorazione (`reco.explore.rate`). Presenti solo enabled/useImplicit/events (+ pesi API senza toggle model/explore). FR-REC-ST-04 Avanzate assente (Should, non causa unica del FAIL) |
| R-DOCS | PASS | funzionale/tecnica v0.2; path WebApp |
| R-SPIKE | PASS | artifacts SP-R1…SP-R4; SP-R5 solo rischio |

## Comandi (parent, 2026-11-06)

| Comando | Esito |
| --- | --- |
| `dotnet test Pickarr.slnx --filter FullyQualifiedName~Reco\|FullyQualifiedName~Rating` | **68 PASS** (Core 34 + Infra 25 + Integration 9) |
| `npm test -- --run` (WebApp) | **29 PASS** |
| `npm run typecheck` | OK |
| `npm run lint` | exit 0 (warning `set-state-in-effect`, anche preesistenti) |

## Difetti bloccanti

1. **F-006** FR-REC-ST-02 Must: `RecoSettingsDto` / `RecoUpdateDto` / `SettingsService.UpdateAsync` / `RecommendationsSettingsPage` non espongono toggle modello locale né esplorazione.

## Non bloccanti (registrati)

- FR-REC-ST-04 *Avanzate* UI assente (pesi/MMR/explore rate)
- UI `includeInLibrary` su `/for-you` non collegata (API sì)
- SP-R3 L1 bordo host (D-013 accettato); SP-R5 licenze (D-003)

## Rinvii / rischi espliciti approvati

1. SP-R5 licenze — nota rischio only (D-003)
2. K-013 NFR bordo L1 — RESOLVED utente (D-013)

## Verdetto

**FAIL** — requisito Must R-ST/FR-REC-ST-02 non soddisfatto; non è un rinvio approvato. Task T24 → BLOCKED; correttivo **T25** prima di ripetere la verifica in contesto separato.
