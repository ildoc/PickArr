# PLAN — test-coverage

## TR-01 — Tooling coverage e igiene suite esistente

### T01 — Abilitare misura coverage e report baseline

- Scope: config test (`*.csproj` / Directory.Build se serve, `vite.config.ts`, eventuale script npm); artefatto in `_skill_memory/planning/test-coverage/artifacts/` (es. `T01-coverage-baseline.md` con comandi e summary); non cambiare codice prodotto
- Requisiti: ANALYSIS.md §1 R-01; A-006
- Acceptance: comando documentato produce report backend (coverlet) e frontend (vitest coverage); summary con % o conteggi file scoperti salvato come artefatto; KPI STATUS §6 “Attuale” aggiornabili
- Verifica: eseguire i comandi documentati; esito PASS se report generati
- Contesto pertinente: K-002, K-003; finding F-001
- Assunzioni: nessuna
- Rollback: rimuovere solo config/script/artefatti aggiunti in questo task

### T02 — Isolare integration test con stato mutabile condiviso

- Scope: `tests/Pickarr.Integration.Tests/CatalogApiTests.cs`, `ArrActionApiTests.cs`, eventualmente `AdminApiTests.cs` / `ArrConnectionApiTests.cs` se necessario; pattern collection come `FavoritesApiTests`
- Requisiti: R-02; A-002; TC-06
- Acceptance: seed non dipende da “DB vuoto globale”; test che cancellano settings Arr non contaminano altri della suite; `dotnet test tests/Pickarr.Integration.Tests` PASS
- Verifica: `dotnet test tests/Pickarr.Integration.Tests` (API ferma se lock DLL)
- Contesto: F-002; K-005; K-007
- Assunzioni: K-007 da confermare
- Rollback: ripristinare attributi collection/seed precedenti

### T03 — Pass di accuratezza sui test unitari deboli esistenti

- Scope: test già presenti con assert poco discriminanti (es. solo `ShouldNotBeNull` senza comportamento); priorità file citati in findings; aggiornare assert, non aggiungere feature
- Requisiti: R-02; A-007 (parte backend se applicabile)
- Acceptance: elenco file toccati in log; ogni test modificato fallisce se si inverte il comportamento chiave; suite progetti toccati PASS
- Verifica: `dotnet test` filtrato sui progetti modificati; per FE se toccato `npm test -- --run`
- Contesto: F-003
- Assunzioni: nessuna
- Rollback: revert dei soli file test modificati

## TR-02 — Gap backend servizi critici

### T04 — Test `CatalogConfigurationService` (limiti e eccezioni)

- Scope: nuovo test Infra (o Core se estratto); `CatalogConfigurationService.cs`; opzionale mapping exception in `SettingsEndpoints` se già espone HTTP
- Requisiti: R-03; A-001; K-004
- Acceptance: casi — provider sconosciuto; enabled senza country; paese non offerto; superamento `FreeCountryLimit` su piano free; update valido persiste enable/country; eccezioni `CatalogSetupException` verificate
- Verifica: `dotnet test tests/Pickarr.Infrastructure.Tests --filter FullyQualifiedName~CatalogConfiguration`
- Contesto: F-004; FLUSSO-05
- Assunzioni: nessuna
- Rollback: rimuovere file test aggiunti

### T05 — Edge `SeriesGuide` + favoriti HTTP-adjacent

- Scope: `SeriesGuideTests.cs` (+ service); eventuali gap `FavoriteService` non coperti (title missing se previsto)
- Requisiti: R-03; A-003 (lato service)
- Acceptance: title missing / non-series → null o comportamento documentato; ordine seasons/episodes resta protetto; almeno un path errore TMDB o cache miss se esiste nel codice
- Verifica: filtro `SeriesGuide` (+ Favorite se toccato)
- Contesto: FLUSSO-02/03
- Assunzioni: nessuna
- Rollback: revert test

### T06 — Admin trigger invalidi e quota service

- Scope: test su `AdminJobService` ArgumentException paths e/o integration BadRequest `INVALID_TRIGGER`; `AdminQuotaService` se logic non solo pass-through
- Requisiti: R-03; FLUSSO-06
- Acceptance: jobKey sconosciuto / mode catalog non valido / sourceId mancante → BadRequest o exception attesa; quota DTO campi chiave assertati
- Verifica: Infra e/o Integration filtrati Admin
- Contesto: F-005
- Assunzioni: nessuna
- Rollback: revert test

## TR-03 — Gap API integration

### T07 — Titles: detail 404, seasons, episodes, similar errors

- Scope: nuovo file Integration (collection dedicata); WireMock TMDB se serve; `TitlesEndpoints`
- Requisiti: R-04; A-003
- Acceptance: GET title missing → 404; seasons newest-first via API; episodes ordered; similar KeyNotFound → 404 e/o RECO_DISABLED → 409 se riproducibile
- Verifica: `dotnet test … --filter FullyQualifiedName~Title` o nome classe nuova
- Contesto: FLUSSO-03; F-006
- Assunzioni: nessuna
- Rollback: rimuovere classe test

### T08 — Settings catalogs + favorites mutate + catalog genres/types

- Scope: Integration tests nuovi/estesi; endpoints Settings/Favorites/Catalog
- Requisiti: R-04; A-001; A-008
- Acceptance: GET/PUT `/api/v1/settings/catalogs` (happy + almeno un 4xx da setup exception se mappato); PUT/DELETE favorite idempotenti; GET genres/types catalog e favorites
- Verifica: Integration filtrata
- Contesto: F-004, F-007; K-004
- Assunzioni: mapping HTTP di `CatalogSetupException` da verificare in codice endpoint — se manca, finding + test service già in T04
- Rollback: revert test

### T09 — Arr API happy path + refresh con WireMock

- Scope: Integration Arr (collection dedicata); pattern da `ArrConnectionApiTests`; non secrets reali
- Requisiti: R-04; A-004; K-008
- Acceptance: POST add series/movie → 201/Created con body arr; DELETE con query params → OK e stato notInLibrary (o equivalente); refresh senza config → 503 `ARR_NOT_CONFIGURED`; JSON body invalido → 400 se applicabile
- Verifica: Integration Arr filtrata + suite Integration PASS
- Contesto: FLUSSO-04; F-008
- Assunzioni: K-008
- Rollback: rimuovere test/WireMock stubs

## TR-04 — Gap frontend flussi core

### T10 — `api/client.ts` e util lib

- Scope: `client.ts`, `buildQuery`; `sourcePreference.ts`; `useDebouncedValue.ts` (test dedicati)
- Requisiti: R-05; A-005
- Acceptance: non-OK → `ApiError` con status; 204 senza JSON; `buildQuery` omette false/empty/undefined; preferenza source round-trip localStorage; debounce emette dopo delay (fake timers)
- Verifica: `npm test -- --run` path relativi
- Contesto: F-009
- Assunzioni: nessuna
- Rollback: rimuovere test

### T11 — CatalogPage: empty states, filtri, quick filters

- Scope: `CatalogPage.tsx`, `QuickFilters.tsx`, `TitleGrid` se necessario per emptyReason; mock api come router.test
- Requisiti: R-05; TC-10; TC-11; FLUSSO-01
- Acceptance: con `emptyReason=arrNotConfigured` (o equivalente) UI mostra empty; cambio quick filter aggiorna request `view`; preferenza source da localStorage usata; nessuna query `favorite`
- Verifica: vitest run sui nuovi test
- Contesto: F-010; K-006
- Assunzioni: nessuna
- Rollback: rimuovere test

### T12 — TitleDialog seasons + AddToArrDialog errori

- Scope: `TitleDialog` / `SeriesSeasons` / `SeasonTable`; `AddToArrDialog.tsx`
- Requisiti: R-05; product seasons order; Arr errors
- Acceptance: stagioni render newest-first; selezione stagioni inviata in payload add (solo selezionate); errore API Arr mostrato (codice/messaggio); non regressione test rating esistente
- Verifica: vitest + typecheck se tipi toccati
- Contesto: F-011; FLUSSO-03/04
- Assunzioni: nessuna
- Rollback: revert test (e minimo glue di test-id solo se inevitabile — preferire ruoli/i18n)

## TR-05 — Frontend residuo e harden

### T13 — Pagine residue + rafforzamento test FE deboli

- Scope: `MyRatingsPage`, `SimilarRow`, `LearningProgress` (smoke comportamentale); aggiornare `DeleteFromArrDialog.test` (submit chiama API con query), `JobsPage`/`QuotaPanel` se assert troppo deboli
- Requisiti: R-06; A-007
- Acceptance: almeno un assert su request/callback per dialog delete; pagine residue coprono loading/empty o azione primaria; `npm test -- --run` + lint/typecheck se modificati sorgenti
- Verifica: comandi FE in AGENTS.md
- Contesto: F-003, F-012
- Assunzioni: nessuna
- Rollback: revert test

## TR-06 — Verifica finale indipendente

### T14 — Verifica finale indipendente

- Scope: intero perimetro R-01…R-06 implementato, test, config coverage, requisiti
- Requisiti: ANALYSIS.md §1 e §7
- Acceptance: criteri obbligatori verificati sulla versione identificata; nessun difetto bloccante; rinvii approvati espliciti; rapporto PASS in ARCHIVE referenziato in STATUS §9
- Verifica: protocollo skill — revisore contesto separato, senza correzioni a codice/test/piano
- Evidenze: ARCHIVE.md sezione tentativo; STATUS §9
