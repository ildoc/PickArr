# ARCHIVE — test-coverage

> Documento append-only: non modificare sezioni già scritte.
> Stato vivo → `STATUS.md`
> Analisi tecnica → `ANALYSIS.md`
> Creato: 2026-11-09

---

## Indice delle sezioni

| ID | Data | Argomento/task | Motivazione | Destinazione |
| --- | --- | --- | --- | --- |
| A-001 | 2026-11-09 | Finding F-001…F-003 RESOLVED (TR-01) | checklist ≥3 RESOLVED | questo file §A |
| A-002 | 2026-11-09 | Finding F-005 RESOLVED (TR-02) | chiusura T06 | questo file §B |
| A-003 | 2026-11-09 | Finding F-004,F-006,F-007,F-008 RESOLVED (TR-03) | chiusura T07–T09 | questo file §C |
| A-004 | 2026-11-09 | Finding F-009,F-010,F-011 RESOLVED (TR-04) | chiusura T10–T12 | questo file §D |
| A-005 | 2026-11-09 | Finding F-012 RESOLVED (TR-05) | chiusura T13 | questo file §E |
| A-006 | 2026-11-09 | TR-06 T14 verifica finale | PASS_WITH_DEFERRALS | questo file §F; artifacts/T14-verifica-finale.md |

---

## A. Finding RESOLVED TR-01 (2026-11-09)

| ID | Gravità | Descrizione | Risoluzione |
| --- | --- | --- | --- |
| F-001 | Media | Coverage non misurata | T01: coverlet + vitest; `artifacts/T01-coverage-baseline.md` |
| F-002 | Alta | Collection `"api"` + seed mutabile | T02: `catalog-api` / `arr-action-api`; seed per titolo; ClearArr |
| F-003 | Media | Assert deboli FE admin/arr + TextNormalizer | T03: submit/error Delete; trigger Jobs; Quota fetch; Theory empty Normalize |

Stato corrente in STATUS.md §3: rimandi RESOLVED senza dettaglio lungo.

---

## B. Finding RESOLVED TR-02 (2026-11-09)

| ID | Gravità | Descrizione | Risoluzione |
| --- | --- | --- | --- |
| F-005 | Bassa | Admin INVALID_TRIGGER / quota | T06: `AdminJobServiceTests`, `AdminQuotaServiceTests`, `AdminTriggerValidationApiTests` |

Nota: F-004 resta OPEN fino a T08 (HTTP catalogs); metà service chiusa in T04.

---

## C. Finding RESOLVED TR-03 (2026-11-09)

| ID | Gravità | Descrizione | Risoluzione |
| --- | --- | --- | --- |
| F-004 | Alta | CatalogConfiguration + `/settings/catalogs` | T04 service + T08 HTTP GET/PUT + 400 free limit |
| F-006 | Alta | seasons/episodes/404/similar | `TitleDetailApiTests` |
| F-007 | Media | Favorites PUT/DELETE + genres/types | `CatalogSetupAndFavoritesApiTests` |
| F-008 | Media | Arr happy path HTTP | `ArrHappyPathApiTests` WireMock |

---

## D. Finding RESOLVED TR-04 (2026-11-09)

| ID | Gravità | Descrizione | Risoluzione |
| --- | --- | --- | --- |
| F-009 | Media | `api/client` e lib senza test | T10: ApiError/204/`buildQuery`; sourcePreference; useDebouncedValue |
| F-010 | Alta | CatalogPage/QuickFilters uncovered | T11: emptyArr + Leaving→`view` + preferred source; no `favorite` |
| F-011 | Alta | AddToArr + seasons UI | T12: SeriesSeasons order + AddToArr selected seasons/error; FE 56/56 |

---

## E. Finding RESOLVED TR-05 (2026-11-09)

| ID | Gravità | Descrizione | Risoluzione |
| --- | --- | --- | --- |
| F-012 | Bassa | FE secondario MyRatings/Similar/Learning | T13: empty/tab/disabled ratings; similar navigate + disabled; LearningProgress bar; Quota error; Delete API già in T03; FE 63/63 |

---

## F. TR-06 Verifica finale indipendente (2026-11-09)

- Task: T14
- Esito: **PASS_WITH_DEFERRALS**
- Versione: HEAD `c576634b3809b2c1f598ce6c1ae114b37f17f49c` + worktree digest `6fe33ca59776439a` (30 path)
- Suite: Core 80 / Infra 131 / Integration 42 / Vitest 63; typecheck OK
- Rinvio approvato: FLUSSO-07 sync/enrichment (D-003)
- Rapporto completo: `artifacts/T14-verifica-finale.md`
- Follow-up: nessuno obbligatorio; dominio chiudibile

---
