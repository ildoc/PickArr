# PickArr — Documento funzionale

| | |
|---|---|
| **Versione** | 0.2 |
| **Data** | 2026-11-06 |
| **Lingua** | Italiano |
| **Nome progetto** | *PickArr* è un nome provvisorio (vedi D-08) |
| **Documenti collegati** | `specifica-tecnica.md`, `raccomandazioni.md` (feature voto e *Per te*) |

---

## Indice

1. Introduzione
2. Contesto e vincoli delle fonti dati
3. Panoramica funzionale
4. Requisiti funzionali
5. Requisiti non funzionali
6. Regole di business
7. User stories
8. Casi d'uso
9. Schermate
10. Fuori ambito ed evoluzioni
11. Assunzioni, rischi e decisioni aperte
12. Fasi di rilascio

---

## 1. Introduzione

### 1.1 Scopo

PickArr è un'applicazione **self-hosted, single-user**. Mantiene un catalogo aggiornato dei contenuti disponibili su un servizio di streaming (inizialmente **Netflix**) in un dato **paese** e permette di sfogliarlo, cercarlo e salvarlo tra i preferiti. Da qui l'utente può inviare un titolo a **Sonarr** (serie) o **Radarr** (film), vedere lo stato della sua libreria e rimuoverlo.

### 1.2 Obiettivi

- Avere in un'unica interfaccia, simile alla homepage di Netflix, i titoli disponibili in un paese, con descrizione e voti.
- Passare da "titolo che mi interessa" a "titolo monitorato e in ricerca su Sonarr/Radarr" in pochi click.
- Vedere a colpo d'occhio cosa è già in libreria e in che stato, e poterlo eliminare.
- Segnare *Mi piace* / *Non mi piace* sui titoli e ricevere consigli locali (*Per te*) filtrati sui cataloghi importati.
- Funzionare entro i limiti del piano **gratuito** di Watchmode (2.500 crediti/mese, max 3 paesi).
- Poter aggiungere altri servizi (Amazon Prime, Disney+, Hulu, ...) e altri paesi senza modificare l'architettura.

### 1.3 Ambito

**Incluso (v1)**

- Servizio Netflix, paesi Italia (IT) e Stati Uniti (US).
- Tipi di contenuto: film, serie TV, stand-up.
- Sincronizzazione schedulata del catalogo con storico di disponibilità.
- Arricchimento metadati da TMDB (e id incrociati IMDb/TVDB).
- UI web desktop, tema scuro, interfaccia internazionalizzata (inglese di default).
- Ricerca server-side con filtri, preferiti, scheda di dettaglio.
- Integrazione con una istanza Sonarr e una istanza Radarr: aggiunta, stato, eliminazione.
- Amministrazione: impostazioni, controllo dei job, consumo dei crediti Watchmode.
- Raccomandazioni locali (R1–R4): voto sul titolo, pagina *Per te*, simili, embedding ONNX opzionale, valutazione offline, livello supervisionato gated (dettaglio in `raccomandazioni.md`).
- Distribuzione come singola immagine Docker.

**Escluso (v1)**: vedi §10. I consigli personalizzati non sono più fuori ambito: sono specificati in `raccomandazioni.md` e rilasciati dopo l'MVP catalogo (fasi R1–R4 in §12).

### 1.4 Attori

| Attore | Descrizione |
|---|---|
| **Utente** | L'unica persona che usa l'app. Esplora, cerca, gestisce preferiti, aggiunge ed elimina titoli. Ha anche il ruolo di amministratore. |
| **Scheduler** | Componente interno che esegue i job (sync catalogo, arricchimento, sync Sonarr/Radarr). |
| **Watchmode** | Fonte del catalogo e della disponibilità per servizio/paese. |
| **TMDB** | Fonte dei metadati e degli id esterni. |
| **Sonarr / Radarr** | Sistemi esterni di gestione libreria serie / film. |

### 1.5 Glossario

| Termine | Significato |
|---|---|
| **Servizio (Provider)** | Servizio di streaming, es. Netflix. |
| **Sorgente di catalogo** | Coppia *servizio + paese*, es. "Netflix IT". È l'unità su cui si sincronizza e si naviga. |
| **Titolo** | Film, serie o stand-up. Ha un'identità unica indipendente dal paese. |
| **Disponibilità** | Relazione tra un titolo e una sorgente di catalogo, con le sue date (aggiunta, rimozione, scadenza). |
| **Baseline** | Primo import completo di una sorgente. I titoli presenti in quel momento non sono considerati "novità". |
| **Crediti** | Unità di consumo delle API Watchmode (2.500/mese nel piano free). |
| **Stato libreria** | Stato di un titolo in Sonarr/Radarr: non in libreria, mancante, in download, scaricato, parziale. |
| **Voto (rating)** | Preferenza esplicita sul titolo: *Mi piace* o *Non mi piace*. Globale come il preferito (non legato alla sorgente). |
| **Per te** | Lista di raccomandazioni calcolata in locale sul pool dei cataloghi abilitati, filtrabile per sorgente o unione. |
| **Pool** | Unione dei titoli con almeno una disponibilità su sorgenti (servizio+paese) abilitate. `RemovedAt` non esclude. |

---

## 2. Contesto e vincoli delle fonti dati

Le verifiche sono state fatte sulla documentazione pubblica di Watchmode in data 2026-11-03. Questi vincoli hanno impatto diretto sul design.

| Vincolo | Impatto sul prodotto |
|---|---|
| Netflix non offre un'API pubblica per il catalogo. | La fonte è Watchmode. |
| Piano **Developer (free)**: 2.500 crediti/mese condivisi tra tutte le chiamate, fino a 3 paesi. | Serve un budget crediti con sync riprendibile su più giorni (FR-CAT-07). Con IT+US si usano 2 dei 3 paesi disponibili. |
| Uso **non commerciale** con **attribuzione** a Watchmode.com. | Link/credit a Watchmode visibile in UI (FR-MET-07). Adatto al self-hosting personale. |
| Sul piano free i dati in cache vanno **rinfrescati o cancellati entro 30 giorni**. | La sync completa deve girare almeno ogni 30 giorni (default: ogni 7). Se fallisce a lungo, la UI avvisa (BR-12). |
| I **feed delle modifiche** (nuovi titoli, fonti cambiate) e le **date di uscita** sono solo sui piani a pagamento. | Sul piano free la sync "incrementale" si basa su pagine recenti dell'elenco titoli, e la rimozione si scopre confrontando elenchi completi (BR-05). |
| Le **date di scadenza** (titoli in uscita dal servizio) sul piano free sono solo un'anteprima dei prossimi 10 per paese. | La sezione "In scadenza" è limitata sul piano free (D-07). |
| TMDB: API gratuita per uso non commerciale con attribuzione. | Fonte primaria per trama, generi, poster, voti, id IMDb/TVDB. |
| Sonarr usa id **TVDB**, Radarr usa id **TMDB**. | Per le serie serve il TVDB id, ottenuto da TMDB (external ids). Titoli senza id utilizzabile non sono aggiungibili (BR-08). |

> **Nota sui crediti.** Alcune chiamate costano più di un credito. Il costo reale per chiamata va misurato nello spike iniziale (SP-1 nella specifica tecnica), perché da esso dipende il dimensionamento dei job.

---

## 3. Panoramica funzionale

```
Watchmode ──(job schedulato)──► Catalogo locale ◄──(arricchimento)── TMDB
                                       │
                                       ▼
              UI: catalogo, lista, Per te, valutazioni, dettaglio
                                       │  (voto locale + conferma Arr)
                                       ▼
                    Backend ──► Sonarr / Radarr   (+ coda ml ONNX)
                                       ◄── stato (sync periodica + webhook)
```

Macro-funzioni:

1. **Catalogo**: sincronizzazione, storico, budget crediti.
2. **Metadati**: arricchimento da TMDB con lingua configurabile e fallback in inglese.
3. **Esplorazione**: griglia, ricerca, filtri, dettaglio.
4. **Preferiti** (`/list`) e **voti** (*Mi piace* / *Non mi piace*).
5. **Raccomandazioni locali** (*Per te*, simili, embedding opzionale).
6. **Integrazione Sonarr/Radarr**: aggiunta, stato, eliminazione.
7. **Amministrazione**: impostazioni, job, crediti, azioni reco.
8. **Internazionalizzazione** dell'interfaccia (inglese).
9. **Multi-servizio e multi-paese** come requisito di architettura.

---

## 4. Requisiti funzionali

Priorità (MoSCoW): **M** = Must (MVP), **S** = Should, **C** = Could, **W** = Won't (per ora).

### 4.1 Catalogo e sincronizzazione (FR-CAT)

| ID | Requisito | Pri |
|---|---|---|
| FR-CAT-01 | Il sistema mantiene un catalogo per ogni **sorgente di catalogo** (servizio + paese) abilitata. Alla prima installazione sono preconfigurate Netflix IT e Netflix US. | M |
| FR-CAT-02 | L'utente può abilitare/disabilitare una sorgente, scegliere la lingua dei metadati e le frequenze di sincronizzazione. | M |
| FR-CAT-03 | Sono importati i tipi **film**, **serie** e **stand-up**. Gli altri tipi (anime, kids) non sono importati in v1, ma il modello deve poterli accogliere senza migrazioni distruttive. | M |
| FR-CAT-04 | **Sync incrementale** schedulata (cron configurabile per sorgente): intercetta i titoli aggiunti di recente. È idempotente e può essere rieseguita in qualsiasi momento. | M |
| FR-CAT-05 | **Sync completa** periodica (default settimanale, configurabile): riallinea l'intero catalogo e rileva le **rimozioni** (BR-05). | M |
| FR-CAT-06 | Il sistema conserva lo **storico di disponibilità**: primo avvistamento, ultima conferma, data di aggiunta, data di rimozione, eventuale data di scadenza, più un registro eventi (aggiunto / rimosso / riaggiunto / scadenza annunciata). | M |
| FR-CAT-07 | **Budget crediti Watchmode**: il sistema non supera mai il budget mensile configurato (default: quota del piano meno una riserva), limita i crediti per singola esecuzione e **riprende** le esecuzioni interrotte nei giorni successivi. Il consumo è sempre visibile. | M |
| FR-CAT-08 | **Baseline**: al completamento della prima sync completa di una sorgente, tutti i titoli presenti sono marcati come baseline e non appaiono come "novità". | M |
| FR-CAT-09 | Ogni esecuzione di sync è tracciata (tipo, avvio, fine, esito, pagine lette, crediti usati, titoli aggiunti/aggiornati/rimossi, errori) e consultabile dall'amministrazione. | M |
| FR-CAT-10 | **Protezione dalle rimozioni anomale**: se una sync completa indicherebbe la rimozione di una percentuale eccessiva del catalogo, le rimozioni non vengono applicate e la run è segnata "da verificare". | M |
| FR-CAT-11 | Sync delle **date di scadenza** quando la fonte le rende disponibili (preview sul piano free, calendario completo sui piani a pagamento). | S |
| FR-CAT-12 | I titoli di cui non si riesce a ricavare un id TMDB sono registrati come **non mappati** e consultabili dall'amministrazione. | S |
| FR-CAT-13 | Sync incrementale via **feed delle modifiche** di Watchmode se l'utente dichiara di avere un piano a pagamento (impostazione "piano Watchmode"). | C |

### 4.2 Metadati (FR-MET)

| ID | Requisito | Pri |
|---|---|---|
| FR-MET-01 | Per ogni titolo il sistema ottiene da TMDB: titolo localizzato e originale, trama, anno, durata (film) o numero di stagioni/episodi (serie), generi, poster, backdrop, voto TMDB, id IMDb e id TVDB (serie). | M |
| FR-MET-02 | La lingua dei metadati è configurabile per sorgente (es. `it-IT` per Netflix IT, `en-US` per Netflix US). Se titolo o trama mancano nella lingua scelta, si usa l'**inglese** come fallback. | M |
| FR-MET-03 | La scheda mostra link esterni a **TMDB** e **IMDb** (se l'id è noto). Il link a TVDB per le serie è facoltativo. | M |
| FR-MET-04 | Voto IMDb mostrato accanto al voto TMDB. Richiede un'integrazione aggiuntiva opzionale (OMDb, con API key propria, vedi D-03). Senza di essa si mostra solo il voto TMDB. | S |
| FR-MET-05 | I metadati sono aggiornati periodicamente (default: ogni 30 giorni per i titoli attivi) e su richiesta. | M |
| FR-MET-06 | Le immagini sono servite direttamente dagli URL di TMDB (hotlink). Nessuna copia locale. | M |
| FR-MET-07 | L'interfaccia riporta le **attribuzioni** richieste: Watchmode.com (con link) e TMDB (logo e dicitura "This product uses the TMDB API but is not endorsed or certified by TMDB"). | M |
| FR-MET-08 | Un titolo il cui arricchimento fallisce è comunque mostrato con i dati minimi (titolo e anno da Watchmode) e riprovato in seguito. | S |

### 4.3 Home e navigazione (FR-UI)

| ID | Requisito | Pri |
|---|---|---|
| FR-UI-01 | Interfaccia **solo desktop** (larghezza minima supportata 1280 px) con **tema scuro**. | M |
| FR-UI-02 | Un selettore nell'intestazione permette di scegliere la sorgente di catalogo (servizio + paese). La scelta è ricordata tra una sessione e l'altra. | M |
| FR-UI-03 | La home è una **vista a griglia** del catalogo della sorgente selezionata, con barra di ricerca e filtri rapidi: *Tutti*, *Novità*, *In scadenza*, *Rimossi di recente*. *La mia lista* e *Per te* sono route dedicate (`/list`, `/for-you`), non viste del catalogo. | M |
| FR-UI-04 | Ogni card mostra poster, titolo, anno, badge del tipo (film/serie/stand-up), badge dello **stato libreria**, icona preferito e controlli di **voto** (*Mi piace* / *Non mi piace*) quando le raccomandazioni sono abilitate. | M |
| FR-UI-05 | La griglia usa scorrimento infinito con caricamento a pagine. Deve restare fluida con cataloghi di alcune migliaia di titoli. | M |
| FR-UI-06 | Il clic su una card apre la **scheda di dettaglio** in una finestra modale, raggiungibile anche tramite URL diretto. | M |
| FR-UI-07 | Un **hero banner** in cima alla home con un titolo in evidenza (es. ultima novità con backdrop). | C |
| FR-UI-08 | Ordinamento selezionabile (vedi FR-SRC-03). | M |

### 4.4 Ricerca e filtri (FR-SRC)

| ID | Requisito | Pri |
|---|---|---|
| FR-SRC-01 | Ricerca testuale **server-side** sul titolo localizzato e su quello originale, insensibile a maiuscole e accenti, con debounce lato UI. | M |
| FR-SRC-02 | Filtri combinabili: **tipo** (film/serie/stand-up), **genere** (multi-selezione), **anno** (intervallo), **stato libreria** (non in libreria, mancante, in download, scaricato, parziale), **solo preferiti**, **includi rimossi**. | M |
| FR-SRC-03 | Ordinamenti: rilevanza (con ricerca), data di aggiunta al servizio, anno, voto, titolo A→Z. | M |
| FR-SRC-04 | Filtro per voto minimo. | C |
| FR-SRC-05 | I filtri sono riflessi nell'URL (condivisibili, compatibili col pulsante Indietro). | S |
| FR-SRC-06 | Il conteggio dei risultati è mostrato in UI. | S |

### 4.5 Scheda di dettaglio (FR-DET)

| ID | Requisito | Pri |
|---|---|---|
| FR-DET-01 | La scheda mostra: backdrop/poster, titolo, titolo originale, anno, durata (film) o numero di stagioni (serie), generi, trama, voto TMDB (e IMDb se disponibile), link esterni. | M |
| FR-DET-02 | Mostra le informazioni di **disponibilità** nella sorgente selezionata: disponibile dal, in scadenza il, rimosso il. | M |
| FR-DET-03 | Mostra lo **stato libreria**; per le serie il dettaglio **stagione per stagione**. | M |
| FR-DET-04 | Azioni: preferito, **voto** (*Mi piace* / *Non mi piace*), aggiungi a Sonarr/Radarr, elimina da Sonarr/Radarr, aggiorna stato. | M |
| FR-DET-05 | Azione "Cerca ora" su un titolo già in libreria (lancia la ricerca in Sonarr/Radarr). | S |
| FR-DET-06 | Link per aprire il titolo nell'interfaccia web di Sonarr/Radarr. | C |
| FR-DET-07 | Se il titolo è disponibile in altre sorgenti configurate (es. anche su Netflix US), la scheda lo indica. | C |

### 4.6 Preferiti (FR-FAV)

| ID | Requisito | Pri |
|---|---|---|
| FR-FAV-01 | L'utente può aggiungere/rimuovere un titolo dai preferiti da card e da scheda. | M |
| FR-FAV-02 | I preferiti sono **globali** (legati al titolo, non alla sorgente) e persistono sul backend. Il **voto** segue lo stesso modello di dato globale, con valori *Mi piace* / *Non mi piace* al posto del booleano. | M |
| FR-FAV-03 | La pagina *La mia lista* (`/list`) mostra tutti i preferiti cross-provider e cross-country, ciascuno con un badge rispetto alla sorgente selezionata (presente / rimosso / assente). Non è una vista `view=favorites` del catalogo. | M |
| FR-FAV-04 | Aggiunta in blocco dei preferiti a Sonarr/Radarr. | C |

### 4.7 Aggiunta a Sonarr/Radarr (FR-ADD)

| ID | Requisito | Pri |
|---|---|---|
| FR-ADD-01 | Per ogni titolo non in libreria compare l'azione **Aggiungi**. La destinazione è determinata dal tipo: serie → Sonarr, film → Radarr. Gli stand-up seguono il tipo TMDB del titolo (film → Radarr, serie → Sonarr). | M |
| FR-ADD-02 | L'azione apre un **dialog di conferma con le opzioni**, preimpostate con i valori di default. Nulla viene inviato finché l'utente non conferma. | M |
| FR-ADD-03 | Opzioni **Sonarr**: profilo qualità, root folder, modalità di monitoraggio (tutti gli episodi, solo futuri, mancanti, esistenti, prima stagione, ultima stagione, stagione più recente, solo pilot, recenti, nessuno), monitoraggio delle nuove stagioni, tipo di serie (standard/daily/anime), cartella per stagione, tag, **cerca subito gli episodi mancanti**. | M |
| FR-ADD-04 | Opzioni **Radarr**: profilo qualità, root folder, monitorato sì/no, disponibilità minima (annunciato / al cinema / uscito), tag, **cerca subito il film**. | M |
| FR-ADD-05 | Profilo qualità e root folder sono preselezionati con i valori di default (vedi D-01). Tutte le altre opzioni sono scelte dall'utente. | M |
| FR-ADD-06 | Se l'istanza non è configurata, non è raggiungibile o rifiuta le credenziali, l'azione è bloccata con un messaggio chiaro e un link alle impostazioni. | M |
| FR-ADD-07 | Se il titolo è già presente nell'istanza, l'azione Aggiungi non è offerta: si mostra lo stato. | M |
| FR-ADD-08 | Se per il titolo manca l'id necessario (TVDB per le serie, TMDB per i film), l'azione è disabilitata con una spiegazione. | M |
| FR-ADD-09 | Dopo l'aggiunta l'interfaccia mostra subito il nuovo stato e un messaggio di esito. Eventuali errori restituiti da Sonarr/Radarr sono mostrati in forma leggibile. | M |
| FR-ADD-10 | Il dialog ricorda le ultime opzioni scelte (tranne il profilo/root di default). | C |

### 4.8 Stato libreria (FR-ST)

| ID | Requisito | Pri |
|---|---|---|
| FR-ST-01 | Ogni titolo ha uno stato libreria: **Non in libreria**, **Mancante**, **In download** (con percentuale), **Scaricato**, **Parziale** (solo serie). Indicazione separata di **monitorato / non monitorato**. | M |
| FR-ST-02 | Lo stato è visibile su card e scheda ed è usabile come filtro. | M |
| FR-ST-03 | Per le serie, la scheda mostra per ogni stagione: monitorata sì/no, episodi scaricati su totali e stato. | M |
| FR-ST-04 | Lo stato proviene da una **cache** aggiornata da una sync periodica (default: ogni 15 minuti) e, se configurati, dai **webhook** di Sonarr/Radarr per aggiornamenti quasi in tempo reale. | M |
| FR-ST-05 | Il match tra titolo del catalogo e libreria avviene per **id TMDB / TVDB**, quindi vale anche per elementi presenti in Sonarr/Radarr ma non aggiunti da PickArr. | M |
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
| FR-DEL-05 | L'eliminazione rimuove l'elemento solo da Sonarr/Radarr: il titolo resta nel catalogo e nei preferiti di PickArr. | M |
| FR-DEL-06 | L'eliminazione è consentita anche per elementi non aggiunti da PickArr (vedi FR-ST-05). | M |
| FR-DEL-07 | Le eliminazioni sono registrate in un log di audit (titolo, istanza, opzioni, data, esito). | S |

### 4.10 Impostazioni (FR-SET)

| ID | Requisito | Pri |
|---|---|---|
| FR-SET-01 | Impostazioni **fonti dati**: API key Watchmode, token TMDB, API key OMDb (opzionale), piano Watchmode (free/a pagamento). | M |
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

### 4.11 Amministrazione dei job (FR-ADM)

| ID | Requisito | Pri |
|---|---|---|
| FR-ADM-01 | Una pagina di amministrazione elenca i job (sync catalogo incrementale/completa per sorgente, arricchimento metadati, sync Sonarr/Radarr, sync scadenze) con ultima esecuzione, prossima esecuzione ed esito. | M |
| FR-ADM-02 | L'utente può **avviare manualmente** un job; per la sync catalogo sceglie tra incrementale e completa. Non è possibile avviare due esecuzioni concorrenti dello stesso job. | M |
| FR-ADM-03 | Storico delle esecuzioni con statistiche ed errori. | M |
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

Requisiti dettagliati e priorità in `raccomandazioni.md` (FR-REC-*). Sintesi prodotto:

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-RT | L'utente può mettere *Mi piace* / *Non mi piace* (o togliere il voto) da card e scheda; il voto è globale sul `Title`. Mutazione ottimistica in UI. | M |
| FR-REC-LIST | Pagina *Le mie valutazioni* (`/ratings`) con export/import; onboarding gusto su `/taste`. | M |
| FR-REC-FY | Pagina *Per te* (`/for-you`) con scope sorgente o unione abilitata, motivi, diversità (MMR) ed esplorazione. | M |
| FR-REC-SIM | Riga *Simili* in scheda (`GET /titles/{id}/similar`) sul pool abilitato, con `places`. | M |
| FR-REC-ML | Embedding ONNX locali opzionali (modello scaricato o copiato); senza modello restano prior e livello sparse. | M |
| FR-REC-AD | Impostazioni *Raccomandazioni*: stato, rebuild, embedding, download modello, valutazione offline, reset; eventi locali disattivabili (default off). | M |

Decisioni D-REC-01…D-REC-13: vedi §11.3. Override rispetto a D-REC-12: il livello 3 supervisionato è implementato in R4 ma resta **gated** dalla valutazione offline.

---

## 5. Requisiti non funzionali

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

## 6. Regole di business

| ID | Regola |
|---|---|
| BR-01 | **Identità del titolo.** Un titolo ha una sola riga indipendente dal paese. Può essere disponibile in più sorgenti. L'identità è data dall'id Watchmode e, quando noto, dalla coppia (tipo TMDB, id TMDB). |
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
- CA2: il filtro *La mia lista* mostra anche i preferiti non disponibili nella sorgente corrente, con badge.

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

## 8. Casi d'uso

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
  3. La UI mostra badge, percentuale e dettaglio per stagione.
- **Alternativi:**
  - *Webhook ricevuto:* il backend aggiorna subito l'elemento interessato.
  - *Istanza irraggiungibile:* ultimo stato noto con avviso di dato obsoleto.

### UC-08 — Eliminare un titolo da Sonarr/Radarr

- **Attore:** Utente.
- **Flusso principale:** clic su **Elimina** → dialog con riepilogo → l'utente sceglie le opzioni → conferma → il backend invia la cancellazione all'istanza con le opzioni scelte → aggiorna la cache → la UI mostra lo stato "Non in libreria".
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

## 9. Schermate

Descrizione testuale (i mockup saranno prodotti in seguito).

**Layout generale.** Intestazione con logo, selettore sorgente (es. "Netflix · Italia"), barra di ricerca, link a *Per te*, *La mia lista*, *Valutazioni*, *Impostazioni*. Piè di pagina con attribuzioni Watchmode/TMDB e versione.

**Home / Catalogo** (`/catalog`).
- Chip dei filtri rapidi: Tutti · Novità · In scadenza · Rimossi.
- Barra filtri: tipo, genere, anno, stato libreria, ordinamento, "includi rimossi".
- Griglia di card poster 2:3 con scorrimento infinito.
- Card: poster, tipo, titolo, anno, badge stato libreria, cuore, controlli *Mi piace* / *Non mi piace*.

**Per te** (`/for-you`). Griglia raccomandata per la sorgente selezionata (o scope unione), con progresso di apprendimento, chip dei motivi e filtri tipo/genere. Onboarding su `/taste` se i voti sono pochi.

**La mia lista** (`/list`). Preferiti globali; badge rispetto alla sorgente selezionata.

**Le mie valutazioni** (`/ratings`). Elenco Like/Dislike con export/import.

**Scheda di dettaglio (modale).** Backdrop in testata; colonna sinistra poster; a destra titolo, anno, durata/stagioni, generi, voti, trama, link esterni, disponibilità (dal / scade / rimosso). Sezione *Libreria*: stato, monitorato, percentuale download, tabella stagioni per le serie. Barra azioni: preferito, voto, Aggiungi / Elimina, Cerca ora, Aggiorna stato. Riga *Simili* quando le raccomandazioni sono attive.

**Dialog Aggiungi.** Titolo, destinazione (Sonarr/Radarr), campi opzione, casella "Cerca subito", pulsanti Annulla / Aggiungi.

**Dialog Elimina.** Riepilogo (percorso, dimensione, file), due caselle, pulsante rosso di conferma.

**Impostazioni.** Sezioni: Fonti dati · Sonarr · Radarr · Cataloghi · Raccomandazioni (stato modello/embedding, azioni rebuild/eval/reset) · Job · Interfaccia.

**Job.** Tabella job (anche coda `ml` per embedding/build/eval), pulsante Esegui ora, storico run, grafico/indicatore crediti, elenco titoli non mappati.

---

## 10. Fuori ambito ed evoluzioni

**Non previsto in v1**

- Autenticazione multi-utente e profili con preferiti separati.
- Più istanze Sonarr/Radarr (es. HD e 4K) e instradamento automatico.
- Anime e contenuti kids come categorie dedicate.
- App mobile / layout responsive.
- Download/riproduzione di contenuti: l'app orchestra soltanto Sonarr/Radarr.
- Servizi non coperti da Watchmode.
- Notifiche (email, push) per novità o scadenze.
- Ordinamento *Per te* dentro la griglia del catalogo (la lista consigliata è solo `/for-you`).
- Chiamate esterne con i dati di gusto (tutto il ML resta locale).

**Evoluzioni possibili:** feed delle modifiche Watchmode (piano a pagamento), SSE per aggiornamenti in tempo reale, aggiunta in blocco dai preferiti, hero banner, più istanze arr, notifiche, autenticazione opzionale, taratura fine dei pesi su corpus di voti reali (SP-R4).

---

## 11. Assunzioni, rischi e decisioni aperte

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
| D-02 | **Autenticazione.** Single-user, ma l'app contiene chiavi sensibili e può eliminare file. | v1 senza login, da usare in LAN o dietro reverse proxy con autenticazione. Password opzionale come evoluzione (FR-SET-10). |
| D-03 | **Voto IMDb.** TMDB non fornisce il voto IMDb. | Integrazione opzionale con OMDb (API key personale). Senza chiave, solo voto TMDB. |
| D-04 | **Default delle opzioni di eliminazione.** | Entrambe disattivate, per sicurezza. L'utente può attivarle a ogni eliminazione. |
| D-05 | **Costo reale per chiamata** Watchmode (e parametri esatti dell'elenco titoli). | Da misurare nello spike SP-1; il design usa un budget parametrico. |
| D-06 | **Classificazione stand-up.** | Preferenza per il genere Watchmode se presente, altrimenti keyword TMDB; verifica nello spike. |
| D-07 | **Sezione "In scadenza" sul piano free** (solo anteprima dei prossimi 10 per paese). | Mostrata quando i dati ci sono, con nota sulla limitazione. Calendario completo con piano a pagamento. |
| D-08 | **Nome del progetto.** | "PickArr" provvisorio. |
| D-09 | **Versioni minime** di Sonarr (v4) e Radarr (v5). | Da confermare con le istanze in uso. |
| D-10 | **Storage di Hangfire su SQLite** (pacchetto di comunità). | Spike SP-4; in alternativa storage in memoria, dato che le schedulazioni si ricreano dal DB all'avvio. |
| D-REC-01 | Scope predefinito di *Per te*. | Sorgente selezionata (`source`). |
| D-REC-02 | Altro scope. | Unione delle sorgenti abilitate; nessuno scope oltre i cataloghi importati. |
| D-REC-03 | Profilo. | Unico; il filtro di catalogo si applica alla lista, non all'apprendimento. |
| D-REC-04 | Simili. | Sempre sull'unione delle sorgenti abilitate; mostrano `places`. |
| D-REC-05 | `RemovedAt`. | Non esclude dal pool raccomandazioni. |
| D-REC-06 | Valori di voto. | Solo *Mi piace* e *Non mi piace*. |
| D-REC-07 | Superficie UI. | Pagina dedicata `/for-you` (non ordinamento in griglia catalogo). |
| D-REC-08 | Valutazioni e onboarding. | `/ratings` globale; onboarding `/taste` sul pool abilitato. |
| D-REC-09 | Libreria Arr. | Esclusa di default in *Per te*, con interruttore. |
| D-REC-10 | Default feature. | Attiva (`reco.enabled`); modello ONNX su richiesta. |
| D-REC-11 | Modello. | `all-MiniLM-L6-v2` quantizzato (`@q8`), rivisto dopo SP-R2/SP-R4. |
| D-REC-12 | Livello 3. | Originariamente fuori MVP; **override prodotto**: implementato in R4, attivo solo se batte L2 nella valutazione offline. |
| D-REC-13 | Segnali impliciti. | Attivi di default, disattivabili. |

---

## 12. Fasi di rilascio

| Fase | Contenuto |
|---|---|
| **MVP** | Sync catalogo Netflix IT/US con budget crediti e baseline; arricchimento TMDB; griglia, ricerca e filtri, dettaglio; preferiti (`/list`); impostazioni; aggiunta a Sonarr/Radarr con dialog; stato con sync periodica; eliminazione; admin job; i18n con inglese; immagine Docker; CI. |
| **R1** | Voto globale, prior, *Per te* L0, API/status, UI card/scheda, `/ratings`. |
| **R2** | Keyword/crediti/en-US, segnali impliciti, sparse L1, simili, reasons. |
| **R3** | Modello ONNX, embedding, kNN L2, MMR/explore, `/taste`, settings/admin reco. |
| **R4** | Valutazione offline, livello 3 gated, registro eventi (default off), taratura SP-R4. |
| **v1.1** | Webhook Sonarr/Radarr, stato per stagione completo, voto IMDb (OMDb), date di scadenza, traduzione italiana, filtri in URL, log di audit. |
| **v1.2** | Aggiornamenti in tempo reale, hero banner, aggiunta in blocco dai preferiti, ricordo delle ultime opzioni. |
| **v2** | Nuovi servizi (Amazon Prime, Disney+), feed delle modifiche per piani a pagamento, autenticazione opzionale. |
