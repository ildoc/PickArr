# PLAN — raccomandazioni

Scope/acceptance canonici. Stato/dipendenze/size solo in STATUS.md §1.
Requisiti: ANALYSIS.md §1; specifiche dettagliate in `docs/raccomandazioni.md` v0.2.

## TR-01 — Spike SP-R1

### T01 — Spike SP-R1: payload TMDB e costo en-US

- Scope: `TmdbJsonModels.cs`, `TmdbMetadataProvider.cs`, fixtures `tests/fixtures/tmdb/*`; artefatto `_skill_memory/planning/raccomandazioni/artifacts/SP-R1.md` (solo KB dominio)
- Requisiti: ANALYSIS R-SPIKE; SP-R1; K-012
- Acceptance: report con (1) se cast[].id / crew Director / created_by sono già nel JSON senza cambiare URL; (2) stima chiamate extra language=en-US sul limiter; (3) go/no-go per T10 senza secondo client TMDB
- Verifica: ispezione fixtures + docs TMDB ufficiali; report in artifacts — **PASS 2026-11-06** (`artifacts/SP-R1.md`, esito GO)
- Contesto: A-002, K-004..K-006; D-012
- Assunzioni: K-012 → promossa FACT in ANALYSIS §8
- Rollback: eliminare solo artefatto spike; nessun schema

## TR-02 — R1 schema e API voto

### T02 — Migrazione Ratings/RatingEvents + Settings reco base

- Scope: entity Core; EF configurations/migration; `PickarrDbContext`; `SettingsKeys` (`reco.enabled`, pesi base, `n0`, `excludeInLibrary`, … minimi R1); DI
- Requisiti: R-RT, R-ST; §10.2 Ratings/RatingEvents; §16 subset
- Acceptance: migrazione applicabile; tabella Ratings PK TitleId; RatingEvents con Origin; setting `reco.enabled` default true leggibile da SettingsService; test persistenza smoke
- Verifica: `dotnet test` su Infrastructure (initializer/migration) filtrato — **PASS 2026-11-06** (DatabaseInitializerTests + migrazione `20261106093517_AddRatings`)
- Contesto: V-REC-03
- Assunzioni: nessuna
- Rollback: migration Down; rimuovere chiavi non referenziate

### T03 — RatingService + API PUT/DELETE + campo rating DTO

- Scope: servizio rating; endpoints titles rating; estensione `TitleListItemDto`/`TitleDetailDto` (campo user `rating`, senza rompere `TitleRatingsDto` TMDB); query list/detail; test Core/Infra/Integration collection dedicata se serve DB pulito
- Requisiti: FR-REC-RT-01..04,07; BR-REC-01
- Acceptance: PUT like/dislike idempotente 204; DELETE rimuove e logga evento null; un voto per Title; list/detail espongono `rating`; con `reco.enabled=false` → 409 `RECO_DISABLED` (o comportamento documentato coerente); test unit+integration PASS
- Verifica: `dotnet test --filter FullyQualifiedName~Rating` — **PASS** (Core 7, Infra RatingService 4, Integration RatingsApi 3)
- Contesto: A-005 naming
- Assunzioni: nessuna
- Rollback: rimuovere endpoints/servizio; DTO senza campo

## TR-03 — R1 prior e API Per te

### T04 — Membership pool/scope + algoritmo prior (Core)

- Scope: helper appartenenza (source/enabled, RemovedAt ignorato); prior bayes+pop §12.2; test unit sintetici
- Requisiti: R-CD, R-RC; BR-REC-07; RR-06
- Acceptance: test: titolo con RemovedAt ∈ scope; sorgente disabled esclusa; zero Availability escluso; ordine prior deterministico su dataset sintetico
- Verifica: `dotnet test tests/Pickarr.Core.Tests --filter FullyQualifiedName~Reco` — **PASS 2026-11-06** (8 test)
- Contesto: A-001, K-002, K-003
- Assunzioni: nessuna
- Rollback: rimuovere modulo Core reco

### T05 — Recommendations/RecoState + coda ml + RecoBuildJob L0

- Scope: tabelle Recommendations/RecoState; `SyncRunKinds` RecoBuild (+ Feature stub se serve); `HangfireBootstrap` secondo server `Queues=["ml"]` WorkerCount=1; job build L0 prior su pool; debounce non ancora obbligatorio se solo prior
- Requisiti: FR-REC-RC-05,12; NFR-REC-05; §11.1
- Acceptance: build pubblica version+1, cancella vecchie; RecoState.stage=cold con N_eff=0; job su coda ml non blocca sync; test infra con Hangfire Memory
- Verifica: test job/build filtrati — **PASS 2026-11-06** (`RecoBuildServiceTests`; Queue=`ml`)
- Contesto: K-007
- Assunzioni: nessuna
- Rollback: togliere secondo server e kind; migration Down tabelle reco

### T06 — GET /recommendations + /status + test scope

- Scope: endpoints recommendations; mapping item con availability o places (places con removedAt se valorizzato — estendere DTO place lato reco); filtri type/genre/year/includeInLibrary; esclusione votati
- Requisiti: FR-REC-RC-01..08; NFR-REC-01 (precalc)
- Acceptance: scope=source richiede sourceId; enabled dedupe+places; votati assenti; RemovedAt non esclude; status espone stage/N_eff; integration test due scope + voto cross-provider stesso TitleId
- Verifica: `dotnet test tests/Pickarr.Integration.Tests --filter FullyQualifiedName~Reco` — **PASS 2026-11-06** (`RecommendationsApiTests`)
- Contesto: A-004
- Assunzioni: nessuna
- Rollback: rimuovere endpoints

## TR-04 — R1 UI

### T07 — RatingButtons ottimistici + gating + scorciatoie

- Scope: `features/ratings/RatingButtons`; montaggio TitleCard/TitleDialog; i18n `ratings`/`reco` in `en.json`; hide se disabled; scorciatoie L/D in dialog (C)
- Requisiti: FR-REC-RT-02,07,08; FR-REC-ST-01
- Acceptance: toggle like/dislike/remove con onMutate+rollback; invalidate titles/title/recommendations/ratings; test Vitest mutazione; scorciatoie funzionanti in dialog
- Verifica: `npm test -- --run` filtrato ratings; typecheck
- Contesto: A-003, K-009
- Assunzioni: nessuna
- Rollback: rimuovere componenti/import

### T08 — ForYouPage + Header + source condivisa + LearningProgress

- Scope: route `/for-you`; Header link; riuso FilterBar/TitleGrid/TitleDialog; scope search param; `sourcePreference`; ReasonChips minimi (popular); LearningProgress
- Requisiti: FR-REC-RC-01..03,05,06; BR-REC-13; US-REC-03..04
- Acceptance: default scope source; nasconde enabled se una sola sorgente; cambia source aggiorna `pickarr.sourceId`; cold indicator; infinite scroll; test router/source
- Verifica: npm test ForYou/router; typecheck lint
- Contesto: K-010
- Assunzioni: nessuna
- Rollback: rimuovere route/nav

### T09 — /ratings + export/import

- Scope: API GET ratings, export, import; pagina `/ratings` tab like/dislike; badge rispetto sorgente selezionata; places
- Requisiti: FR-REC-RT-05,06
- Acceptance: elenco globale; import merge/replace solo titoli esistenti per tmdbType+tmdbId; contatori unknown; UI export/import; test API import unknown
- Verifica: dotnet test + npm test ratings page
- Contesto: nessuno
- Assunzioni: nessuna
- Rollback: rimuovere route/API

## TR-05 — R2 enrichment features

### T10 — Persistenza keyword/crediti/en-US + RecoFeatureJob

- Scope: estendere DTO TMDB (id, crew, created_by); tabelle Keywords/TitleKeywords/People/TitleCredits; opz. OriginCountries; EnrichCoreAsync persist; fetch en-US se manca; RecoFeatureJob backfill Ready; SyncRunKinds.RecoFeature
- Requisiti: FR-REC-CD-04; §11.2; dipende esito T01
- Acceptance: titolo arricchito ha keyword e crediti in DB; CastJson resta lista nomi; job idempotente; EnrichmentServiceTests aggiornati; nessun nuovo client TMDB
- Verifica: Infrastructure tests enrichment + feature job
- Contesto: T01 go; A-002; K-005
- Assunzioni: K-012 confermata da T01
- Rollback: non scrivere nuove tabelle; feature flag skip persist se serve

## TR-06 — R2 profilo e simili

### T11 — Segnali + sparse + fusione + build L1 + reasons + debounce

- Scope: Core sparse TF-IDF/profilo/fusione §12.3–12.5,12.7; lettura Favorite/ArrAuditLog/ArrLibraryItem; RecoBuildJob L1; debounce 30s su voto; Settings useImplicit
- Requisiti: FR-REC-SG-*; FR-REC-RC-06,12; BR-REC-02..05,12
- Acceptance: dataset fantascienza→top fantascienza; dislike abbassa vicini; voto esplicito ignora implicit; Delete non nega Add; reasonsJson kinds similarTo|terms|popular; rebuild debounce
- Verifica: Core.Tests algoritmo; Infra build test
- Contesto: nessuno
- Assunzioni: nessuna
- Rollback: build torna L0-only via setting

### T12 — Simili sparse + ReasonChips + settings toggles

- Scope: `GET /titles/{id}/similar` membership enabled+places; SimilarRow in TitleDialog; ReasonChips i18n; settings switches implicit/enabled già esposti
- Requisiti: FR-REC-RC-09; BR-REC-14; FR-REC-ST-02
- Acceptance: vicino solo su altro provider restituito con places; votati esclusi; UI mostra motivi; test API similar
- Verifica: integration + npm test
- Contesto: A-004
- Assunzioni: nessuna
- Rollback: nascondere SimilarRow

## TR-07 — Spike SP-R2 e SP-R3

### T13 — Spike SP-R2: ONNX + tokenizer + perf

- Scope: spike isolato (prototipo o doc+bench); artifacts/SP-R2.md; scelta tokenizer .NET e variante modello
- Requisiti: SP-R2; D-REC-11; K-014
- Acceptance: report con tokenizer, self-test coseno, titoli/s, RAM, go/no-go quantizzato; aggiorna ANALYSIS §7 se cambia modelId
- Verifica: artefatto + misure dichiarate
- Contesto: RR-04, RR-05
- Assunzioni: K-014
- Rollback: nessuno sul prodotto se spike fuori runtime

### T14 — Spike SP-R3: rebuild e GET latency

- Scope: misure su pool (fixture o DB .data esplicito); artifacts/SP-R3.md
- Requisiti: SP-R3; NFR-REC-01/02; K-013
- Acceptance: numeri rebuild e GET con join; note MMR; gap vs NFR documentati
- Verifica: artefatto
- Contesto: dipende da T05/T11 build esistente
- Assunzioni: K-013
- Rollback: nessuno

## TR-08 — R3 modello locale

### T15 — Download modello + detect + self-test

- Scope: path `Pickarr__Models__Path`; RecoModelDownloadJob; hash; self-test; stato RecoState.ModelStatus; admin POST model/download|detect
- Requisiti: FR-REC-ML-01..03,05; UC-REC-05
- Acceptance: Absent→Downloading→Ready|Error; Error non spegne L0/L1; copia manuale+detect; test condizionale skip se no file
- Verifica: test condizionali + manual checklist
- Contesto: esito T13
- Assunzioni: K-014 confermata
- Rollback: disabilitare embeddings.enabled

### T16 — TitleEmbeddings + RecoEmbeddingJob

- Scope: tabella embeddings; testo BR-REC-09; TextHash; job batch+throttle; coverage in RecoState
- Requisiti: FR-REC-ML-*; §11.3
- Acceptance: stesso testo→stesso vettore; ricalcolo se hash cambia; coverage aggiornata; coda ml
- Verifica: test embedding sintetico/condizionale
- Contesto: nessuno
- Assunzioni: nessuna
- Rollback: drop embeddings; coverage=0

### T17 — kNN L2 + similar embedding + build soglia coverage

- Scope: Core kNN §12.4; build attiva L2 se coverage≥minCoverage; similar preferisce embedding
- Requisiti: FR-REC-RC-09; FR-REC-ML-03
- Acceptance: sotto coverage usa L1; kNN test sintetico; similar usa embedding se presenti
- Verifica: Core.Tests knn
- Contesto: nessuno
- Assunzioni: nessuna
- Rollback: b=0 forzato

### T18 — MMR + explore + onboarding /taste

- Scope: MMR/explore in lettura §12.8–12.9; GET onboarding; pagina `/taste` + POST onboarding batch una rebuild
- Requisiti: FR-REC-RC-10,11; FR-REC-RT-09
- Acceptance: diversify default; explore rate 0 disattiva; onboarding 24 titoli pool enabled; test determinismo seme Version
- Verifica: Core MMR/explore tests + FE taste
- Contesto: nessuno
- Assunzioni: nessuna
- Rollback: diversify=false default temporaneo solo se regressione

### T19 — Pannello Settings Recommendations + admin actions

- Scope: `/settings/recommendations`; status modello/pool/voti; azioni rebuild/embeddings/reset; SyncRuns kind visibili in Jobs; testo calcolo locale
- Requisiti: FR-REC-AD-01,02,04,06; FR-REC-ST-02..04
- Acceptance: UI mostra stato; reset derived vs includeRatings con conferma; 202+run id; i18n
- Verifica: npm test settings reco; integration admin reco
- Contesto: nessuno
- Assunzioni: nessuna
- Rollback: nascondere tab

## TR-09 — R4 misura e livello 3

### T20 — RecoEvalJob leave-one-out + report

- Scope: eval §17.3; POST evaluate; GET evaluations/latest; warning <30 voti
- Requisiti: FR-REC-AD-03
- Acceptance: report metriche per livello; SyncRun RecoEval; test con fixture sintetica
- Verifica: infra/integration eval
- Contesto: nessuno
- Assunzioni: nessuna
- Rollback: rimuovere job/endpoints

### T21 — Livello 3 classificatore ML.NET

- Scope: training logistico se minRatings; integrazione Personal se soglia; setting ml.minRatings
- Requisiti: FR-REC-ML-06; utente: implementare sempre in R4
- Acceptance: codice L3 presente; attivazione solo se batte L2 in eval (gate runtime); test con dataset sintetico minimo; sotto soglia L3 inattivo
- Verifica: test unit L3 + eval confronta
- Contesto: D-REC-12 vs override utente — prevale utente (STATUS D-00x)
- Assunzioni: nessuna
- Rollback: setting disabilita L3

### T22 — RecoEventLog + SP-R4 taratura

- Scope: tabella/eventi se enabled; artifacts/SP-R4.md taratura a/b/N0/γ/K; eventuale update default settings
- Requisiti: FR-REC-AD-05; SP-R4
- Acceptance: events default off; con enabled log Shown/Opened/Liked/…; report taratura; defaults aggiornati solo con nota in §4
- Verifica: test events off-by-default; artefatto SP-R4
- Contesto: nessuno
- Assunzioni: nessuna
- Rollback: events.enabled=false

## TR-10 — Documentazione prodotto

### T23 — Aggiornare documento-funzionale e specifica-tecnica

- Scope: `docs/documento-funzionale.md`, `docs/specifica-tecnica.md` secondo §21; riferimenti D-REC-01..13 e path WebApp corretti
- Requisiti: R-DOCS; V-REC-08
- Acceptance: decisioni e tabelle/API/job/route allineate all’implementato; niente path `frontend/` errati
- Verifica: review diff docs vs codice
- Contesto: A-006
- Assunzioni: nessuna
- Rollback: revert docs

## TR-11-FIX — Correttivo post T24 FAIL

### T25 — Settings toggles model + explore (+ Avanzate / include library)

- Scope: `RecoSettingsDto`/`RecoUpdateDto`/`SettingsService`; `RecommendationsSettingsPage` + i18n; `ForYouPage` `includeInLibrary`; test Settings + page
- Requisiti: FR-REC-ST-02 (M); FR-REC-ST-04 (S); FR-REC-RC-07 interruttore libreria UI
- Acceptance: GET/PUT espongono `embeddingsEnabled` e explore (`exploreRate` o toggle); UI settings ha interruttori model+explore; sezione Avanzate per pesi/MMR/exploreRate; For you può includere titoli in libreria; test PASS
- Verifica: `dotnet test` filter Settings*; npm RecommendationsSettingsPage/ForYou*; typecheck
- Contesto: F-006; artifacts/T24-verifica-finale.md
- Assunzioni: default exploreRate 0.10; explore off ⇒ rate 0
- Rollback: revert DTO/UI

## TR-11 — Verifica finale indipendente

### T24 — Verifica finale indipendente

- Scope: intero perimetro R1–R4 + docs; test; config; requisiti ANALYSIS §1/§7
- Requisiti: tutti obbligatori; rinvii solo se approvati
- Acceptance: revisore contesto separato; PASS su SHA+snapshot; rapporto in ARCHIVE.md; STATUS §9 aggiornato; nessun difetto bloccante
- Verifica: protocollo skill FASE verifica finale; no correzioni del revisore
- Contesto: dipende TR-01..TR-10, T25 (dopo FAIL tentativo 1)
- Assunzioni: nessuna
- Rollback: N/A (sola verifica)
