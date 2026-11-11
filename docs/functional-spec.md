# Pickarr — Functional specification

| | |
|---|---|
| **Version** | 0.3 |
| **Date** | 2026-11-10 |
| **Language** | English |
| ** Project name ** | *Pickarr* is a provisional name (see D-08) |
| **Linked documents** | `technical-spec.md`, `recommendations.md` (ratings and *For you*) |

---

## Table of contents

1. Introduction
2. Context and constraints of data sources
3. Functional overview
4. Functional requirements
5. Non-functional requirements
6. Business rules
7. User stories
8. Use cases
9. Screens
10. Out of scope and evolutions
11. Assumptions, risks, and open decisions
12. Release phases

---

## 1. Introduction

### 1.1 Scopo

Pickarr is a **self-hosted * * application without authentication. An installation hosts up to **10 profiles** (separate tastes and lists); catalogue, integrations and Sonarr/Radarr library remain shared. Maintains an up-to-date catalog of content available on a streaming service (initially * * Netflix * *) in a given * * country * * and allows you to browse, search and save it among the favorites of the active profile. From here you can send a title to **Sonarr** (series) or **Radarr** (movies), see the status of the shared library and remove it.

### 1.2 Obiettivi

- Have in a single interface, similar to the Netflix homepage, the titles available in a country, with description and votes.
- Go from "title that interests me" to "title monitored and searched on Sonarr/Radarr" in a few clicks.
- See at a glance what is already in the library and in what state, and be able to eliminate it.
- Mark *Likes* / *Dislikes* on titles and receive local recommendations (*For you*) filtered on imported catalogs.
- Operate within the limits of Watchmode's **free * * plan (2,500 credits/mese, max 3 countries).
- To be able to add other services (Amazon Prime, Disney+, Hulu, ...) and other countries without changing the architecture.

### 1.3 Ambito

**Incluso (v1)**

- Servizio Netflix, paesi Italia (IT) e Stati Uniti (US).
- Types of content: movies, TV series, stand-up.
- Scheduled synchronization of the catalogue with availability history.
- Arricchimento metadati da TMDB (e id incrociati IMDb/TVDB).
- UI web desktop, tema scuro, interfaccia internazionalizzata (inglese di default).
- Server-side search with filters, favorites, detail tab.
- Integration with a Sonarr instance and a Radarr instance: addition, status, deletion.
- Profiles (up to 10) with ⟦0 selector⟧, profile memory in the browser, personal data for profile and cold house prior (detail in `recommendations.md`).
- Administration: settings, job control, Watchmode credit consumption (admin profile in UI only).
- Local recommendations (R1–R4): title rating per profile, *For you* page, similar, optional ONNX embedding, offline rating, gated supervised level (detail in `recommendations.md`).
- Distribution as a single Docker image.

**Excluded (v1)**: see §10. Custom recommendations are no longer out of scope: they are specified in `recommendations.md` and released after the catalog MVP (steps R1–R4 in §12).

### 1.4 Attori

| Plaintiff | Description |
|---|---|
| **Profile** | Person using the installation. Choose profile to `/who` (no login). Explore, search, manage your favorites/voti, add and delete titles. |
| ** Admin Profile ** | Only profile with `IsAdmin`. Created on migration/seed with the name "Admin" (renamable). Recipient of pre-existing personal data. Manages settings, jobs, and other profiles. The role is not relinquished and the profile is not deleted. |
| **Scheduler** | Internal component that executes jobs (catalog sync, enrichment, Sonarr/Radarr sync). |
| **Watchmode** | Catalog and availability source for service/paese. |
| **TMDB** | Source of external metadata and IDs. |
| **Sonarr / Radarr** | Sistemi esterni di gestione libreria serie / film. |

### 1.5 Glossario

| Termine | Significato |
|---|---|
| **Servizio (Provider)** | Servizio di streaming, es. Netflix. |
| ** Catalogue source ** | Pair *service + country*, e.g. "Netflix IT". It's the drive you sync and browse on. |
| **Title** | Movie, series or stand-up. It has a unique identity independent of the country. |
| **Availability** | Relationship between a title and a catalog source, with its dates (addition, removal, expiration). |
| **Baseline** | First complete import of a source. The titles present at that time are not considered "news". |
| **Credits** | Watchmode API consumption units (2,500/mese in free plan). |
| **Stato libreria** | Stato di un titolo in Sonarr/Radarr: non in libreria, mancante, in download, scaricato, parziale. |
| **Profile (home account)** | Taste partition: name, icon color, favorites, votes, views, recommendations and two preferences reco (`useImplicit`, `excludeInLibrary`). It is not an authenticated account. |
| **Rating * * | Explicit preference on the title by profile: *I like it very much */* I like it */ * I don't like it* / * I don't care*. Independent of source; two profiles may diverge on the same title. |
| **For you** | Active profile recommendation list, calculated locally on the enabled catalog pool, filterable by source or merge. |
| **Pool** | Merging titles with at least one source availability (service+country) enabled. ⟦0 does⟧ not exclude. |
| **Prior of the house** | Cold contribution to the recommendations of a profile with few signals, built by the *Likes* / *I really like * of the other profiles (ceiling 0.5; zero to `N_eff ≥ N0`). |

---

## 2. Data-source context and constraints

The checks were made on Watchmode's public documentation on 2026-11-03. These constraints have a direct impact on design.

| Constraint | Product impact |
|---|---|
| Netflix does not offer a public API for the catalog. | The source is Watchmode. |
| **Developer (free)** plan: 2,500⟦ 0 credits⟧ shared across all calls, up to 3 countries. | We need a credit budget with sync that can be resumed over several days (FR-CAT-07). With IT+US you use 2 of the 3 available countries. |
| **Non-commercial * * use with **attribution** to Watchmode.com. | Link/credit to Watchmode visible in UI (FR-MET-07). Suitable for personal self-hosting. |
| On the free plan, cached data must be **refreshed or deleted within 30 days**. | The full sync must run at least every 30 days (default: every 7). If it fails for a long time, the UI warns (BR-12). |
| ** Change feeds ** (new titles, changed sources) and ** release dates ** are only on paid plans. | On the free plan, the "incremental" sync is based on recent pages of the title list, and the removal is discovered by comparing complete lists (BR-05). |
| The ** expiration dates ** (out-of-service titles) on the free plan are just a preview of the next 10 per country. | The "Due" section is limited on the free (D-07) plan. |
| TMDB: Free API for non-commercial use with attribution. | Primary source by plot, genres, posters, ratings, IMDB/TVDB id. |
| Sonarr uses id **TVDB**, Radarr uses id **TMDB**. | For series you need the TVDB id, obtained from TMDB (external ids). Titles without usable id are not addable (BR-08). |

> **Note on receivables.** Some calls cost more than one credit. The actual cost per call must be measured in the initial spike (SP-1 in the technical specification), because job sizing depends on it.

---

## 3. Functional overview

```
Watchmode ──(job schedulato)──► Catalogo locale ◄──(arricchimento)── TMDB
                                       │
                                       ▼
              UI: catalogo, lista, For you, valutazioni, dettaglio
                                       │  (voto locale + conferma Arr)
                                       ▼
                    Backend ──► Sonarr / Radarr   (+ coda ml ONNX)
                                       ◄── stato (sync periodica + webhook)
```

Macro-funzioni:

1. **Catalogo**: sincronizzazione, storico, budget crediti.
2. **Metadata**: TMDB enrichment with configurable language and English fallback.
3. **Esplorazione**: griglia, ricerca, filtri, dettaglio.
4. **Preferiti** (`/list`) e **voti** (*Like* / *Dislike*).
5. ** Local recommendations ** (*For you*, similar, optional embedding).
6. **Integrazione Sonarr/Radarr**: aggiunta, stato, eliminazione.
7. **Amministrazione**: impostazioni, job, crediti, azioni reco.
8. **Internazionalizzazione** dell'interfaccia (inglese).
9. **Multi-service and multi-country** as architecture requirement.

---

## 4. Functional requirements

Priority (MoSCoW): **M** = Must (MVP), **S** = Should, **C** = Could, **W** = Won't (for now).

### 4.1 Catalogo e sincronizzazione (FR-CAT)

| ID | Requirement | PRI |
|---|---|---|
| FR-CAT-01 | The system maintains a catalog for each ** catalog source ** (service + country) enabled. At the first installation, Netflix IT and Netflix US are preconfigured. | M |
| FR-CAT-02 | User can enable/disabilitare source, choose metadata language and sync frequencies. | M |
| FR-CAT-03 | * * Movie **, ** series ** and ** stand-up ** types are imported. The other types (anime, kids) are not imported into v1, but the model must be able to accommodate them without destructive migrations. | M |
| FR-CAT-04 | ** Incremental sync ** scheduled (cron configurable by source): intercepts recently added titles. It is idempotent and can be re-executed at any time. | M |
| FR-CAT-05 | ** full sync ** periodic (weekly default, configurable): realigns the entire catalog and detects **removals** (BR-05). | M |
| FR-CAT-06 | The system keeps the ** availability history **: first sighting, last confirmation, date of addition, date of removal, possible expiration date, plus an event log (added / removed / re-added /announced expiration). | M |
| FR-CAT-07 | **Watchmode credit budget**: the system never exceeds the configured monthly budget (default: plan quota minus a reserve), limits credits per single execution and **resumes** executions interrupted in the following days. Consumption is always visible. | M |
| FR-CAT-08 | **Baseline**: Upon completion of the first complete sync of a source, all titles present are marked as baseline and do not appear as "novelty". | M |
| FR-CAT-09 | Each sync run is tracked (type, start, end, outcome, pages read, credits used, titles added/aggiornati/rimossi, errors) and can be consulted by the administration. | M |
| FR-CAT-10 | ** Abnormal Removal Protection **: If a full sync would indicate removal of an excessive percentage of the catalog, the removals are not applied and the run is marked "to be verified". | M |
| FR-CAT-11 | Sync ** due dates ** when source makes them available (preview on free plan, full calendar on paid plans). | S |
| FR-CAT-12 | Securities from which a TMDB id cannot be obtained are recorded as * * unmapped * * and can be consulted by the administration. | S |
| FR-CAT-13 | Incremental sync via Watchmode's * * change feed * * if the user claims to have a paid plan (“Watchmode plan” setting). | C |

### 4.2 Metadati (FR-MET)

| ID | Requirement | PRI |
|---|---|---|
| FR-MET-01 | For each title the system obtains from TMDB: localized and original title, plot, year, duration (film) or number of seasons/episodi (series), genres, posters, backdrop, TMDB rating, IMDB id and TVDB id (series). | M |
| FR-MET-02 | The metadata language is configurable by source (e.g. `it-IT` for Netflix IT, `en-US` for Netflix US). If the title or plot is missing in the chosen language, * * English ** is used as the fallback. | M |
| FR-MET-03 | The tab shows external links to **TMDB** and **IMDB** (if the id is known). Link to TVDB for series is optional. | M |
| FR-MET-04 | IMDB rating shown next to TMDB rating. Requires an optional additional integration (OMDb, with its own API key, see D-03). Without it, only the TMDB rating is shown. | S |
| FR-MET-05 | Metadata is updated periodically (default: every 30 days for active titles) and upon request. | M |
| FR-MET-06 | Images are served directly from TMDB URLs (hotlinks). No local copy. | M |
| FR-MET-07 | The interface shows the **attributions** required: Watchmode.com (with link) and TMDB (logo and wording "This product uses the TMDB API but is not endorsed or certified by TMDB"). | M |
| FR-MET-08 | A title whose enrichment fails is still shown with the minimum data (title and year from Watchmode) and tried again later. | S |

### 4.3 Home e navigazione (FR-UI)

| ID | Requirement | PRI |
|---|---|---|
| FR-UI-01 | ** Desktop only ** interface (minimum supported width 1280px) with ** dark theme **. | M |
| FR-UI-02 | A selector in the header allows you to choose the catalog source (service + country). The choice is remembered between one session and another. | M |
| FR-UI-03 | Home is a ** grid view ** of the catalog of the selected source, with search bar and quick filters: *All*, * New *, *Expiring*, * Recently removed *. With the recommendations active, a * * dedicated line * * of recommendations for that source (max 25) appears under * All * (without search). *My list* and *For you* (union of sources) are dedicated routes (`/list`, `/for-you`). | M |
| FR-UI-04 | Each card shows poster, title, year, type badge (movie/serie/stand-up), ** library status ** badge, favorite icon, and **vote** checks (* Like */ *Dislike*) when recommendations are enabled. | M |
| FR-UI-05 | The grid uses infinite scrolling with page loading. It must remain fluid with catalogs of a few thousand titles. | M |
| FR-UI-06 | Clicking on a card opens the * * detail card * * in a modal window, which can also be reached via a direct URL. | M |
| FR-UI-07 | A **hero banner** at the top of the home with a featured title (e.g. latest news with backdrop). | C |
| FR-UI-08 | Ordinamento selezionabile (vedi FR-SRC-03). | M |

### 4.4 Ricerca e filtri (FR-SRC)

| ID | Requirement | PRI |
|---|---|---|
| FR-SRC-01 | Text search **server-side * * on the localized title and on the original one, insensitive to capitalization and accents, with debunking on the UI side. | M |
| FR-SRC-02 | Combinable filters: **type** (movie/serie/stand-up), **genre** (multi-selection), **year** (range), ** library status ** (not in library, missing, downloaded, downloaded, partial), ** favorites only **, **include removed**. | M |
| FR-SRC-03 | Sorts: relevance (with search), date added to the service, year, grade, title A→Z. | M |
| FR-SRC-04 | Minimum grade filter. | C |
| FR-SRC-05 | Filters are reflected in the URL (shareable, back button compatible). | S |
| FR-SRC-06 | The result count is shown in UI. | S |

### 4.5 Scheda di dettaglio (FR-DET)

| ID | Requirement | PRI |
|---|---|---|
| FR-DET-01 | Show card: backdrop/poster, title, original title, year, duration (movie) or number of seasons (series), genres, storyline, TMDB rating (and IMDB if available), external links. | M |
| FR-DET-02 | Shows * * availability ** information in the selected source: available from, expiring on, removed on. | M |
| FR-DET-03 | Shows the ** library status **; for series the detail **season by season**. | M |
| FR-DET-04 | Stocks: favorite, **vote** (*Like* / *Dislike*), add to Sonarr/Radarr, delete from Sonarr/Radarr, update status. | M |
| FR-DET-05 | "Search now" action on a title already in the library (launches the search in Sonarr/Radarr). | S |
| FR-DET-06 | Link to open the title in the Sonarr/Radarr web interface. | C |
| FR-DET-07 | If the title is available in other configured sources (e.g. also on Netflix US), the tab indicates this. | C |

### 4.6 Preferiti (FR-FAV)

| ID | Requirement | PRI |
|---|---|---|
| FR-FAV-01 | The active profile can⟧ add⟦1 title from card and card favorites. | M |
| FR-FAV-02 | Favorites and **rating** are from the **profile** (profile key +title), not the source. Two profiles can diverge on the same title. The ratings are *I like it very much* / *I like it* / * I don't like it* / * I don't care*. | M |
| FR-FAV-03 | The * My List* (`/list`) page shows active, cross-provider, and cross-country profile favorites, each with a badge against the selected source (present / removed / absent). It is not a view `view=favorites` of the catalogue. | M |
| FR-FAV-04 | Bulk add favorites to Sonarr/Radarr. | C |

### 4.7 Aggiunta a Sonarr/Radarr (FR-ADD)

| ID | Requirement | PRI |
|---|---|---|
| FR-ADD-01 | For each title not in the bookstore, the * * Add * * action appears. The destination is determined by the type: Sonarr → series, Radarr → film. The stand-ups follow the TMDB type of the title (Radarr → film, Sonarr → series). | M |
| FR-ADD-02 | The action opens a ** confirmation dialog with the ** options, preset with the default values. Nothing is sent until the user confirms. | M |
| FR-ADD-03 | ** Sonarr ** options: quality profile, root folder, monitoring mode (all episodes, only future, missing, existing, first season, last season, most recent season, only pilots, recent, none), monitoring of new seasons, series type (standard/daily/anime), folder per season, tags, **search for missing episodes immediately **. | M |
| FR-ADD-04 | * *Radarr** options: quality profile, root folder, monitored yes/no, minimum availability (announced / at the cinema / out), tags, **look for the movie right away **. | M |
| FR-ADD-05 | Quality profile and root folder are preselected with default values (see D-01). All other options are chosen by the user. | M |
| FR-ADD-06 | If the instance is not configured, unreachable, or rejects credentials, the action is blocked with a clear message and a link to the settings. | M |
| FR-ADD-07 | If the title is already present in the instance, the Add action is not offered: it shows the status. | M |
| FR-ADD-08 | Se per il titolo manca l'id necessario (TVDB per le serie, TMDB per i film), l'azione è disabilitata con una spiegazione. | M |
| FR-ADD-09 | Dopo l'aggiunta l'interfaccia mostra subito il nuovo stato e un messaggio di esito. Eventuali errori restituiti da Sonarr/Radarr sono mostrati in forma leggibile. | M |
| FR-ADD-10 | Il dialog ricorda le ultime opzioni scelte (tranne il profilo/root di default). | C |

### 4.8 Stato libreria (FR-ST)

| ID | Requisito | Pri |
|---|---|---|
| FR-ST-01 | Ogni titolo ha uno stato libreria: **Non in libreria** (default, non mostrato come badge), **Mancante**, **In download** (con percentuale), **Scaricato**, **Parziale** (solo serie). In UI il badge compare solo per stati diversi da *Non in libreria*. Indicazione separata di **monitorato / non monitorato**. | M |
| FR-ST-02 | Lo stato è visibile su card e scheda ed è usabile come filtro. | M |
| FR-ST-03 | Per le serie, la scheda mostra per ogni stagione: monitorata sì/no, episodi scaricati su totali e stato. | M |
| FR-ST-04 | Lo stato proviene da una **cache** aggiornata da una sync periodica (default: ogni 15 minuti) e, se configurati, dai **webhook** di Sonarr/Radarr per aggiornamenti quasi in tempo reale. | M |
| FR-ST-05 | Il match tra titolo del catalogo e libreria avviene per **id TMDB / TVDB**, quindi vale anche per elementi presenti in Sonarr/Radarr ma non aggiunti da Pickarr. | M |
| FR-ST-06 | L'utente può forzare l'aggiornamento dello stato di un titolo. | M |
| FR-ST-07 | Se l'istanza è irraggiungibile, resta l'ultimo stato noto e la UI segnala che è obsoleto (con ora dell'ultimo aggiornamento). | M |
| FR-ST-08 | Aggiornamento in tempo reale della UI (senza ricaricare) alla variazione di stato. | C |

### 4.9 Eliminazione da Sonarr/Radarr (FR-DEL)

| ID | Requisito | Pri |
|---|---|---|
| FR-DEL-01 | Per i titoli in libreria compare l'azione **Elimina**. | M |
| FR-DEL-02 | L'azione apre un dialog di **conferma esplicita** che riassume titolo, percorso, dimensione su disco e numero di file. | M |
| FR-DEL-03 | Il dialog offre due opzioni: **"Elimina anche i file scaricati"** e **"Aggiungi alle esclusioni di importazione"** (impedisce che il titolo sia riaggiunto da liste esterne). Entrambe disattivate di default (D-04). | M |
| FR-DEL-04 | Se si seleziona l'eliminazione dei file, il pulsante di conferma cambia aspetto e il testo avvisa che l'operazione non è reversibile. | M |
| FR-DEL-05 | L'eliminazione rimuove l'elemento solo da Sonarr/Radarr: il titolo resta nel catalogo e nei preferiti di Pickarr. | M |
| FR-DEL-06 | L'eliminazione è consentita anche per elementi non aggiunti da Pickarr (vedi FR-ST-05). | M |
| FR-DEL-07 | Le eliminazioni sono registrate in un log di audit (titolo, istanza, opzioni, data, esito). | S |

### 4.10 Impostazioni (FR-SET)

| ID | Requisito | Pri |
|---|---|---|
| FR-SET-01 | Impostazioni **fonti dati**: API key Watchmode, API key TMDB, API key OMDb (opzionale), piano Watchmode (free/a pagamento). | M |
| FR-SET-02 | Impostazioni **Sonarr** e **Radarr**: URL e API key di una istanza ciascuna, con pulsante **Test connessione** che mostra la versione rilevata. | M |
| FR-SET-03 | Valori di **default** per Sonarr/Radarr: profilo qualità e root folder, scelti tra quelli esposti dall'istanza. | M |
| FR-SET-04 | **Budget crediti**: quota mensile, riserva percentuale, massimo crediti per esecuzione. | M |
| FR-SET-05 | Per sorgente: stato, lingua metadati, cron incrementale, cron completa, numero di pagine dell'incrementale. | M |
| FR-SET-06 | Intervallo della sync Sonarr/Radarr e **configurazione dei webhook** (URL generato e token da copiare nelle impostazioni Connect di Sonarr/Radarr). | M |
| FR-SET-07 | Finestre per le sezioni *Novità*, *In scadenza* e *Rimossi di recente* (giorni). | S |
| FR-SET-08 | Lingua dell'interfaccia. | M |
| FR-SET-09 | Le chiavi e i token non sono mai mostrati in chiaro dopo il salvataggio: la UI indica solo se sono impostati. | M |
| FR-SET-10 | Autenticazione opzionale con password (vedi D-02). | C |
| FR-SET-11 | Le stesse impostazioni (chiavi, URL, quote, default Sonarr/Radarr, finestre, lingua, raccomandazioni) si possono **dichiarare con variabili d'ambiente**. Se la variabile è impostata, quel valore è effettivo e non viene sostituito dal salvataggio in UI. I segreti dichiarati così non sono mostrati e non sono scritti nel database. | M |
| FR-SET-12 | Link *Impostazioni* e route `/settings*` sono visibili/percorribili **solo per il profilo admin**. Un altro profilo non vede il link e una URL `/settings` lo riporta al catalogo. Le API `/api/v1/settings` (e job/sistema) restano chiamabili senza controllo di ruolo. | M |
| FR-SET-13 | I toggle *segnali impliciti* e *escludi in libreria* sono preferenze del **profilo** (`useImplicit`, `excludeInLibrary`), non chiavi globali di calcolo. Modello ONNX, pesi e copertura restano impostazioni globali (solo admin in UI). | M |

### 4.10bis Profili (FR-PRF)

| ID | Requisito | Pri |
|---|---|---|
| FR-PRF-01 | Esiste sempre un profilo admin. Una installazione vuota o già popolata ne ha uno dopo migrazione/seed. Massimo **10** profili. Nome 1–40 caratteri, univoco senza distinzione di maiuscole. Icona = colore di palette fissa + iniziale del nome. | M |
| FR-PRF-02 | Con un solo profilo si entra in catalogo senza selettore. Con due o più, un id ricordato e ancora valido entra diretto; id assente o eliminato apre `/who`. Il controllo in header apre sempre `/who`. | M |
| FR-PRF-03 | L'admin crea ed elimina gli altri profili e modifica tutti. Ogni profilo modifica nome, colore e preferenze reco propri. L'admin non è eliminabile e `IsAdmin` non si cede. | M |
| FR-PRF-04 | I dati personali già presenti alla migrazione (preferiti, voti, storico voti, visti, raccomandazioni, stato gusto, log reco, audit Arr) sono attribuiti all'admin. Catalogo, embedding, settings e righe libreria Arr restano globali. | M |

### 4.11 Amministrazione dei job (FR-ADM)

| ID | Requisito | Pri |
|---|---|---|
| FR-ADM-01 | Una pagina di amministrazione elenca i job (sync catalogo incrementale/completa per sorgente, arricchimento metadati, sync Sonarr/Radarr, sync scadenze) con ultima esecuzione, prossima esecuzione ed esito. | M |
| FR-ADM-02 | L'utente può **avviare manualmente** un job; per la sync catalogo sceglie tra incrementale e completa. Non è possibile avviare due esecuzioni concorrenti dello stesso job. | M |
| FR-ADM-03 | Storico delle esecuzioni con statistiche ed errori. Si conservano le ultime *N* esecuzioni finite per ciascun job (Kind + sorgente); *N* è configurabile (1–100, default 10). Le statistiche mostrate dipendono dal tipo di job (crediti/pagine solo per sync catalogo, conteggi metadata/reco dove applicabile). | M |
| FR-ADM-08 | Nella pagina System: elenco dei file di log sotto il data path, download singolo e azione per svuotare tutti i log Pickarr. | S |
| FR-ADM-04 | Pannello **crediti Watchmode**: usati, quota, disponibili al netto della riserva, proiezione a fine mese. | M |
| FR-ADM-05 | Possibilità di annullare una esecuzione in corso e di riprendere una esecuzione in pausa. | S |
| FR-ADM-06 | Elenco dei titoli non mappati e dei titoli con arricchimento fallito, con possibilità di riprovare. | S |
| FR-ADM-07 | Gestione dei **servizi e delle sorgenti**: aggiungere un servizio scegliendolo dall'elenco della fonte e abilitare/disabilitare i paesi. | S |

### 4.12 Internazionalizzazione (FR-I18N)

| ID | Requisito | Pri |
|---|---|---|
| FR-I18N-01 | Tutti i testi dell'interfaccia sono gestiti tramite chiavi di traduzione. L'**inglese** è la lingua di riferimento. | M |
| FR-I18N-02 | Le chiavi non tradotte in una lingua ricadono sul testo inglese. | M |
| FR-I18N-03 | Date, numeri e durate sono formattati secondo la lingua dell'interfaccia. | M |
| FR-I18N-04 | Aggiungere una lingua richiede solo un nuovo file di traduzione. | M |
| FR-I18N-05 | Traduzione italiana fornita insieme all'MVP. | S |

### 4.13 Multi-servizio e multi-paese (FR-PRV)

| ID | Requisito | Pri |
|---|---|---|
| FR-PRV-01 | Aggiungere un nuovo servizio (es. Amazon Prime, Disney+) e un nuovo paese è un'operazione di **configurazione**: nessuna modifica di schema né di codice se il servizio è coperto da Watchmode. | M |
| FR-PRV-02 | Il selettore di sorgente mostra dinamicamente le sorgenti abilitate. | M |
| FR-PRV-03 | Tutte le funzioni (catalogo, preferiti, aggiunta, stato, eliminazione) funzionano allo stesso modo per ogni sorgente. | M |
| FR-PRV-04 | L'applicazione avvisa se le sorgenti abilitate superano i **3 paesi** del piano free. | S |
| FR-PRV-05 | L'architettura permette di aggiungere in futuro fonti dati diverse da Watchmode, implementando un'astrazione di provider. | M |

### 4.14 Voto e raccomandazioni (FR-REC)

Requisiti dettagliati e priorità in `recommendations.md` (FR-REC-*). Sintesi prodotto:

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-RT | Il profilo attivo può mettere *Love* / *Like* / *Dislike* / *Not interested* (o togliere il voto) da card e scheda; può *Hide* dalla scheda. Voto e hide sono del profilo sul `Title` e indipendenti dalla sorgente. Mutazione ottimistica in UI. I votati e gli hide del profilo non compaiono in *For you*. | M |
| FR-REC-LIST | Pagina *My ratings* (`/ratings`) con export/import; onboarding gusto su `/taste`. | M |
| FR-REC-FY | Consigli per sorgente in riga sul catalogo; pagina *For you* (`/for-you`) sull’unione delle sorgenti abilitate, con motivi, diversità (MMR) ed esplorazione. *My list* è un segnale implicito temporaneo (dettaglio in `recommendations.md`). | M |
| FR-REC-SIM | Griglia *Simili* in fondo alla scheda (`GET /titles/{id}/similar`) sul pool abilitato, con `places`. | M |
| FR-REC-ML | Embedding ONNX locali opzionali (modello scaricato o copiato); senza modello restano prior e livello sparse. | M |
| FR-REC-AD | Impostazioni *Raccomandazioni*: stato, rebuild, embedding, download modello, valutazione offline, reset; eventi locali disattivabili (default off). | M |

Decisioni D-REC-01…D-REC-13: vedi §11.3. Override rispetto a D-REC-12: il livello 3 supervisionato è implementato in R4 ma resta **gated** dalla valutazione offline.

---

## 5. Non-functional requirements

| ID | Area | Requisito |
|---|---|---|
| NFR-01 | Prestazioni | Lista/ricerca titoli: risposta in meno di 300 ms (p95) con un catalogo di 10.000 titoli su SQLite. Scorrimento della griglia fluido. |
| NFR-02 | Affidabilità | I job sono **idempotenti** e riprendibili. Un arresto del container a metà sync non corrompe i dati né produce rimozioni errate. |
| NFR-03 | Resilienza | Ritentativi con backoff per errori temporanei delle API esterne; rispetto di limiti e codici 429; l'indisponibilità di un servizio esterno non rende inutilizzabile l'app. |
| NFR-04 | Sicurezza | Chiavi e token cifrati a riposo e mai restituiti dalle API. Endpoint webhook protetti da token. Vedi §11 per l'autenticazione. |
| NFR-05 | Portabilità | **Singola immagine Docker** (linux/amd64 e linux/arm64), configurazione via variabili d'ambiente e un unico volume `/config`. Avvio senza configurazione preliminare. |
| NFR-06 | Persistenza | Tutti i dati in file SQLite nel volume `/config`: il backup è la copia della cartella. |
| NFR-07 | Osservabilità | Log strutturati, endpoint di health, storico delle esecuzioni dei job. |
| NFR-08 | Manutenibilità | Documentazione OpenAPI generata, test automatici, pipeline CI su GitHub Actions. |
| NFR-09 | Conformità | Rispetto dei termini d'uso di Watchmode (non commerciale, attribuzione, cache 30 giorni) e TMDB (attribuzione). |
| NFR-10 | Compatibilità | Sonarr v4 e Radarr v5 (API v3). Versioni minime da confermare (D-09). |
| NFR-11 | Usabilità | UI desktop ≥ 1280 px, tema scuro, azioni distruttive sempre confermate. |
| NFR-12 | Fuso orario | Orari di schedulazione interpretati nel fuso configurato (`TZ`); date memorizzate in UTC. |

---

## 6. Business rules

| ID | Regola |
|---|---|
| BR-01 | **Identità del titolo.** Un titolo ha una sola riga indipendente dal paese. Può essere disponibile in più sorgenti. L'identità è data dall'id Watchmode, dalla coppia (tipo TMDB, id TMDB) quando nota, e dall'id IMDb quando noto (Watchmode può emettere id distinti per varianti localizzate dello stesso titolo). |
| BR-02 | **Classificazione.** *Serie*: tipo TMDB `tv`. *Film*: tipo TMDB `movie`. *Stand-up*: sottocategoria identificata da genere/keyword (es. keyword TMDB "stand-up comedy"). La regola esatta si definisce dopo lo spike (D-06). La categoria è solo un'etichetta di UI: la destinazione su Sonarr/Radarr dipende dal tipo TMDB. |
| BR-03 | **Disponibilità attiva.** Una disponibilità è attiva se non ha data di rimozione. |
| BR-04 | **Novità.** Titolo con disponibilità attiva, non baseline, aggiunta negli ultimi *N* giorni (default 14). I titoli della baseline non sono mai "novità". |
| BR-05 | **Rimozione.** Un titolo è considerato rimosso solo da una **sync completa**, quando non compare nell'elenco per **2 sync complete consecutive** (parametro configurabile). Questo evita falsi positivi dovuti a elenchi paginati che cambiano durante la lettura. Alla rimozione si imposta la data e si registra un evento. Se il titolo ricompare, la disponibilità torna attiva e si registra un evento "riaggiunto" (e conta come novità). |
| BR-06 | **Rimossi di recente.** Disponibilità con data di rimozione negli ultimi *N* giorni (default 30). I titoli rimossi restano nel DB e nella lista dei preferiti, ma sono esclusi da ricerca e griglia salvo il filtro "includi rimossi". |
| BR-07 | **In scadenza.** Disponibilità attiva con data di scadenza nei prossimi *N* giorni (default 30), se la fonte la fornisce. Se non ci sono dati di scadenza, la sezione è nascosta o indica la limitazione del piano. |
| BR-08 | **Aggiungibilità.** Serie → Sonarr, serve un TVDB id. Film → Radarr, serve un TMDB id. Senza id il titolo è "non aggiungibile". Il TVDB id si ottiene dagli *external ids* di TMDB. |
| BR-09 | **Stato film (Radarr).** *Non in libreria* se assente. *Scaricato* se ha un file. *In download* se è in coda. *Mancante* se in libreria senza file e non in coda. L'ordine di valutazione è: in download, scaricato, mancante. |
| BR-10 | **Stato serie (Sonarr).** *In download* se almeno un episodio è in coda. *Scaricato* se tutti gli episodi attesi (aired e monitorati) hanno un file. *Parziale* se solo una parte ha un file. *Mancante* se nessuno ne ha uno. Lo stato di ogni stagione segue la stessa regola sui propri episodi. Se il numero di episodi attesi è zero (es. serie non ancora iniziata) lo stato è *Mancante*. |
| BR-11 | **Monitorato.** Flag indipendente dallo stato, letto da Sonarr/Radarr. |
| BR-12 | **Dati obsoleti.** Se una sorgente non completa una sync completa da più di 21 giorni, la UI mostra un avviso (il limite del piano free è 30 giorni). |
| BR-13 | **Eliminazione.** Rimuove solo l'elemento da Sonarr/Radarr, con le opzioni scelte. Non altera catalogo, storico o preferiti. |
| BR-14 | **Budget crediti.** Le chiamate a Watchmode non possono superare la quota mensile meno la riserva. Le operazioni manuali possono usare la riserva. Superato il limite per esecuzione, la sync va in pausa e riprende alla prossima occasione. |
| BR-15 | **Lingua metadati.** Si cerca prima nella lingua della sorgente, poi in inglese. Nelle ricerche si considerano titolo localizzato, fallback inglese e titolo originale. |

---

## 7. User stories

Formato: *Come utente, voglio … per …* con criteri di accettazione (CA).

### Catalogo e metadati

**US-01 — Catalogo sempre aggiornato.** Voglio che il catalogo di Netflix IT/US si aggiorni da solo, per non dovermene occupare.
- CA1: la sync incrementale gira secondo il cron configurato e registra l'esito.
- CA2: i nuovi titoli compaiono in griglia dopo l'arricchimento (anche con i soli dati minimi).
- CA3: la sync non supera il budget crediti configurato.

**US-02 — Novità e rimozioni.** Voglio vedere cosa è nuovo e cosa è stato tolto, per sapere cosa guardare o scaricare in fretta.
- CA1: dopo la baseline, i titoli aggiunti compaiono in *Novità* per N giorni.
- CA2: un titolo che sparisce per 2 sync complete consecutive va in *Rimossi di recente* con la data.
- CA3: i titoli della baseline non sono mai "novità".

**US-03 — Descrizioni nella mia lingua.** Voglio trama e titolo in italiano per il catalogo IT, per leggere comodamente.
- CA1: titolo e trama sono in `it-IT` per Netflix IT.
- CA2: se la trama italiana manca, si vede quella inglese.

**US-04 — Controllo dei crediti.** Voglio vedere quanti crediti Watchmode ho usato, per non esaurire la quota.
- CA1: il pannello admin mostra usati/quota/proiezione.
- CA2: raggiunta la soglia, i job si fermano e riprendono nel ciclo successivo.

### Esplorazione

**US-05 — Sfogliare.** Voglio una griglia di poster, per riconoscere subito i titoli.
- CA1: card con poster, titolo, anno, tipo, stato libreria, preferito.
- CA2: scorrimento infinito senza rallentamenti.

**US-06 — Cercare e filtrare.** Voglio cercare per titolo e filtrare per tipo, genere, anno e stato, per trovare ciò che mi interessa.
- CA1: la ricerca ignora maiuscole e accenti.
- CA2: i filtri sono combinabili e riflessi nell'URL.
- CA3: ordinamenti disponibili come da FR-SRC-03.

**US-07 — Dettaglio.** Voglio una scheda con trama, generi, voti, link e disponibilità, per decidere se aggiungerlo.
- CA1: la scheda si apre in modale, con URL condivisibile.
- CA2: mostra link a TMDB e IMDb quando gli id sono noti.

**US-08 — Preferiti.** Voglio segnare titoli come preferiti, per ritrovarli in seguito.
- CA1: toggle da card e scheda.
- CA2: il filtro *My list* mostra anche i preferiti non disponibili nella sorgente corrente, con badge.

### Sonarr/Radarr

**US-09 — Aggiunta guidata.** Voglio aggiungere un titolo a Sonarr/Radarr con un dialog di conferma, per scegliere le opzioni giuste.
- CA1: le opzioni sono quelle di FR-ADD-03/04, con default per profilo e root folder.
- CA2: alla conferma il titolo è aggiunto con monitoraggio e ricerca immediata come scelto.
- CA3: errori di Sonarr/Radarr sono mostrati in modo comprensibile.

**US-10 — Evitare duplicati.** Voglio che l'app mi dica se un titolo è già in libreria, per non aggiungerlo due volte.
- CA1: titoli già presenti mostrano lo stato, non il pulsante Aggiungi.
- CA2: vale anche per titoli aggiunti direttamente su Sonarr/Radarr.

**US-11 — Stato di avanzamento.** Voglio vedere se un titolo è mancante, in download o scaricato, per sapere cosa sta succedendo.
- CA1: badge sulla card; percentuale per i download.
- CA2: per le serie, dettaglio per stagione con episodi scaricati/totali.
- CA3: con i webhook attivi lo stato si aggiorna in pochi secondi, altrimenti alla successiva sync.

**US-12 — Eliminazione sicura.** Voglio eliminare un titolo da Sonarr/Radarr, con o senza file, dopo una conferma esplicita.
- CA1: il dialog mostra percorso e dimensione.
- CA2: le opzioni "elimina file" e "esclusione importazione" sono disattivate di default.
- CA3: il titolo resta nel catalogo e tra i preferiti.

**US-13 — Titolo senza id.** Voglio capire perché alcuni titoli non sono aggiungibili.
- CA1: azione disabilitata con spiegazione ("TVDB id non disponibile").

### Amministrazione

**US-14 — Configurazione.** Voglio impostare chiavi, URL e preferenze dalla UI, senza modificare file.
- CA1: test di connessione per Watchmode, TMDB, Sonarr e Radarr.
- CA2: i segreti non si rileggono in chiaro.

**US-15 — Controllo dei job.** Voglio lanciare a mano una sync e vederne l'esito, per verificare che tutto funzioni.
- CA1: pulsante "Esegui ora" con scelta incrementale/completa.
- CA2: storico con statistiche ed errori.

**US-16 — Nuovo servizio.** Voglio aggiungere Amazon Prime o Disney+ come nuova sorgente, per avere lo stesso catalogo anche per loro.
- CA1: si aggiunge da UI scegliendo servizio e paese, senza deploy.
- CA2: compare nel selettore e funziona come Netflix.

**US-17 — Lingua dell'interfaccia.** Voglio cambiare lingua dell'interfaccia.
- CA1: le chiavi mancanti ricadono sull'inglese.

---

## 8. Use cases

### UC-01 — Sincronizzare il catalogo

- **Attore:** Scheduler (o Utente da UI admin).
- **Precondizioni:** chiave Watchmode valida; sorgente abilitata; budget crediti disponibile.
- **Flusso principale (incrementale):**
  1. Lo scheduler avvia la sync incrementale della sorgente e crea la run.
  2. Il sistema legge le prime *N* pagine dell'elenco titoli per servizio+paese, ordinate per data di uscita decrescente.
  3. Per ogni titolo: se nuovo, lo crea, crea la disponibilità (con data di aggiunta, se la baseline è completata) e registra l'evento *aggiunto*. Se già noto, aggiorna l'ultima conferma.
  4. I titoli nuovi o senza metadati sono messi in coda per l'arricchimento.
  5. La run termina e registra pagine, crediti e conteggi.
- **Flusso alternativo A — sync completa:** al passo 2 legge **tutte** le pagine; al termine, per ogni disponibilità attiva non vista incrementa il contatore di assenze. Raggiunta la soglia (BR-05) e superato il controllo di anomalia (FR-CAT-10) applica la rimozione e registra l'evento. Alla prima sync completa riuscita imposta la **baseline**.
- **Flusso alternativo B — budget esaurito:** raggiunto il limite per run o il limite mensile, la run va in **pausa** salvando il checkpoint. Una run in pausa è ripresa dal job di ripresa quando c'è budget.
- **Flusso alternativo C — errore della fonte:** dopo i ritentativi la run fallisce senza applicare rimozioni; l'esito è visibile in admin.
- **Postcondizioni:** catalogo e storico aggiornati; consumo crediti registrato.

### UC-02 — Sfogliare il catalogo

- **Attore:** Utente.
- **Flusso principale:** l'utente apre la home → il sistema mostra la griglia della sorgente ricordata → l'utente sceglie un filtro rapido (*Novità*, *In scadenza*, ...) → scorre → apre una card (UC-04).
- **Alternativo:** nessun dato (sorgente appena creata, baseline non completata): messaggio "catalogo in preparazione" con link allo stato dei job.

### UC-03 — Cercare e filtrare

- **Attore:** Utente.
- **Flusso principale:** digita nella barra di ricerca → dopo il debounce il sistema esegue la ricerca lato server → applica i filtri scelti → mostra i risultati con conteggio e ordinamento.
- **Alternativo:** nessun risultato → messaggio con suggerimento di allentare i filtri.

### UC-04 — Consultare il dettaglio

- **Attore:** Utente.
- **Flusso principale:** clic sulla card → si apre la modale con metadati, disponibilità, stato libreria e azioni → l'URL riflette il titolo aperto → chiusura con Esc o clic esterno.
- **Alternativo:** metadati non ancora disponibili → si mostrano i dati minimi e l'indicazione "dettagli in arrivo".

### UC-05 — Gestire i preferiti

- **Attore:** Utente.
- **Flusso principale:** clic sul cuore → il sistema salva/rimuove il preferito → l'icona si aggiorna subito (aggiornamento ottimistico, con ripristino in caso di errore).

### UC-06 — Aggiungere un titolo a Sonarr/Radarr

- **Attore:** Utente. **Sistemi:** Backend, Sonarr/Radarr.
- **Precondizioni:** istanza configurata e raggiungibile; titolo non in libreria; id necessario presente.
- **Flusso principale:**
  1. Clic su **Aggiungi** (card o scheda).
2. Il sistema recupera profili qualità, root folder e tag dell'istanza e propone il dialog con i default.
  3. L'utente modifica le opzioni e conferma.
  4. Il backend verifica che il titolo non sia già presente, esegue il lookup sull'istanza e crea l'elemento con monitoraggio e ricerca immediata come scelto.
  5. Il backend aggiorna la cache dello stato e risponde.
  6. La UI mostra il messaggio di esito e il nuovo stato.
- **Alternativi:**
- *Istanza non configurata o irraggiungibile:* messaggio con link alle impostazioni; il dialog non si apre o la conferma è bloccata.
  - *Già presente (race):* si mostra lo stato esistente.
  - *Rifiuto da parte di Sonarr/Radarr (es. root folder non valida):* messaggio con il motivo restituito dall'istanza.
  - *Id mancante:* azione disabilitata (BR-08).
- **Postcondizioni:** titolo in libreria; se richiesto, ricerca avviata.

### UC-07 — Visualizzare lo stato libreria

- **Attore:** Utente. **Sistemi:** Sonarr/Radarr.
- **Flusso principale:**
  1. Lo scheduler sincronizza periodicamente la libreria e la coda di download di ciascuna istanza.
  2. Per ogni elemento calcola lo stato (BR-09/BR-10) e lo associa al titolo tramite id.
  3. La UI mostra badge (solo se diverso da *Non in libreria*), percentuale e dettaglio per stagione.
- **Alternativi:**
- *Webhook ricevuto:* il backend aggiorna subito l'elemento interessato.
  - *Istanza irraggiungibile:* ultimo stato noto con avviso di dato obsoleto.

### UC-08 — Eliminare un titolo da Sonarr/Radarr

- **Attore:** Utente.
- **Flusso principale:** clic su **Elimina** → dialog con riepilogo → l'utente sceglie le opzioni → conferma → il backend invia la cancellazione all'istanza con le opzioni scelte → aggiorna la cache → la UI torna allo stato implicito *Non in libreria* (nessun badge).
- **Alternativi:** *annullamento* (nessun effetto); *errore dell'istanza* (messaggio e nessuna modifica locale).

### UC-09 — Configurare l'applicazione

- **Attore:** Utente.
- **Flusso principale:** apre *Impostazioni* → inserisce chiavi e URL → preme **Test connessione** → il sistema verifica e mostra l'esito (e la versione di Sonarr/Radarr) → salva → il sistema ricalcola le schedulazioni.
- **Alternativo:** test fallito → messaggio specifico (credenziali, URL, timeout); il salvataggio resta possibile con avviso.

### UC-10 — Eseguire manualmente un job

- **Attore:** Utente.
- **Flusso principale:** apre *Admin → Job* → sceglie un job (e la modalità) → **Esegui ora** → il sistema avvia la run e mostra l'avanzamento → a fine run l'esito compare nello storico.
- **Alternativo:** job già in esecuzione → richiesta rifiutata con messaggio.

### UC-11 — Ricevere un webhook da Sonarr/Radarr

- **Attore:** Sonarr/Radarr.
- **Flusso principale:** l'istanza invia l'evento (grab, import, aggiunta, eliminazione, ...) all'URL con token → il backend valida il token → identifica l'elemento → aggiorna lo stato dell'elemento interrogando l'istanza → la UI mostra il nuovo stato.
- **Alternativo:** token errato → richiesta respinta e registrata.

### UC-12 — Aggiungere un servizio o un paese

- **Attore:** Utente.
- **Flusso principale:** *Admin → Servizi* → sceglie un servizio dall'elenco della fonte → sceglie il paese e la lingua dei metadati → abilita la sorgente → il sistema pianifica la baseline (sync completa) → a baseline completata la sorgente compare nel selettore.
- **Alternativo:** superamento dei 3 paesi del piano free → avviso.

---

## 9. Screens

Descrizione testuale (i mockup saranno prodotti in seguito).

**Layout generale.** Intestazione fissa in cima (sticky) con logo, link di navigazione (*Catalogo*, *For you*, *My list*, *Valutazioni*, *Impostazioni* solo per l’admin), controllo profilo (apre `/who`), a destra la versione dell’immagine Docker (link al repo GitHub) e icona Ko-fi. Il selettore sorgente e la ricerca stanno nella pagina catalogo. Piè di pagina con attribuzioni Watchmode/TMDB.

**Chi sei** (`/who`). Griglia dei profili (icona colore + iniziale). Scelta, creazione/eliminazione (admin), modifica nome/colore. Un solo profilo salta questa schermata all’avvio.

**Home / Catalogo** (`/catalog`).
- Chip dei filtri rapidi: Tutti · Novità · In scadenza · Rimossi.
- Barra filtri: tipo, genere, anno, stato libreria, ordinamento, "includi rimossi".
- Griglia di card poster 2:3 con scorrimento infinito.
- Card: poster, tipo, titolo, anno, badge stato libreria (se presente in libreria), cuore, controlli *Like* / *Dislike*.

**For you** (`/for-you`). Griglia raccomandata per la sorgente selezionata (o scope unione), con progresso di apprendimento (nascosto quando `N_eff ≥ N0`), chip dei motivi e filtri tipo/genere.

**My list** (`/list`). Preferiti del profilo attivo; badge rispetto alla sorgente selezionata.

**My ratings** (`/ratings`). Elenco Like/Dislike con export/import.

**Scheda di dettaglio (modale).** Backdrop in testata; colonna sinistra poster; a destra titolo, anno, durata/stagioni, generi, voti, trama, link esterni, disponibilità (dal / scade / rimosso). Sezione *Libreria*: stato, monitorato, percentuale download, tabella stagioni per le serie. Barra azioni: preferito, voto, Aggiungi / Elimina, Cerca ora, Aggiorna stato. In fondo, griglia *Simili* su più righe quando le raccomandazioni sono attive.

**Dialog Aggiungi.** Titolo, destinazione (Sonarr/Radarr), campi opzione, casella "Cerca subito", pulsanti Annulla / Aggiungi.

**Dialog Elimina.** Riepilogo (percorso, dimensione, file), due caselle, pulsante rosso di conferma.

**Impostazioni.** Solo profilo admin. Sezioni: Fonti dati · Sonarr · Radarr · Cataloghi · Raccomandazioni (stato modello/embedding, azioni rebuild/eval/reset) · Job · Sistema · Interfaccia. I toggle di gusto per profilo si gestiscono dal profilo su `/who`, non da queste sezioni globali.

**Job.** Tabella job (anche coda `ml` per embedding/build/eval), pulsante Esegui ora, storico run, grafico/indicatore crediti, elenco titoli non mappati.

---

## 10. Out of scope and future work

**Non previsto in v1**

- Autenticazione (login, password, account protetti). I profili sono una partizione logica senza autenticazione.
- Collaborative filtering o raccomandazioni utente-utente oltre al household prior a freddo.
- Più istanze Sonarr/Radarr (es. HD e 4K) e instradamento automatico.
- Anime e contenuti kids come categorie dedicate.
- App mobile / layout responsive.
- Download/riproduzione di contenuti: l'app orchestra soltanto Sonarr/Radarr.
- Servizi non coperti da Watchmode.
- Notifiche (email, push) per novità o scadenze.
- Ordinamento *For you* dentro la griglia del catalogo (la lista consigliata è solo `/for-you`).
- Chiamate esterne con i dati di gusto (tutto il ML resta locale).

**Evoluzioni possibili:** feed delle modifiche Watchmode (piano a pagamento), SSE per aggiornamenti in tempo reale, aggiunta in blocco dai preferiti, hero banner, più istanze arr, notifiche, autenticazione opzionale, taratura fine dei pesi su corpus di voti reali (SP-R4).

---

## 11. Assumptions, risks, and open decisions

### 11.1 Assunzioni

- A-01: l'uso è personale e non commerciale, quindi il piano free di Watchmode e le licenze personali di TMDB/TVDB sono adeguati.
- A-02: l'accesso all'app avviene in rete locale o dietro reverse proxy; v1 non include un login (D-02).
- A-03: ogni utente di un'immagine Docker usa le **proprie** chiavi API.
- A-04: l'API TVDB diretta non è necessaria in v1, perché il TVDB id si ricava da TMDB.

### 11.2 Rischi

| ID | Rischio | Mitigazione |
|---|---|---|
| R-01 | Costo reale in crediti delle chiamate Watchmode superiore alle stime. | Spike SP-1; budget con riserva; limite per run; sync riprendibile. |
| R-02 | Sul piano free non esistono feed di modifica: le aggiunte di vecchi titoli a Netflix sono rilevate solo dalla sync completa. | Sync completa settimanale; strategia incrementale sostituibile con il feed delle modifiche su piano a pagamento. |
| R-03 | Falsi positivi sulle rimozioni (elenchi paginati che cambiano durante la lettura). | Doppia conferma su 2 sync complete; controllo di anomalia; sort stabile. |
| R-04 | Titoli Netflix senza TVDB id (non aggiungibili a Sonarr). | Segnalazione in UI e in admin; eventuale fallback TVDB diretto in futuro. |
| R-05 | Differenze tra versioni di Sonarr/Radarr. | Lettura della versione al test di connessione; test di contratto; spike SP-3. |
| R-06 | Limiti dei termini d'uso Watchmode (30 giorni di cache, cancellazione dati alla chiusura dell'account). | Sync completa frequente; avviso dati obsoleti; funzione "cancella dati catalogo". |
| R-07 | Concorrenza di scritture su SQLite tra job e API. | WAL, transazioni brevi, DB dei job separato, test di carico. |

### 11.3 Decisioni aperte

Dove una decisione è aperta, il documento adotta una proposta predefinita da validare.

| ID | Tema | Proposta adottata |
|---|---|---|
| D-01 | **Default di profilo qualità e root folder.** Non è verificato che le API di Sonarr/Radarr espongano un "profilo predefinito" esplicito. | L'utente imposta i default nelle impostazioni (scelti dalle liste dell'istanza). Se non impostati, si preseleziona il primo elemento restituito dall'istanza. |
| D-02 | **Autenticazione.** Più profili senza login; l'app contiene chiavi sensibili e può eliminare file. | Nessun login. Profili come partizione logica (header dichiarato). Da usare in LAN o dietro reverse proxy. Password opzionale come evoluzione (FR-SET-10). |
| D-03 | **Voto IMDb.** TMDB non fornisce il voto IMDb. | Integrazione opzionale con OMDb (API key personale). Senza chiave, solo voto TMDB. |
| D-04 | **Default delle opzioni di eliminazione.** | Entrambe disattivate, per sicurezza. L'utente può attivarle a ogni eliminazione. |
| D-05 | **Costo reale per chiamata** Watchmode (e parametri esatti dell'elenco titoli). | Da misurare nello spike SP-1; il design usa un budget parametrico. |
| D-06 | **Classificazione stand-up.** | Preferenza per il genere Watchmode se presente, altrimenti keyword TMDB; verifica nello spike. |
| D-07 | **Sezione "In scadenza" sul piano free** (solo anteprima dei prossimi 10 per paese). | Mostrata quando i dati ci sono, con nota sulla limitazione. Calendario completo con piano a pagamento. |
| D-08 | **Nome del progetto.** | "Pickarr" provvisorio. |
| D-09 | **Versioni minime** di Sonarr (v4) e Radarr (v5). | Da confermare con le istanze in uso. |
| D-10 | **Storage di Hangfire su SQLite** (pacchetto di comunità). | Spike SP-4; in alternativa storage in memoria, dato che le schedulazioni si ricreano dal DB all'avvio. |
| D-REC-01 | Scope predefinito di *For you*. | Sorgente selezionata (`source`). |
| D-REC-02 | Altro scope. | Unione delle sorgenti abilitate; nessuno scope oltre i cataloghi importati. |
| D-REC-03 | Profilo di gusto. | Uno per profilo di casa. Il filtro di catalogo si applica alla lista, non all'apprendimento. Sotto `N0` entra il household prior (Love/Like altrui, tetto 0,5). |
| D-REC-04 | Simili. | Sempre sull'unione delle sorgenti abilitate; mostrano `places`. |
| D-REC-05 | `RemovedAt`. | Non esclude dal pool raccomandazioni. |
| D-REC-06 | Valori di voto. | *Love*, *Like*, *Dislike*, *Not interested*; *Hide* separato. |
| D-REC-07 | Superficie UI. | Pagina dedicata `/for-you` (non ordinamento in griglia catalogo). |
| D-REC-08 | Valutazioni e onboarding. | `/ratings` globale; onboarding `/taste` sul pool abilitato. |
| D-REC-09 | Libreria Arr. | Esclusa di default in *For you*, con interruttore. |
| D-REC-10 | Default feature. | Attiva (`reco.enabled`); modello ONNX su richiesta. |
| D-REC-11 | Modello. | `all-MiniLM-L6-v2` quantizzato (`@q8`), rivisto dopo SP-R2/SP-R4. |
| D-REC-12 | Livello 3. | Originariamente fuori MVP; **override prodotto**: implementato in R4, attivo solo se batte L2 nella valutazione offline. |
| D-REC-13 | Segnali impliciti ed esclusione libreria. | Preferenze del profilo (`useImplicit`, `excludeInLibrary`), attive di default. |

---

## 12. Release phases

| Fase | Contenuto |
|---|---|
| **MVP** | Sync catalogo Netflix IT/US con budget crediti e baseline; arricchimento TMDB; griglia, ricerca e filtri, dettaglio; preferiti (`/list`); impostazioni; aggiunta a Sonarr/Radarr con dialog; stato con sync periodica; eliminazione; admin job; i18n con inglese; immagine Docker; CI. |
| **R1** | Voto (poi per profilo), prior, *For you* L0, API/status, UI card/scheda, `/ratings`. |
| **R2** | Keyword/crediti/en-US, segnali impliciti, sparse L1, simili, reasons. |
| **R3** | Modello ONNX, embedding, kNN L2, MMR/explore, `/taste`, settings/admin reco. |
| **R4** | Valutazione offline, livello 3 gated, registro eventi (default off), taratura SP-R4. |
| **v1.1** | Webhook Sonarr/Radarr, stato per stagione completo, voto IMDb (OMDb), date di scadenza, traduzione italiana, filtri in URL, log di audit. |
| **v1.2** | Aggiornamenti in tempo reale, hero banner, aggiunta in blocco dai preferiti, ricordo delle ultime opzioni. |
| **v2** | Nuovi servizi (Amazon Prime, Disney+), feed delle modifiche per piani a pagamento, autenticazione opzionale. |
