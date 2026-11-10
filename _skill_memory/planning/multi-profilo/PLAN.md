# PLAN — multi-profilo

Scope e acceptance. Stato, dipendenze e size: STATUS.md §1.

## TR-01 — Schema e admin

### T01 — Profilo, migrazione, backfill admin

- Scope: entità `Profile`; `PickarrDbContext`; configurazione EF; migrazione nuova in `src/Pickarr.Infrastructure/Persistence/Migrations` più snapshot generato; `DatabaseInitializer` (seed se la tabella profili è vuota); `Favorite`, `Watched`, `Rating`, `RatingEvent`, `Recommendation`, `RecoState`, `RecoEventLog`, `ArrAuditLog` solo per aggiungere `ProfileId` nullable in questa tranche se il backfill SQL lo richiede. I service restano compilanti: se la chiave cambia qui, i chiamanti si aggiornano il minimo per compilare e i test esistenti passano contro l'admin. Non cambiare il contratto HTTP.
- Requisiti: R-01, R-02, K-006.
- Acceptance:
  - Dopo `MigrateAsync` su un DB che già ha preferiti/voti/visti/reco/audit, esiste un solo profilo admin di nome Admin e quelle righe hanno il suo `ProfileId`.
  - `ArrLibraryItem`, `Title`, `Availability`, `TitleEmbedding`, `AppSetting` non hanno `ProfileId`.
  - Un DB nuovo, passato da `DatabaseInitializer`, ha lo stesso admin anche se il seed e la migrazione corrono in ordine diverso.
  - Non si può avere due `IsAdmin` dallo schema o dal seed.
- Verifica: `dotnet ef` (API ferma) e un test Infrastructure che applica la migrazione su uno SQLite con righe vecchie, più un test che lo schema `EnsureCreated` più il seed produce l'admin. Comando filtrato sul test nuovo.
- Contesto: A-001, A-004, D-002, D-003, D-011, F-001 non tocca questi file.
- Assunzioni: K-006. Se l'audit storico non va migrato, fermarsi e aggiornare D-011.
- Rollback: rimuovere la migrazione generata e le entità aggiunte in questo task. Non toccare il worktree preesistente.

## TR-02 — Servizi dati personali

### T02 — Preferiti, voti e visti per profilo

- Scope: `Title` (collezioni al posto dei riferimenti singoli), configurazioni Favorite/Watched/Rating/RatingEvent, `FavoriteService`, `RatingService`, `WatchedService` e le interfacce Core, test `FavoriteServiceTests` e `RatingServiceTests`, altri chiamanti che non compilano. Gli endpoint, se compilano solo fissando l'admin, lo fanno in modo esplicito e temporaneo fino a T04.
- Requisiti: R-05.
- Acceptance:
  - Lo stesso `TitleId` accetta preferito, voto e visto distinti per due profili.
  - Togliere il preferito o il voto di un profilo non modifica l'altro.
  - `RatingEvent` registra il `ProfileId`.
  - I test preesistenti di questi service passano, con un profilo esplicito.
- Verifica: `dotnet test tests/Pickarr.Infrastructure.Tests --filter FullyQualifiedName~FavoriteServiceTests|FullyQualifiedName~RatingServiceTests` e i test nuovi dello stesso progetto. API ferma se il build tocca i binari Api.
- Contesto: K-001, D-003.
- Assunzioni: nessuna.
- Rollback: ripristinare i service e le configurazioni di questo task.

## TR-03 — API profili e header

### T03 — API profili e invarianti

- Scope: endpoint nuovi `/api/v1/profiles` in `src/Pickarr.Api/Endpoints`, DTO in Core, servizio Infrastructure, registrazione in `Program.cs`, test Integration dedicati. Collezione test propria se lo stato admin/profili collide con la collezione `api`.
- Requisiti: R-01, R-03, R-04.
- Acceptance:
  - GET elenca i profili senza header.
  - POST crea un profilo solo se `X-Pickarr-Profile` è l'admin. Altrimenti 403. All'undicesimo, 409.
  - PATCH sul proprio id aggiorna nome, colore, `UseImplicit`, `ExcludeInLibrary` e rifiuta un cambio di `IsAdmin`.
  - PATCH su un altro id richiede l'admin dichiarato.
  - DELETE dell'admin è 409. DELETE di un altro, con admin dichiarato, rimuove i suoi preferiti, voti, eventi, visti, raccomandazioni, stato reco e log reco. L'audit Arr di quel profilo resta, con `ProfileId` azzerato.
  - Nome vuoto, oltre 40, duplicato o colore fuori palette: 400.
- Verifica: `dotnet test tests/Pickarr.Integration.Tests` filtrato sui test nuovi. API ferma prima del build.
- Contesto: D-002, D-008.
- Assunzioni: nessuna.
- Rollback: rimuovere endpoint, servizio e test di T03.

### T04 — Header sulle mutazioni e liste personali

- Scope: endpoint favorites, ratings, watched e le liste `GET /favorites`, `GET` valutazioni, `GET /recommendations` se già leggono il singleton. Il client web non è in questo task: i test API inviano il header. Fino a T08 l'app può continuare a non inviarlo; allora le mutazioni rispondono 400. Per non lasciare l'app muta, lo stesso task aggiunge il header nel client fetch delle sole API personali, leggendo `localStorage` `pickarr.profileId` e, se assente, l'id admin restituito da GET `/profiles`.
- Requisiti: R-06.
- Acceptance:
  - PUT/DELETE preferito, voto e visto senza header: 400, nessuna scrittura.
  - Con header di un profilo, la scrittura è solo sua.
  - GET delle liste personali senza header: 400. Con header: solo le righe di quel profilo.
  - Le API settings senza header restano 200.
- Verifica: test Integration dei casi 400 e del isolamento; test frontend del client solo se il wrapper è sotto `src/Pickarr.WebApp/src/api`. `npm test -- --run` sui file toccati.
- Contesto: D-004, D-007.
- Assunzioni: nessuna.
- Rollback: header opzionale rimosso dai test e dal client; gli endpoint tornano al profilo admin esplicito di T02.

## TR-04 — Flag nel catalogo

### T05 — DTO catalogo e scheda col profilo

- Scope: `TitleQueryService` e gli endpoint catalogo/titoli che riempiono `isFavorite`, voto e visto. Test `TitleQueryServiceTests` e i test API di catalogo che asseriscono quei flag.
- Requisiti: R-07.
- Acceptance:
  - Con header, i flag sono del profilo dichiarato, anche se l'altro profilo ha valori diversi sullo stesso titolo.
  - Senza header il catalogo risponde 200 e i flag personali sono assenti o falsi, senza prendere l'admin in silenzio.
  - Il filtro preferiti della lista personale resta su `/list` e sulle API favorites. Il catalogo continua a ignorare `favorite=true`.
- Verifica: `dotnet test tests/Pickarr.Infrastructure.Tests --filter FullyQualifiedName~TitleQueryServiceTests` e i test Integration di catalogo toccati.
- Contesto: D-007, confini catalogo/lista in AGENTS.md.
- Assunzioni: nessuna.
- Rollback: flag di nuovo calcolati sull'admin, solo se T04 è ancora il chiamante.

## TR-05 — Raccomandazioni per profilo

### T06 — Build, stato e segnali Arr per profilo

- Scope: `RecoState` (riga per profilo, D-012), `Recommendation`, `RecoEventLog`, `ArrAuditLog.ProfileId`, `RecoBuildService`, `RecoQueryService`, `RecoEvalService`, `RecoModelService`, `RecoEmbeddingService`, `RecoAdminEndpoints`, test reco build/eval/event. `useImplicit` e `excludeInLibrary` restano letti dalle chiavi globali (D-010). I campi sul profilo esistono da T03 ma il build non li legge ancora.
- Requisiti: R-08. Non include R-09 né R-10.
- Acceptance:
  - Due profili con voti diversi ottengono `Recommendations` e `ProfileJson` diversi.
  - Lo stato modello si aggiorna sulla riga admin e i build degli altri profili lo leggono da lì.
  - Un Add Arr con header entra nei segnali impliciti solo di quel profilo. Un Add senza header non entra in nessun profilo. Le righe migrate restano sull'admin.
  - Downloaded segue il profilo dell'Add attribuito. Delete non cancella il segnale.
  - Un voto programma il build di quel profilo, non quello degli altri.
  - Con i dati migrati sull'admin e le stesse chiavi globali, l'ordine dei consigli dell'admin resta quello del modello attuale sui test già presenti.
- Verifica: `dotnet test tests/Pickarr.Infrastructure.Tests --filter FullyQualifiedName~RecoBuildServiceTests` e i test Core di `RecoSignals` se il filtro Arr cambia la firma dei fact. API ferma.
- Contesto: A-002, A-003, K-002, K-005, D-010, D-011, D-012.
- Assunzioni: K-006 già chiusa in T01.
- Rollback: build di nuovo singleton sull'admin; `ProfileId` audit ignorato dal calcolo.

## TR-06 — Prior di casa

### T07 — Prior di casa sotto N0

- Scope: funzione pura accanto a `RecoLearning` / `RecoSignals`, chiamata da `RecoBuildService`. Test Core della formula. Nessuna chiave settings nuova. Non attivare il kNN sul vettore di casa.
- Requisiti: R-10, D-006.
- Acceptance:
  - Nessun Love/Like altrui, qualunque `N_eff`: i punteggi coincidono con `FuseScore` attuale.
  - `N_eff = 0` e un altro profilo con Love: il titolo sale rispetto al solo prior, e il prior TMDB pesa ancora 0,5 dello slot non personale.
  - `N_eff ≥ N0`: il termine di casa è 0.
  - Un Dislike altrui non abbassa il termine di casa. I preferiti altrui nemmeno.
  - Il profilo non usa i propri voti come prior di casa.
  - Un voto su A ricostruisce A e i profili con `N_eff < N0`. Non ricostruisce un profilo già a `N_eff ≥ N0`.
- Verifica: `dotnet test tests/Pickarr.Core.Tests` filtrato sulla nuova classe, più il build test Infrastructure del caso a freddo.
- Contesto: K-002, D-006.
- Assunzioni: nessuna. Se l'utente rifiuta il tetto 0,5, il task resta TODO e la formula si aggiorna in D-006 prima di scrivere codice.
- Rollback: tornare a `FuseScore(prior, personal, personalWeight)` senza termine di casa.

## TR-07 — Interfaccia profili

### T08 — Selettore, memoria, guard settings

- Scope: `router.tsx`, `Header.tsx`, nuova rotta `/who`, contesto profilo, `sourcePreference.ts` e i suoi test, `en.json`, test di routing. Rispettare il diff già presente (F-001): non rigenerare i file.
- Requisiti: R-11, R-12, D-005, D-009.
- Acceptance:
  - Un solo profilo: l'ingresso va al catalogo e il profilo attivo è quello.
  - Due profili e id ricordato valido: ingresso al catalogo con quel profilo.
  - Id ricordato cancellato: `/who`.
  - Il controllo in header apre `/who` anche se un profilo è ricordato.
  - Profilo non admin: niente link Settings; `/settings` e sottopagine reindirizzano a `/catalog`.
  - Profilo admin: Settings resta raggiungibile.
  - La sorgente catalogo è per profilo; l'admin senza chiave propria eredita una volta `pickarr.sourceId`.
  - Verifica manuale nel browser sui tre ingressi, sul cambio profilo e sul redirect settings. I test Vitest coprono il gate e il redirect, non solo il render.
- Verifica: `npm test -- --run`, `npm run typecheck`, `npm run lint` in `src/Pickarr.WebApp`. Poi il browser sull'app avviata.
- Contesto: F-001, D-004, D-005, D-009.
- Assunzioni: nessuna.
- Rollback: ripristinare solo l'hunk di questo task nei file già sporchi.

### T09 — Gestione profilo e preferenze reco

- Scope: UI su `/who` per creare, eliminare, rinominare, scegliere il colore e i due toggle; `SettingsPage.tsx`, `RecommendationsSettingsPage.tsx` e i test, da cui quei due controlli escono; il build e la query reco passano a leggere i campi del profilo. Copy in `en.json`. F-001.
- Requisiti: R-09, R-13, D-010.
- Acceptance:
  - Admin: crea fino a 10, elimina gli altri, modifica tutti. Non elimina sé stesso e non vede un'azione di cessione ruolo.
  - Profilo non admin: modifica solo nome, colore e i due toggle propri. Non vede crea/elimina.
  - `useImplicit` false su un profilo toglie i segnali impliciti solo dai suoi consigli.
  - `excludeInLibrary` false include la libreria solo nella sua lettura Per te.
  - Le chiavi globali, se ancora inviate da un client vecchio, non cambiano il calcolo.
  - Browser: creare un secondo profilo, entrare, verificare che la sua lista sia vuota mentre quella admin no, e che Settings non sia raggiungibile.
- Verifica: test Vitest delle due pagine settings e di `/who`; test Infrastructure o Integration che i due profili divergono sul toggle; browser come sopra.
- Contesto: D-008, D-010, FLUSSO-05.
- Assunzioni: nessuna.
- Rollback: i toggle tornano sulle pagine settings e il build rilegge le chiavi globali.

## TR-08 — Documenti

### T10 — Allineare docs e AGENTS.md

- Scope: `docs/documento-funzionale.md`, `docs/specifica-tecnica.md`, `docs/raccomandazioni.md`, sezione confini di `AGENTS.md`. Merge col diff preesistente, senza cancellarlo.
- Requisiti: R-14.
- Acceptance: i quattro file descrivono profili, massimo 10, migrazione sull'admin, header, divieto settings solo UI, preferenze per profilo, prior di casa D-006, libreria Arr condivisa con audit attribuito. Le formule `N_eff` e `FuseScore` restano, con il termine di casa aggiunto dove si descrive il punteggio.
- Verifica: lettura dei punti toccati. Nessun test automatico. Segnare la verifica come lettura, non come PASS di suite.
- Contesto: F-001.
- Assunzioni: nessuna.
- Rollback: hunk documentale di T10 soltanto.

## TR-09 — Verifica finale indipendente

### T11 — Verifica finale indipendente

- Scope: requisiti R-01..R-14, codice, test e configurazione della versione identificata. Nessuna modifica a codice, test o piano da parte del revisore.
- Requisiti: ANALYSIS.md §1 e §7.
- Acceptance: criteri obbligatori verificati, D-006 e D-008 confermati o aggiornati prima dell'esecuzione, nessun difetto bloccante, rapporto indipendente PASS in ARCHIVE.md e riepilogo in STATUS.md §9.
- Verifica: protocollo finale della skill. Contesto separato. Se non disponibile: handoff a una sessione nuova, task BLOCKED, niente autoverifica.
- Evidenze: un rapporto per ogni tentativo in ARCHIVE.md.
- Contesto: tutte le D- e F-001.
- Assunzioni: nessuna lasciata aperta tra K-006 e le decisioni.
- Rollback: non applicabile. Il revisore non modifica il codice.
