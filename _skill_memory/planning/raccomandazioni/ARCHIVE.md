# ARCHIVE — raccomandazioni

> Documento append-only: non modificare sezioni già scritte.
> Stato vivo → `STATUS.md`
> Analisi tecnica → `ANALYSIS.md`
> Creato: 2026-11-06

---

## Indice delle sezioni

| ID | Data | Argomento/task | Motivazione | Destinazione |
| --- | --- | --- | --- | --- |
| A-001 | 2026-11-06 | TR-04 R1 UI chiusa (T07–T09) | Activity log + finding F-003 | sotto |
| A-002 | 2026-11-06 | TR-05 R2 enrichment (T10) | Activity log + finding F-002 | sotto |
| A-003 | 2026-11-06 | TR-11 T24 tentativo 1 FAIL | Verifica indipendente; F-006 | sotto |
| A-004 | 2026-11-06 | TR-11-FIX T25 + T24 tentativo 2 PASS | Chiusura verifica | sotto |
| A-005 | 2026-11-06 | Retrospettiva dominio raccomandazioni | FASE 3 chiusura | sotto |

---

## A-001 — TR-04 R1 UI (2026-11-06)

- T07: `RatingButtons` con `onMutate`/rollback; montaggio TitleCard/TitleDialog; scorciatoie L/D; hide se `reco.enabled=false`
- T08: `/for-you`, Header, scope source/enabled, `pickarr.sourceId`, LearningProgress, ReasonChips (popular)
- T09: `GET/POST /api/v1/ratings` list+export+import; pagina `/ratings` con badge sorgente
- Verifica: `RatingsApiTests` 4 PASS; Vitest RatingButtons/ForYou/router PASS; typecheck OK
- F-003 RESOLVED (optimistic rating)

---

## A-002 — TR-05 R2 enrichment (2026-11-06)

- Migrazione `AddRecoFeatures`: Keywords, TitleKeywords, People, TitleCredits, Titles.OriginCountries
- DTO TMDB: cast id, crew Director, created_by, origin_country; CastJson resta nomi
- EnrichCoreAsync: persist keywords/credits; en-US anche con overview piena
- RecoFeatureJob (coda ml) + schedule daily; EnrichFeatureBackfillBatchAsync
- Verifica: EnrichmentServiceTests (keywords/credits/en-US/backfill) PASS
- F-002 RESOLVED

---

## A-003 — TR-11 T24 tentativo 1 FAIL (2026-11-06)

- Revisore: explore `ses_e4effbee9ffeW8quNQhIoT5TxA` (read-only) + parent test evidence
- Versione: HEAD `58acd390…` + worktree digest `74da903437fd55e6`
- Esito: **FAIL** su R-ST / FR-REC-ST-02 (toggles model + explore assenti da DTO/API/UI)
- Suite: Reco|Rating 68 PASS; WebApp 29 PASS; typecheck/lint OK
- Rapporto: `artifacts/T24-verifica-finale.md`
- Follow-up: T25 correttivo; T24 BLOCKED fino a nuova verifica indipendente

---

## A-004 — TR-11-FIX T25 + T24 tentativo 2 PASS (2026-11-06)

- T25: `EmbeddingsEnabled`/`ExploreRate`/`MmrLambda` in DTO+SettingsService; toggles + Avanzate in `RecommendationsSettingsPage`; `includeInLibrary` su `ForYouPage`; i18n en; test Settings/UI PASS; F-006..F-008 RESOLVED
- T24 tentativo 2: explore `ses_e4ef58dfdffeaTHjbBiJ8CiQYO` → **PASS_WITH_DEFERRALS**
- Rapporto: `artifacts/T24-verifica-finale-t2.md`
- Rinvii: SP-R5 (D-003); K-013 (D-013)
- Suite parent: Core 37 + Infra 25 + Integration 9 seq + WebApp 31 PASS

---

## A-005 — Retrospettiva dominio raccomandazioni (2026-11-06)

### Obiettivo raggiunto vs pianificato

- Piano R1→R4 + spike SP-R1…R4 + docs prodotto + verifica indipendente: **raggiunto** (PASS_WITH_DEFERRALS).
- Override D-REC-12: L3 implementato in R4 gated da eval offline.

### Task aggiunti in corso

- T25 correttivo post FAIL T24 (FR-REC-ST-02/04 + includeInLibrary UI) — non nel taglio iniziale T01–T24.

### Decisioni chiave

- D-001 dominio isolato; D-005 L3+eventi in R4; D-007 membership dedicata; D-013 K-013 chiuso su bordo host; D-014 defaults SP-R4 tenuti.

### Lezioni

- Verifica indipendente ha trovato gap Must settings non coperto dai test di tranche (DTO/UI vs keys seed-only).
- Integration Hangfire Memory: preferire run sequenziali per collezione in verifica finale.
- Path doc `frontend/` vs WebApp: allineare docs nella tranche docs, non lasciare alla fine.

