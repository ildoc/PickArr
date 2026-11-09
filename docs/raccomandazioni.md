# PickArr — Rating e raccomandazioni

| | |
|---|---|
| **Versione** | 0.2 |
| **Data** | 2026-11-05 |
| **Lingua** | Italiano |
| **Documenti collegati** | `documento-funzionale.md`, `specifica-tecnica.md` |
| **Convenzioni** | Gli ID di questo documento usano il suffisso `REC` (FR-REC, BR-REC, NFR-REC, US-REC, UC-REC) o `SP-R` per gli spike, per non collidere con quelli dei documenti esistenti. I riferimenti a FR-, BR-, D-, SP- senza `REC` rimandano ai documenti collegati. |

### Revisioni

| Versione | Scelta |
|---|---|
| 0.2 | Il voto è uno solo per `Title`, condiviso tra i provider. I consigli mostrati appartengono ai cataloghi già importati: per default la sorgente selezionata (servizio + paese), in più l’unione delle sorgenti abilitate. Un mi piace dato su un altro servizio entra nel profilo usato anche per la sorgente corrente. I titoli simili elencano i titoli presenti in almeno una sorgente abilitata, con i servizi. La pagina è `/for-you` e riusa la sorgente del catalogo. La rimozione dal servizio non esclude un titolo. Niente stato *Nascondi*. Niente raccolta di titoli assenti dai cataloghi. |
| 0.1 | Bozza con pool TMDB, scope «tutto TMDB» e stato *Nascondi*. Sostituita da 0.2. |

---

## Indice

**Parte A — Funzionale**
1. Introduzione e obiettivi
2. Principi di progetto
3. Requisiti funzionali
4. Requisiti non funzionali
5. Regole di business
6. User stories
7. Casi d'uso
8. Schermate

**Parte B — Tecnica**
9. Visione d'insieme e ancoraggio al codice
10. Modello dati
11. Pipeline di dati e job
12. Algoritmo di raccomandazione a livelli
13. Modello locale (embedding)
14. Contratti API
15. Frontend
16. Configurazione
17. Test e valutazione
18. Build, container e CI
19. Rischi, spike e decisioni aperte
20. Fasi di rilascio
21. Impatto sui documenti esistenti

---

# Parte A — Funzionale

## 1. Introduzione e obiettivi

### 1.1 Scopo

Aggiungere a PickArr un **voto** (mi piace / non mi piace) sul titolo e dei **consigli** che usano quel voto. Il voto è unico anche se lo stesso titolo è su più servizi. I consigli che l’utente vede sono titoli già presenti nei cataloghi importati.

### 1.2 Obiettivi

- Permettere di votare un titolo con un gesto (un click), dalla card e dalla scheda.
- Tenere **un solo voto per titolo**. Netflix e Prime Video che hanno lo stesso titolo condividono quel voto. Lo stesso vale tra paesi (Netflix IT e Netflix US).
- Mostrare in **Per te**, per default, solo i titoli della **sorgente di catalogo selezionata** (servizio + paese: ad esempio Netflix Italia).
- Offrire, sulla stessa pagina, la vista **Tutte le sorgenti abilitate**: ogni titolo una volta, se compare in almeno un catalogo acceso, con l’elenco dei servizi.
- Far entrare nel profilo anche i voti dati su titoli di un altro servizio. Cambia il filtro di ciò che viene mostrato, non il profilo.
- Funzionare dal primo giorno, senza alcun voto (qualità e popolarità dei titoli già in catalogo, più i «titoli simili»), e migliorare con l’uso.
- Eseguire il calcolo **in locale**, nel container. Il modello di embedding, se installato, è locale.
- Spiegare ogni consiglio.
- Restare un’app a singola immagine Docker, single-user, self-hosted.

### 1.3 Fuori ambito

- Consigli di titoli che non hanno nessuna riga `Availability` su una sorgente abilitata. Non si importano candidati da TMDB `recommendations`, `similar` o `discover` solo per consigliarli.
- Scope «tutto TMDB», etichetta *Fuori catalogo*, watch providers TMDB/JustWatch.
- Stato *Nascondi* generico. I voti restano *Mi piace* / *Non mi piace*. Esiste invece il flag separato **Visto** (già guardato): esclusione da *Per te*/simili e segnale implicito, non un hide senza feedback.
- Ordinamento *Per te* mescolato nella griglia del catalogo. I consigli per **una** sorgente stanno in una **riga dedicata** su `/catalog` (al più 25 titoli). La pagina `/for-you` copre l’unione delle sorgenti abilitate.
- Raccomandazioni collaborative, profili multipli, sync di cronologia esterna (Trakt, Plex, Jellyfin).
- Fine-tuning di modelli di linguaggio. Il modello di embedding, se usato, è in sola inferenza.
- Notifiche push.

### 1.4 Glossario

| Termine | Significato |
|---|---|
| **Titolo** | Riga `Titles`. Una sola per identità (BR-01): id Watchmode e, quando nota, coppia (tipo TMDB, id TMDB). I provider non duplicano il titolo: aggiungono una `Availability`. |
| **Sorgente** | `CatalogSource`: un provider e un paese (Netflix IT, Netflix US, Prime Video IT sono sorgenti diverse). |
| **Voto** | *Mi piace molto* (*Love*), *Mi piace* o *Non mi piace* sul `Title.Id`. Al più uno. Vale per ogni sorgente in cui il titolo compare. |
| **Visto** | Flag separato sul `Title.Id` (*già guardato*). Coesiste col voto. Esclude da *Per te* e simili; senza voto esplicito contribuisce al profilo come segnale implicito. |
| **Segnale implicito** | Interesse senza voto: *La mia lista* (interesse temporaneo / da guardare), *Visto*, aggiunta a Sonarr/Radarr, download completato. Non sostituisce un voto esplicito. |
| **Profilo** | Gusti dell’utente, costruiti da voti e segnali di qualunque sorgente. |
| **Pool** | Titoli tra cui si calcola il punteggio: i `Title` che hanno almeno una `Availability` su una sorgente abilitata. |
| **Scope** | Filtro applicato ai punteggi già calcolati. `source`: la sorgente selezionata. `enabled`: tutte le sorgenti abilitate. |
| **Prior** | Punteggio non personale da voto medio e popolarità TMDB già salvati sul titolo. |
| **Embedding** | Vettore di 384 numeri calcolato in locale dal testo del titolo. |
| **Cold start** | Fase con pochi segnali, in cui il prior pesa ancora molto. |

---

## 2. Principi di progetto

1. **Il voto sta sul titolo.** `Ratings.TitleId` è la chiave primaria di `Titles`. Non esiste un voto per coppia titolo+sorgente. Un mi piace dato mentre si guarda Prime Video è lo stesso record letto quando si guarda Netflix Italia.
2. **Il profilo è unico, il filtro è di catalogo.** I punteggi si calcolano sul pool. *Per te* con scope `source` restituisce i titoli che hanno una `Availability` sulla sorgente selezionata. Lo scope `enabled` restituisce i titoli che ne hanno una su almeno una sorgente abilitata.
3. **La rimozione dal servizio non toglie il titolo dal pool.** `Availability.RemovedAt` non è un filtro delle raccomandazioni. Il titolo resta consigliabile. La card può mostrare la data di rimozione già presente nel modello.
4. **Prima semplice, poi personale.** Ogni livello dell’algoritmo funziona da solo. Se il modello locale manca, si usano i livelli inferiori.
5. **Spiegabile.** Ogni elemento consigliato riporta i motivi.
6. **Locale.** Nessun dato di gusto esce dall’installazione. L’utente può esportare, correggere o azzerare voti e dati derivati.
7. **Niente crediti Watchmode in più.** I consigli leggono `Titles`, `Availabilities` e i metadati già arricchiti. Non chiamano Watchmode.
8. **Stessi confini del catalogo.** Si riusano sorgente selezionata, DTO delle card, preferiti, stati Sonarr/Radarr e job Hangfire. Le parti nuove si agganciano lì.

---

## 3. Requisiti funzionali

Priorità MoSCoW: **M** per l’MVP della feature, **S** should, **C** could.

### 3.1 Voto (FR-REC-RT)

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-RT-01 | L’utente assegna a un titolo **Mi piace molto**, **Mi piace** o **Non mi piace**. Può sostituirlo o rimuoverlo (nessun voto). | M |
| FR-REC-RT-02 | Il voto è disponibile su card (catalogo, Per te, Le mie valutazioni) e sulla scheda di dettaglio. | M |
| FR-REC-RT-03 | Il voto è **globale sul titolo** e persiste sul backend. È indipendente dal provider e dal paese. | M |
| FR-REC-RT-04 | Un titolo già votato non compare in *Per te* né nei simili. Resta in *Le mie valutazioni*, nel catalogo e nei preferiti. Il *Non mi piace* resta un segnale negativo per il profilo. | M |
| FR-REC-RT-05 | Pagina **Le mie valutazioni**: elenco globale dei titoli votati, tab *Mi piace* / *Non mi piace*, ricerca, modifica e rimozione del voto. Stessa presentazione trasversale di *La mia lista* (`places`). In più, lo stato rispetto alla sorgente selezionata: presente in quella sorgente (con `removedAt` se c’è) oppure assente da quella sorgente. | S |
| FR-REC-RT-06 | Esportazione e importazione JSON. L’importazione aggiorna solo titoli **già nel database**, riconosciuti per (`tmdbType`, `tmdbId`). Non crea titoli. | S |
| FR-REC-RT-07 | La UI aggiorna subito il voto (aggiornamento ottimistico) e lo ripristina se la richiesta fallisce. | M |
| FR-REC-RT-08 | Scorciatoie nella scheda: `O` mi piace molto, `L` mi piace, `D` non mi piace, `W` visto. | C |
| FR-REC-RT-09 | Onboarding **Insegnami i tuoi gusti**: una selezione varia di titoli **già nei cataloghi abilitati**, da votare in fretta (mi piace / non mi piace / salta). | S |
| FR-REC-RT-10 | L’utente marca un titolo come **Visto** (o toglie il flag) dalla scheda. Il flag è globale sul titolo e indipendente dal voto Like/Dislike. | M |
| FR-REC-RT-11 | Un titolo *Visto* non compare in *Per te*, nei simili né nell’onboarding `/taste`. Resta nel catalogo e può avere un voto. | M |

### 3.2 Segnali impliciti (FR-REC-SG)

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-SG-01 | Segnali positivi: riga in `Favorites` (interesse temporaneo / da guardare); riga in `Watched`; `ArrAuditLog` con azione `Add` ed esito `Success`; `ArrLibraryItem.State = Downloaded`. I pesi sono in BR-REC-02. | M |
| FR-REC-SG-02 | Se il titolo ha un voto esplicito, il profilo usa solo quel voto. | M |
| FR-REC-SG-03 | L’utente può disattivare i segnali impliciti nelle impostazioni. | S |
| FR-REC-SG-04 | L’eliminazione da Sonarr/Radarr (`ArrAuditLog` azione `Delete`) non è un segnale negativo. Il segnale «aggiunto» resta. | M |

### 3.3 Raccomandazioni (FR-REC-RC)

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-RC-01 | Pagina **Per te** (`/for-you`): titoli ordinati per punteggio sull’unione delle sorgenti abilitate, scorrimento infinito. | M |
| FR-REC-RC-02 | Sul catalogo della sorgente selezionata (`/catalog`), una **riga dedicata** mostra fino a **25** consigli con scope `source`. La pagina `/for-you` usa lo scope `enabled` (titoli con `Availability` su almeno una sorgente abilitata). | M |
| FR-REC-RC-03 | Nello scope *Tutte le sorgenti abilitate* un titolo compare una volta, con `places` (nome provider e paese) per ogni sorgente abilitata in cui ha una `Availability`. | M |
| FR-REC-RC-04 | Un titolo senza `Availability` su sorgenti abilitate non è mai restituito. | M |
| FR-REC-RC-05 | Senza voti né segnali, *Per te* mostra il prior (livello 0) e lo dichiara, con l’indicatore di apprendimento. | M |
| FR-REC-RC-06 | Ogni elemento riporta i motivi (titoli simili con mi piace, generi o keyword, oppure «molto apprezzato / popolare»). | M |
| FR-REC-RC-07 | Filtri: tipo (film/serie/stand-up), genere, anno, stato libreria. Per default sono esclusi i titoli già in libreria (*Scaricato*, *In download*, *Parziale*, *Mancante*). L’utente può includerli. | M |
| FR-REC-RC-08 | Sono esclusi i titoli che hanno già un voto o sono marcati *Visto*. | M |
| FR-REC-RC-09 | **Titoli simili**, in fondo alla scheda su più righe: vicini del titolo aperto che hanno una `Availability` su almeno una sorgente abilitata, con `places`. Non richiede voti. Non è limitato alla sorgente da cui si è aperta la scheda. | M |
| FR-REC-RC-10 | Diversità: re-ranking MMR sui primi risultati, per non riempire la pagina con un solo genere o una sola saga. | S |
| FR-REC-RC-11 | Esplorazione: una quota di posizioni (default 10%) per titoli lontani dalle abitudini. Disattivabile. | S |
| FR-REC-RC-12 | Azione **Aggiorna raccomandazioni** nelle impostazioni. | M |

### 3.4 Pool (FR-REC-CD)

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-CD-01 | Il pool è l’insieme dei `Title` con almeno una `Availability` su una sorgente abilitata (`CatalogSource.IsEnabled` e `Provider.IsEnabled`). È lo stesso criterio con cui `TitleQueryService` elenca le sorgenti. | M |
| FR-REC-CD-02 | `RemovedAt` valorizzato non toglie il titolo dal pool né dallo scope di quella sorgente. | M |
| FR-REC-CD-03 | I tipi già importati (film, serie, stand-up, FR-CAT-03) sono il perimetro. Non si allarga il catalogo per i consigli. | M |
| FR-REC-CD-04 | Keyword e crediti usati dal livello 1 si persistono a partire dal payload TMDB che l’arricchimento scarica già (`append_to_response=keywords,credits`). Non è una seconda pipeline di scoperta titoli. | M |

### 3.5 Modello locale (FR-REC-ML)

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-ML-01 | Gli embedding si calcolano nel container, con un modello ONNX su CPU. Nessuna chiamata esterna con i dati di gusto. | M |
| FR-REC-ML-02 | Il modello non è nell’immagine di base: si scarica al primo utilizzo, con verifica di integrità, oppure si copia in `/config/models`. | M |
| FR-REC-ML-03 | Senza modello la feature funziona con i livelli 0 e 1. La UI indica lo stato del modello. | M |
| FR-REC-ML-04 | Il modello è sostituibile da configurazione. Al cambio, gli embedding si ricalcolano in background. | S |
| FR-REC-ML-05 | I thread ONNX e la dimensione dei batch sono limitabili. | M |
| FR-REC-ML-06 | Livello supervisionato opzionale, oltre una soglia di voti e solo se batte il livello precedente nella valutazione offline. | C |

### 3.6 Amministrazione (FR-REC-AD)

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-AD-01 | Sezione *Raccomandazioni* nelle impostazioni: stato del modello, copertura embedding, dimensione del pool, numero di voti, stadio, ultima ricostruzione, ultimo errore. | M |
| FR-REC-AD-02 | Azioni: scarica modello, ricalcola embedding, ricostruisci, esegui valutazione offline. | M |
| FR-REC-AD-03 | Valutazione offline (leave-one-out) con confronto tra prior, livello 1, livello 2 (e 3 se attivo). | S |
| FR-REC-AD-04 | Azzerare i dati derivati (profilo, embedding, raccomandazioni) lasciando i voti. Azione separata, con conferma, per cancellare anche i voti. | M |
| FR-REC-AD-05 | Registro locale degli eventi (mostrato, aperto, votato, aggiunto), disattivabile. | C |
| FR-REC-AD-06 | Testo in UI: il calcolo è locale. | S |

### 3.7 Impostazioni (FR-REC-ST)

| ID | Requisito | Pri |
|---|---|---|
| FR-REC-ST-01 | Interruttore della funzione (default attivo). Da spenta, voto e pagine di raccomandazione sono nascosti. I voti già salvati restano. | M |
| FR-REC-ST-02 | Interruttori: segnali impliciti, modello locale, esplorazione. | M |
| FR-REC-ST-03 | La lingua del nome in *Per te* è `CatalogSource.MetadataLanguage` della sorgente selezionata, con fallback inglese, come nel catalogo. | M |
| FR-REC-ST-04 | Pesi, soglie, MMR e quota di esplorazione in una sezione *Avanzate*. | S |

---

## 4. Requisiti non funzionali

| ID | Area | Requisito |
|---|---|---|
| NFR-REC-01 | Lettura | `GET /recommendations` e `GET /titles/{id}/similar` rispondono in meno di 300 ms (p95) sul pool reale dell’installazione. I punteggi sono pre-calcolati; la richiesta filtra per scope e pagina. |
| NFR-REC-02 | Ricostruzione | Ricostruzione sul pool (unione dei cataloghi abilitati) e fino a 500 segnali in meno di 10 secondi su CPU comune. Da misurare (SP-R3). |
| NFR-REC-03 | Embedding | Calcolo in background, con tetto di thread, senza bloccare sync di catalogo e *arr*. |
| NFR-REC-04 | Memoria | Modello quantizzato più i vettori del pool restano nell’ordine di qualche centinaio di MB. Da misurare. |
| NFR-REC-05 | Robustezza | Modello assente o corrotto, errore TMDB o metadati incompleti: l’app resta su, i consigli scendono al livello disponibile. |
| NFR-REC-06 | Riproducibilità | A parità di voti, segnali e catalogo, la ricostruzione produce lo stesso ordine. L’esplorazione usa un seme legato alla versione. |
| NFR-REC-07 | Privacy | I gusti restano in `PickArr.db`. Verso TMDB escono solo id di titoli già in catalogo, per completare i metadati. Nessuna telemetria. |
| NFR-REC-08 | Persistenza | I voti stanno nel DB principale (backup di `/config`) e sono esportabili. |
| NFR-REC-09 | Portabilità | Il modello gira su linux/amd64 e linux/arm64 senza GPU. |
| NFR-REC-10 | Licenze | Modello e dati con licenze compatibili (SP-R5). |

---

## 5. Regole di business

| ID | Regola |
|---|---|
| BR-REC-01 | **Un voto per titolo.** Valori `Love`, `Like` o `Dislike`. Una nuova scelta sostituisce la precedente. La rimozione cancella la riga. Ogni variazione è una riga in `RatingEvents`. |
| BR-REC-02 | **Pesi.** *Mi piace molto* **+1,5**. *Mi piace* **+1,0**. *Non mi piace* **−1,0**. *La mia lista* **+0,5** (segnale temporaneo di interesse / watch-later: sparisce dal profilo quando si toglie il preferito o si assegna un voto). *Visto* **+0,35**. *Aggiunto a Sonarr/Radarr* **+0,6**. *Scaricato* **+0,4**. I segnali impliciti si sommano e si limitano a **[0, +1]**. Con un voto esplicito vale solo quello. Pesi configurabili. |
| BR-REC-03 | **Decadimento.** Opzionale, emivita in giorni. Default: nessuno. |
| BR-REC-04 | **Numerosità efficace.** `N_eff = Σ \|s_i\|` sui titoli con segnale. Governa il peso del profilo rispetto al prior (§12.2). |
| BR-REC-05 | **Stadi.** `cold` se `N_eff < 5`; `warming` se `5 ≤ N_eff < N0` (default 30); `personalized` se `N_eff ≥ N0`. Informativi in UI. Il peso cresce in modo continuo. |
| BR-REC-06 | **Esclusioni dalla lista.** Titoli che hanno un voto o sono *Visto*. Per default anche i titoli con `ArrLibraryItem` in stato *Scaricato*, *In download*, *Parziale* o *Mancante* (filtro disattivabile). Il punteggio di quei titoli si può comunque calcolare: l’esclusione è in lettura. |
| BR-REC-07 | **Appartenenza.** Scope `source`: esiste `Availability` con `CatalogSourceId` uguale alla sorgente richiesta. Scope `enabled`: esiste `Availability` la cui sorgente ha `IsEnabled` e il cui `Provider.IsEnabled` è vero. In entrambi i casi `RemovedAt` è irrilevante. |
| BR-REC-08 | **Identità.** Il candidato è il `Title` già unificato da `CatalogUpserter` (Watchmode id, poi coppia TMDB). Due righe `Titles` restano due voti: succede solo se il catalogo non le ha collegate. Questa feature non aggiunge un altro criterio di merge. |
| BR-REC-09 | **Testo degli embedding.** Si preferisce la localizzazione `en-US` in `TitleLocalizations`. Se manca, si usa la localizzazione già salvata per la lingua della sorgente. |
| BR-REC-10 | **Watchmode.** Nessuna funzione di raccomandazione chiama Watchmode. |
| BR-REC-11 | **Sonarr/Radarr.** Un titolo consigliato si aggiunge con il flusso già esistente (`ArrAddService`), se ha l’id richiesto (BR-08). |
| BR-REC-12 | **Ricostruzione.** Dopo un voto o un cambio di *Visto*, ricostruzione con debounce di 30 secondi, senza aspettare il job notturno. |
| BR-REC-13 | **Sorgente selezionata.** È lo stesso id ricordato dal catalogo (`localStorage` `pickarr.sourceId`, query `source`). *Per te* lo legge e lo scrive. Cambiarlo qui cambia il catalogo alla visita successiva, e il contrario. |
| BR-REC-14 | **Simili e scope.** I simili applicano sempre l’appartenenza `enabled`, anche se *Per te* è sullo scope `source`. |

---

## 6. User stories

**US-REC-01 — Votare in fretta.** Voglio un mi piace o un non mi piace dalla card.
- CA1: il voto si vede subito sulla card e sulla scheda, in qualunque sorgente apra lo stesso titolo.
- CA2: posso cambiarlo o toglierlo.

**US-REC-02 — Un voto solo.** Se segno un titolo su Prime Video, su Netflix lo trovo già votato.
- CA1: esiste una sola riga di voto per quel `Title`.
- CA2: toglierlo da una sorgente lo toglie anche dall’altra.

**US-REC-03 — Consigli nel catalogo che sto guardando.** Mentre consulto Netflix Italia, la riga *For you* sul catalogo contiene titoli con disponibilità su Netflix Italia (al più 25).
- CA1: un titolo solo su Prime Video non compare in quella riga.
- CA2: un titolo rimosso di recente da Netflix Italia può ancora comparire.
- CA3: l’unione delle sorgenti abilitate resta su `/for-you`.

**US-REC-04 — Consigli sul primo giorno.** Senza voti vedo titoli apprezzati e popolari del catalogo, con l’avviso che non è ancora personale.
- CA1: l’indicatore è in stato `cold`.
- CA2: la scheda ha *Titoli simili* anche senza voti.

**US-REC-05 — I gusti viaggiano.** Un mi piace dato su Prime Video alza, dentro Netflix Italia, i titoli di Netflix Italia vicini a quello.
- CA1: il profilo non è spezzato per provider.
- CA2: la lista di Netflix Italia continua a contenere solo titoli con disponibilità su quella sorgente.

**US-REC-06 — Tutte le mie sorgenti.** Voglio una vista unica dei consigli sui cataloghi accesi.
- CA1: ogni titolo compare una volta, con i servizi e i paesi.
- CA2: una sorgente spenta non contribuisce.

**US-REC-07 — Simili anche su un altro servizio.** Nella scheda vedo titoli vicini presenti in almeno un catalogo acceso, con l’indicazione di dove sono.
- CA1: un simile solo su Prime Video è visibile anche se ho aperto la scheda dal catalogo Netflix Italia.
- CA2: un titolo assente da ogni catalogo acceso non compare.

**US-REC-08 — Capire il perché.** Ogni consiglio mostra fino a 3 motivi.

**US-REC-09 — Gusti in casa.** Il modello gira nel container. Posso esportare e cancellare i voti.

**US-REC-10 — Variare.** I primi risultati mescolano generi e saghe.

**US-REC-11 — Accelerare.** L’onboarding propone titoli già nei cataloghi accesi.

**US-REC-12 — Le mie valutazioni.** Vedo tutti i voti, anche di titoli che nella sorgente selezionata non ci sono, con un badge che lo dice.

---

## 7. Casi d'uso

### UC-REC-01 — Votare un titolo

- **Attore:** Utente.
- **Flusso:** click su *Mi piace* (card o scheda) → la UI aggiorna l’icona → `PUT /titles/{id}/rating` → il backend scrive `Ratings` e `RatingEvents` → accoda la ricostruzione (debounce) → l’indicatore di apprendimento si aggiorna al refresh dello stato.
- **Alternativi:** click sul voto già attivo → `DELETE`, il voto sparisce. Errore di rete → la UI torna allo stato precedente.

### UC-REC-02 — Consultare *Per te*

- **Attore:** Utente.
- **Flusso:** apre `/for-you` → la pagina legge la sorgente ricordata dal catalogo → scope default `source` → `GET /recommendations?scope=source&sourceId=` → card con motivi, voto, preferito, stato *arr* e disponibilità (inclusa la rimozione, se c’è).
- **Alternativo:** l’utente passa a *Tutte le sorgenti abilitate* → `scope=enabled` → un titolo, più `places`. Pool vuoto o rebuild mai eseguita → messaggio con link allo stato in impostazioni.

### UC-REC-03 — Titoli simili

- **Attore:** Utente.
- **Flusso:** nella scheda, *Titoli simili* chiama `GET /titles/{id}/similar`. I risultati sono titoli del pool `enabled`, ordinati per vicinanza, con `places`. Il click apre la scheda.

### UC-REC-04 — Onboarding

- **Attore:** Utente.
- **Flusso:** `/taste` mostra titoli del pool, scelti per coprire generi ed epoche → l’utente può chiedere un altro lotto (esclude i titoli già mostrati e conserva i voti già dati) → voti in blocco → una sola ricostruzione a fine invio.

### UC-REC-05 — Installare il modello

- **Attore:** Utente.
- **Flusso:** *Impostazioni → Raccomandazioni → Scarica modello* → file in `/config/models/{modelId}/`, verifica SHA-256, self-test → stato *Pronto* → embedding in background → al raggiungimento della copertura entra il livello 2.
- **Alternativi:** copia manuale e *Rileva*. Hash o self-test falliti → stato *Errore*; i livelli 0 e 1 restano.

### UC-REC-06 — Valutazione offline

- **Attore:** Utente.
- **Flusso:** azione in impostazioni → report con metriche per livello e numerosità. Sotto circa 30 voti il report avvisa che il campione è piccolo.

### UC-REC-07 — Azzerare

- **Attore:** Utente.
- **Flusso:** *Azzera dati derivati* cancella profilo, embedding e raccomandazioni, poi ricostruisce. *Azzera anche i voti* è un’azione distinta e chiede conferma.

---

## 8. Schermate

**Card.** Accanto al cuore dei preferiti, due azioni (mi piace, non mi piace). Il voto attivo resta visibile. Su *Per te* in scope `enabled`, e sui simili, la card mostra `places` come *La mia lista*.

**Scheda (`TitleDialog`).** Azioni etichettate *Mi piace* / *Non mi piace*, *Visto*, e preferito. Scorciatoie `L` / `D` / `W`. Blocco *Perché te lo consigliamo* se si arriva da *Per te*. In fondo, griglia *Titoli simili* su più righe.

**Per te (`/for-you`).** Stesso selettore provider + paese del catalogo (`FilterBar`). Interruttore di scope: *Questa sorgente* / *Tutte le sorgenti abilitate*. Con una sola sorgente abilitata il secondo scope è nascosto. Indicatore `N_eff / N0`. Filtri tipo, genere, anno, includi in libreria. Griglia a scorrimento infinito. Chip dei motivi.

**Onboarding (`/taste`).** Griglia di titoli del pool, azioni grandi, contatore, *Mostra altri titoli*, *Fatto*.

**Le mie valutazioni (`/ratings`).** Due tab, ricerca, azioni sul voto, *Esporta* / *Importa*. Elenco globale. Badge rispetto alla sorgente selezionata e `places` sulle sorgenti abilitate.

**Impostazioni → Raccomandazioni (`/settings/recommendations`).** Interruttori, stato modello, copertura, pool, voti, stadio, azioni di §3.6, azzeramento. I job compaiono anche in *Impostazioni → Job*, come gli altri `SyncRuns`.

Voce **Per te** nell’intestazione, accanto a *La mia lista*. *Le mie valutazioni* è raggiungibile da *Per te* e dalle impostazioni.

---

# Parte B — Tecnica

## 9. Visione d'insieme e ancoraggio al codice

### 9.1 Cosa c’è già e si riusa

| Pezzo | Dove | Uso |
|---|---|---|
| Titolo unico | `Title`, merge in `CatalogUpserter.ResolveTitleAsync` (prima `WatchmodeId`, poi `TmdbType`+`TmdbId`) | Il voto punta a `Title.Id`. |
| Presenza in un catalogo | `Availability` (`TitleId`, `CatalogSourceId`, `RemovedAt`, `AddedAt`, `LeavingDate`) | Filtro di scope. `RemovedAt` non esclude. |
| Sorgente | `CatalogSource` + `Provider` | Sorgente = provider + `CountryCode`. Accesa se `CatalogSource.IsEnabled` e `Provider.IsEnabled`. |
| Nome e trama | `TitleLocalization` (`TitleId`, `Language`) | Lingua della sorgente, fallback `en-US` (`LanguageFallback`). Ci si aggiunge la riga `en-US` se manca, per gli embedding. |
| Generi | `Genre.Id` = id TMDB, `TitleGenre`, `GenreLocalization` | Termini del livello 1, già persistiti. |
| Keyword e cast nel payload | `TmdbMetadataProvider` (`append_to_response=external_ids,keywords,credits`), `TmdbTitleDetails.Keywords`, `CastJson` (solo nomi) | Le keyword oggi servono al rilevamento stand-up e non sono salvate. Il cast non ha id persona. Si estende il mapping, non si apre un altro client. |
| Voto medio e popolarità | `Title.VoteTmdb`, `VoteTmdbCount`, `Popularity` | Prior. |
| Preferiti | `Favorite` (`TitleId` PK) | Segnale implicito e modello UI del toggle ottimistico (`FavoriteButton`). |
| Libreria | `ArrLibraryItem.State`, `ArrAuditLog` (`Add` / `Delete`, `Success` / `Failed`) | Segnali e filtro «in libreria». |
| Sorgente UI | `src/PickArr.WebApp/src/` (`pickarr.sourceId`), query `source` su `/catalog` | Stessa sorgente per `/for-you`. |
| Card | `TitleListItem` / `TitleListItemDto`, `places`, `TitleCard`, `TitleDialog` | Si aggiunge il campo `rating`. |
| Job | Hangfire, code `default`, `sync`, `arr` (`HangfireBootstrap`), `SyncRun` + `SyncRunKinds` | Nuova coda `ml` e nuovi `Kind`. |
| Impostazioni | `AppSetting`, `SettingsKeys` | Chiavi `reco.*`. |
| API | Gruppi `/api/v1/...`, JSON camelCase, pagina `page` / `pageSize` | Stesse convenzioni di `specifica-tecnica.md` §8. |

### 9.2 Flusso

```
Voto / Favorites / ArrAuditLog / ArrLibraryItem
        │
        ▼
Titles già in catalogo + Availabilities
        │  arricchimento esistente, più keyword/crediti/en-US persistiti
        ▼
TitleEmbeddings (se il modello c’è)
        │
        ▼
RecoBuildJob: profilo unico + punteggio su tutto il pool
        │
        ▼
Recommendations (versionate)
        │  in lettura: scope source | enabled, filtri, pagina
        ▼
GET /recommendations , GET /titles/{id}/similar  →  React
```

### 9.3 Decisioni

| # | Decisione | Perché |
|---|---|---|
| 1 | Il pool è l’unione dei titoli che hanno già una `Availability` su una sorgente abilitata. Nessuna colonna `Origin`, nessun titolo `Harvested`. | I consigli devono appartenere ai cataloghi importati. Il merge cross-provider c’è già. |
| 2 | Il punteggio è globale e pre-calcolato. Lo scope si applica in lettura con una join su `Availabilities`. | Un mi piace su Prime Video entra nel profilo; Netflix Italia filtra dopo. |
| 3 | `RemovedAt` non è un predicato delle query di raccomandazione. | La rimozione dal servizio è irrilevante per il consiglio. La griglia del catalogo, nel filtro predefinito, continua a escludere i rimossi (`TitleQueryService`, `includeRemoved`). Sono due query diverse. |
| 4 | Livelli 0 prior, 1 profilo sparso, 2 kNN sugli embedding, 3 classificatore opzionale. Il peso personale cresce con `N_eff`. | Consigli utilizzabili subito, più fini quando ci sono voti e, se serve, il modello. |
| 5 | kNN sui voti, non un solo centroide. | Gusti diversi (thriller e commedia) non vanno mediati in un punto solo. |
| 6 | ONNX Runtime nel processo .NET, modello scaricato a richiesta. Vicini a forza bruta con `System.Numerics.Tensors`. | Una immagine, un processo. La dimensione è quella dei cataloghi, non un indice ANN. |
| 7 | I simili usano sempre l’appartenenza `enabled` e compilano `places`. | Scelta di prodotto, distinta dallo scope di *Per te*. |
| 8 | Zero chiamate Watchmode. | BR-14, BR-REC-10. |

---

## 10. Modello dati

Convenzioni di `specifica-tecnica.md` §4. Tabelle nel DB `PickArr.db`. Migrazione EF additiva.

### 10.1 Colonne nuove su tabelle esistenti

Nessuna colonna obbligatoria su `Titles` per l’appartenenza: basta `Availability`.

Opzionali, se il livello 1 le calcola e le tiene:

| Tabella | Colonna | Note |
|---|---|---|
| `Titles` | `OriginCountries` text null | JSON dei paesi di origine TMDB, se li si legge dal payload già richiesto. Feature del livello 1. |

`CastJson` resta la lista di nomi già salvata. Gli id persona stanno nelle tabelle nuove, così non si rompe il contratto attuale del cast in scheda.

### 10.2 Tabelle nuove

**`Ratings`** — un voto corrente

| Colonna | Tipo | Note |
|---|---|---|
| TitleId | int PK, FK → Titles | |
| Value | text | `Like` / `Dislike` |
| RatedAt | datetime | |

**`RatingEvents`**

| Colonna | Tipo | Note |
|---|---|---|
| Id | int PK | |
| TitleId | int FK | |
| Value | text null | null = voto rimosso |
| At | datetime | |
| Origin | text | `Ui` / `Onboarding` / `Import` |

**`Keywords`**: `Id` (id TMDB) PK, `Name`.
**`TitleKeywords`**: PK `(TitleId, KeywordId)`.

**`People`**: `Id` (id TMDB) PK, `Name`.
**`TitleCredits`**: PK `(TitleId, PersonId, Role)`, `Role` ∈ `Cast` / `Director` / `Creator`, `Order` int. Primi 5 attori; registi per i film; `created_by` per le serie. Richiede di leggere `credits.cast[].id` e `credits.crew` (e `created_by` sulle serie) nel DTO TMDB attuale, che oggi deserializza solo il nome del cast.

**`TitleEmbeddings`**

| Colonna | Tipo | Note |
|---|---|---|
| TitleId | int FK | |
| ModelId | text | es. `all-MiniLM-L6-v2@q8` |
| Dim | int | 384 |
| Vector | blob | float32 little-endian, L2 = 1 |
| TextHash | text | hash del testo; se cambia, si ricalcola |
| ComputedAt | datetime | |

PK `(TitleId, ModelId)`.

**`RecoState`** (una riga)

| Colonna | Tipo | Note |
|---|---|---|
| CurrentVersion | int | |
| BuiltAt | datetime null | |
| RatingCount | int | |
| EffectiveN | real | |
| Stage | text | `cold` / `warming` / `personalized` |
| ModelStatus | text | `Absent` / `Downloading` / `Ready` / `Error` |
| ModelId | text null | |
| EmbeddingCoverage | real | 0–1 sul pool |
| ProfileJson | text null | profilo sparso troncato |
| LastError | text null | |

**`Recommendations`**

| Colonna | Tipo | Note |
|---|---|---|
| Version | int | |
| TitleId | int FK | |
| Score | real | |
| Personal | real null | |
| Prior | real | |
| Rank | int | ordine globale di quella versione, prima dello scope |
| ReasonsJson | text | motivi (§12.7) |

PK `(Version, TitleId)`. Indice `(Version, Score DESC)`. Alla pubblicazione si cancellano le versioni precedenti.

**`RecoEventLog`** (solo se FR-REC-AD-05): `Id`, `At`, `TitleId`, `Event` (`Shown` / `Opened` / `Liked` / `Disliked` / `AddedToArr`), `Version`, `Rank`, `Scope`.

### 10.3 Segnali, senza tabelle duplicate

Alla ricostruzione:

- voto: `Ratings`;
- preferito: `Favorites`;
- aggiunto: `ArrAuditLog` con `Action = Add`, `Outcome = Success`, `TitleId` non nullo;
- scaricato: `ArrLibraryItem` con `State = Downloaded` e `TitleId` non nullo.

Un `Delete` riuscito non inverte l’`Add`.

### 10.4 Migrazione

EF additiva. I titoli già importati entrano nel pool grazie alle `Availability` esistenti. Un backfill, dopo l’estensione dell’arricchimento, rilegge keyword e crediti dai titoli `Ready` (i payload vecchi non le hanno salvate).

---

## 11. Pipeline di dati e job

### 11.1 Coda

In `HangfireBootstrap` le code diventano `default`, `sync`, `arr`, `ml`.

I job di questa feature stanno sulla coda `ml`, con `[DisableConcurrentExecution]` per tipo di job, così un embedding lungo non occupa i worker di `sync` e `arr`. Si aggiunge un secondo `AddHangfireServer` con `WorkerCount = 1` e `Queues = ["ml"]`, e si toglie `ml` dal server esistente. I job si registrano in `SyncRuns` con `Kind`: `RecoFeature`, `RecoEmbedding`, `RecoBuild`, `RecoEval`.

| Job | Trigger | Scopo |
|---|---|---|
| `RecoFeatureJob` | dopo il deploy della persistenza keyword, poi a coda dell’arricchimento e ogni ora | Per i titoli `Ready` senza keyword/crediti salvati, o senza localizzazione `en-US`: completa i dati. |
| `RecoEmbeddingJob` | dopo `RecoFeatureJob`, a modello pronto | Embedding mancanti o con `TextHash` cambiato. |
| `RecoBuildJob` | voto (debounce 30 s), dopo embedding, notturno | Profilo e punteggi; pubblica una versione. |
| `RecoEvalJob` | manuale | §17.3. |
| `RecoModelDownloadJob` | manuale | §13.3. |

Idempotenti, stessa idea di `SyncRuns` (stato, errore, trigger `Schedule` / `Manual`).

### 11.2 Feature: estendere l’arricchimento, non un secondo catalogo

`EnrichmentService` e `TmdbMetadataProvider` restano l’unico punto che chiama TMDB per un titolo.

Modifiche:

- Persistere `TmdbTitleDetails.Keywords` in `Keywords` / `TitleKeywords` dentro `EnrichCoreAsync` (oggi la lista è usata solo da `TmdbStandUp`).
- Estendere `TmdbCreditsDto` con id persona e crew (`Director`) e, per le serie, `created_by`. Mapparli in `TmdbTitleDetails` e salvarli in `People` / `TitleCredits`. `CastJson` continua a essere la lista di nomi per la scheda.
- Se nessuna `TitleLocalization` è `en-US`, una chiamata aggiuntiva `GetMovie` / `GetTv` con `language=en-US`, scritta nella stessa tabella (la chiave è già `(TitleId, Language)`). Vale per gli embedding (BR-REC-09). Il limitatore TMDB già presente si applica anche a questa chiamata.
- `RecoFeatureJob` seleziona i `Title` `Ready` del pool a cui manca uno di questi pezzi e riusa il servizio. Non inserisce titoli.

`OriginCountries`, se si aggiunge la colonna, si legge dallo stesso dettaglio TMDB quando il campo c’è nel DTO.

### 11.3 `RecoEmbeddingJob`

```
testo(t) = "{nome} ({anno}). {generi}. Keywords: {max 15}. {tagline}. {overview}"
           lingua: en-US se presente, altrimenti la localizzazione già salvata
           troncato a 256 token
per batch di titoli senza embedding o con TextHash diverso:
    tokenizza → ONNX → mean pooling → L2
    salva TitleEmbeddings
aggiorna RecoState.EmbeddingCoverage sul pool
```

### 11.4 `RecoBuildJob`

```
1. segnali → s_i , N_eff          (BR-REC-02)
2. profilo sparso                 (§12.3)
3. kNN embedding                  (§12.4) se modello pronto e copertura ≥ soglia
4. prior su tutto il pool        (§12.2)
5. fusione → Score, Reasons      (§12.5, §12.7)
6. transazione: Recommendations(version+1), RecoState, cancella versioni vecchie
```

- Una ricostruzione alla volta. Altri voti durante l’esecuzione accodano un giro successivo (debounce).
- Copertura embedding sotto `reco.embeddings.minCoverage` (default 0,8): il livello 2 è escluso e il peso personale va al livello 1.
- Lo scope non entra nel build. Due scope non significano due punteggi.

---

## 12. Algoritmo di raccomandazione a livelli

### 12.1 Panoramica

```
Score(t) = (1 − w) · Prior(t)  +  w · Personal(t)
w        = min(1, N_eff / N0)                 N0 = 30
Personal = a · Sparse(t) + b · Knn(t)         b = 0 se il modello non c’è; a + b = 1
```

Pesi iniziali `a = 0,4`, `b = 0,6`. Senza modello, `a = 1`. I punteggi componenti sono normalizzati in [0, 1] sul pool prima della fusione.

| Livello | Cosa | Richiede | Attivo da |
|---|---|---|---|
| 0 | Prior | `VoteTmdb`, `VoteTmdbCount`, `Popularity` | sempre |
| 1 | Profilo sparso | segnali + generi (keyword e crediti quando persistiti) | `N_eff > 0` |
| 2 | kNN embedding | modello + copertura | `N_eff > 0` e copertura ≥ 80% |
| 3 | Classificatore | ≥ 100 voti espliciti e validazione | opzionale (§12.6) |

Il profilo somma segnali di **tutte** le sorgenti. Un mi piace su un titolo presente solo su Prime Video alza `Sparse` e `Knn` dei titoli di Netflix Italia vicini a quello. Quei titoli di Prime Video non compaiono nello scope `source` di Netflix Italia perché la join su `Availability` li ferma in lettura.

### 12.2 Livello 0 — Prior

```
bayes(t) = (v/(v+m)) · R + (m/(v+m)) · C
           v = VoteTmdbCount, R = VoteTmdb, C = media del pool, m = 300
pop(t)   = log(1 + Popularity) normalizzato in [0, 1] sul pool
Prior(t) = 0,7 · norm(bayes) + 0,3 · pop
```

Titolo senza voto TMDB: `bayes` cade su `C`, `pop` su 0. Resta nel pool. Il prior si può calcolare per tipo (film / serie / stand-up). Il MMR (§12.8) completa la varietà.

### 12.3 Livello 1 — Profilo sparso

Termini, in ordine di dati disponibili:

| Prefisso | Fonte | Quando |
|---|---|---|
| `g:{genreId}` | `TitleGenre` | già in DB; id = id TMDB |
| `l:{OriginalLanguage}` | `Title` | già in DB |
| `y:{decennio}` | `ReleaseYear` | già in DB |
| `t:{Category}` | `Title.Category` | `movie` / `series` / `standup` |
| `k:{keywordId}` | `TitleKeywords` | dopo §11.2 |
| `p:{personId}` | `TitleCredits` ruolo `Cast` | dopo §11.2 |
| `d:{personId}` | `TitleCredits` `Director` o `Creator` | dopo §11.2 |
| `c:{country}` | `OriginCountries` | se la colonna c’è |

Peso TF-IDF (IDF sul pool). Pesi di dimensione: generi 1,0; keyword 1,0; regia/ideatori 0,8; cast 0,5; lingua e paese 0,3; decennio 0,3; tipo 0,5. Vettore L2.

```
P = Σ_i s_i · x_i
Sparse(t) = cos(P, x_t)
```

I termini negativi arrivano dai *Non mi piace*. Il profilo troncato (circa 200 termini per valore assoluto) sta in `RecoState.ProfileJson`.

Finché keyword e crediti non sono persistiti, il livello 1 usa generi, lingua, decennio e tipo. È abbastanza per R2 iniziale; keyword e persone si accendono da sole quando le tabelle si riempiono.

### 12.4 Livello 2 — kNN

Per ogni titolo del pool, i **K** titoli con segnale più vicini (K = 10) per prodotto scalare (vettori unitari):

```
neighbors(t) = top-K su {i : s_i ≠ 0} per sim(t, i) = e_t · e_i
Knn(t)       = Σ sim(t,i)^γ · s_i  /  Σ sim(t,i)^γ          γ = 3
```

- Il risultato è in [−1, 1] e si porta in [0, 1] prima della fusione.
- Con meno di K vicini sopra `sim_min` (default 0,25), il punteggio si smorza verso 0,5.
- I vicini possono stare su un’altra sorgente: è il profilo unico.

**Titoli simili** (FR-REC-RC-09): per il titolo `q`, i vicini nel pool per `e_q · e_t`. Senza embedding, coseno sparso del livello 1. Poi si tengono solo i titoli con appartenenza `enabled` (BR-REC-14), si tolgono i votati e, se il filtro libreria è attivo, quelli in libreria. L’ordine è la similarità. La risposta include `places`.

### 12.5 Fusione

```
Personal(t) = a · Sparse'(t) + b · Knn'(t)
Score(t)    = (1 − w) · Prior'(t) + w · Personal(t)
```

- `N_eff = 0` → `w = 0` → solo prior.
- `N_eff ≥ N0` → il prior resta un residuo `ε = 0,1`: `Score = 0,1·Prior + 0,9·Personal`.
- Parametri in `Settings` (§16).

### 12.6 Livello 3 — opzionale

Con almeno `reco.ml.minRatings` voti espliciti (default 100, almeno 20 per classe), logistica regolarizzata in ML.NET sulle feature `[x_t hashed, Sparse, Knn, Prior, anno, voto, popolarità]`, bersaglio *Like* contro *Dislike*.

Sostituisce `Personal` solo se nella valutazione offline batte il livello 2 con un margine e un campione sufficienti. Fuori dall’MVP (D-REC-06).

### 12.7 Motivi

Fino a 3, in `ReasonsJson`, calcolati nel build:

| Tipo | Origine | Testo (lo costruisce il frontend) |
|---|---|---|
| `similarTo` | fino a 2 vicini con segnale positivo e similarità sopra soglia | «Simile a *Dark*» |
| `terms` | termini del profilo con contributo maggiore | generi, keyword, persone |
| `popular` | prior alto, contributo personale basso | «Molto apprezzato dal pubblico» |
| `explore` | quota di esplorazione | «Qualcosa di diverso» |

Nel JSON ci sono chiavi e id, non frasi tradotte.

### 12.8 Diversità ed esplorazione

**MMR** (FR-REC-RC-10), in lettura, sui primi 200 risultati dopo scope e filtri:

```
MMR(t) = λ · Score(t) − (1 − λ) · max_{u già scelto} sim(t, u)     λ = 0,7
```

`sim` è il coseno embedding, oppure quello sparso. La lista della prima pagina si tiene in memoria per la combinazione di filtri, con cursore per le pagine successive.

**Esplorazione** (FR-REC-RC-11): frazione `reco.explore.rate` (default 0,10) di posizioni a titoli di punteggio medio-alto e lontani dai termini del profilo (generi mai toccati dai segnali). Seme deterministico da `Version`. Motivo `explore`.

### 12.9 Cold start

- `N_eff = 0`: prior, con MMR.
- Onboarding: 24 titoli del pool ad alta popolarità, scelti per coprire generi ed epoche (greedy su generi; sugli embedding se ci sono). Solo titoli con appartenenza `enabled`. Un refresh passa `exclude` con gli id già mostrati e ripete la selezione sul resto del pool; i voti già dati restano in sessione fino a *Fatto*.
- I simili non richiedono voti.

---

## 13. Modello locale (embedding)

### 13.1 Scelta di partenza

| Aspetto | Scelta |
|---|---|
| Modello | `sentence-transformers/all-MiniLM-L6-v2`, ONNX, variante quantizzata int8 |
| File | circa 23 MB; varianti `avx2`, `avx512`, `avx512_vnni`, `arm64` (da confermare, SP-R2) |
| Vettore | 384 |
| Lunghezza | 256 token |
| Licenza | Apache-2.0 |
| Runtime | `Microsoft.ML.OnnxRuntime`, una sessione, `IntraOpNumThreads` default 2 |
| Tokenizzatore | WordPiece. Scelta .NET nello spike SP-R2 (`Microsoft.ML.Tokenizers` o altra libreria) |
| Pooling | media dei token con maschera di attenzione, poi L2 |

Il testo preferito è inglese perché il modello è inglese. La localizzazione `en-US` si aggiunge in `TitleLocalizations`; nel frattempo si usa il testo già presente (BR-REC-09), accettando un embedding meno stabile finché l’inglese non arriva.

Il downloader sceglie `arm64` su `linux/arm64` e, su `amd64`, `avx512_vnni` > `avx512` > `avx2`. SP-R2 verifica il binario quantizzato con ONNX Runtime.

### 13.2 Testo

```
"{nome} ({anno}). {generi}. Keywords: {k1, … max 15}. {tagline}. {overview}"
```

`TextHash` = SHA-256 del testo.

### 13.3 Distribuzione

- Cartella `/config/models/{modelId}/`: `model.onnx`, tokenizer, `model.json` (dimensione, pooling, lunghezza, hash).
- Download da URL configurabile, SHA-256 fissato nel codice, riprendibile.
- Self-test: embedding di 3 frasi di riferimento, coseno ≥ 0,99 rispetto ai vettori attesi. Altrimenti stato `Error`.
- Copia manuale e azione *Rileva*.
- Immagine `:latest-ml` con il modello dentro: possibile dopo, non nell’MVP.

### 13.4 Sostituzione

`reco.embeddings.modelId` seleziona il descrittore. `TitleEmbeddings` tiene più `ModelId`. Sotto `minCoverage` si usa il modello precedente, se c’è, altrimenti il livello 1. *Azzera dati derivati* cancella gli embedding.

### 13.5 Risorse

- Batch 16–32, pausa breve tra i batch se `reco.embeddings.throttle` è attivo.
- Vettori in memoria come `float[]` contigui. Si caricano al primo uso e si invalidano quando cambia l’insieme degli embedding.
- Velocità da misurare (SP-R2). Il job è in background sulla coda `ml`.

---

## 14. Contratti API

Prefisso `/api/v1`. Errori RFC 7807 come il resto dell’API. Campo nuovo sui DTO esistenti di elenco e scheda:

```json
"rating": "like" | "dislike" | null
```

Si aggiunge a `TitleListItemDto` e al DTO di dettaglio, così catalogo, lista e scheda mostrano lo stesso voto.

### 14.1 Voto

| Metodo | Percorso | Corpo / effetto |
|---|---|---|
| `PUT` | `/titles/{id}/rating` | `{ "value": "like" \| "dislike" }`. Idempotente. `204`. |
| `DELETE` | `/titles/{id}/rating` | Rimuove. Idempotente. `204`. |
| `GET` | `/ratings?value=like\|dislike&q=&page=` | Elenco paginato, stessa forma di `TitleListItem` più `rating` e `places`. |
| `GET` | `/ratings/export` | JSON: `tmdbType`, `tmdbId`, nome, valore, data. Solo titoli che hanno l’id TMDB. |
| `POST` | `/ratings/import` | `{ "mode": "merge" \| "replace", "items": [...] }`. Contatori `imported`, `unknown`, `ignored`. `unknown` = id TMDB non presente in `Titles`. Nessun inserimento di titoli. |
| `POST` | `/ratings/onboarding` | Lista `{ "titleId", "value" }`. Una ricostruzione a fine batch. `value` nullo = salto, non scrive un voto. |

`id` è `Title.Id`. Un voto messo dalla card di Netflix sulla stessa riga letta dalla card di Prime Video.

### 14.2 Raccomandazioni

**`GET /recommendations`**

| Query | Valori |
|---|---|
| `scope` | `source` (default) o `enabled` |
| `sourceId` | Obbligatorio se `scope=source`. È l’id di `CatalogSource`. Con `scope=enabled` è facoltativo e serve solo a scegliere la lingua del nome. |
| `type` | `movie`, `series`, `standup`, ripetibile o CSV, come il catalogo |
| `genre` | id genere, CSV |
| `yearFrom`, `yearTo` | |
| `includeInLibrary` | default `false` |
| `diversify` | default `true` |
| `page`, `pageSize` | default 48, max 100 |

Risposta: pagina come `PagedTitles`, più `version`, `stage`, e su ogni item il blocco `reco`. Con `scope=source` l’item ha `availability` di quella sorgente (campi già usati: `addedAt`, `removedAt`, `leavingDate`, `isNew`). Con `scope=enabled` ha `places` per ogni sorgente abilitata del titolo, anche se `RemovedAt` è valorizzato: in quel caso il place include `removedAt`.

```json
{
  "version": 42,
  "stage": "warming",
  "page": 1,
  "pageSize": 48,
  "total": 912,
  "items": [
    {
      "id": 205,
      "name": "Esempio",
      "originalTitle": "Example",
      "year": 2023,
      "category": "series",
      "posterUrl": "https://image.tmdb.org/t/p/w342/xyz.jpg",
      "genres": [{ "id": 80, "name": "Crime" }],
      "ratingTmdb": 8.1,
      "ratingImdb": null,
      "rating": null,
      "isFavorite": false,
      "availability": {
        "addedAt": "2024-01-10T00:00:00Z",
        "removedAt": null,
        "leavingDate": null,
        "isNew": false
      },
      "places": [{ "providerName": "Netflix", "countryCode": "IT", "removedAt": null }],
      "arr": { "kind": "sonarr", "state": "notInLibrary" },
      "reco": {
        "score": 0.83,
        "personalized": true,
        "reasons": [
          { "kind": "similarTo", "titleId": 101, "name": "Dark", "similarity": 0.78 },
          { "kind": "terms", "terms": ["Crime", "Mistero"] }
        ]
      }
    }
  ]
}
```

`places` nello scope `source` può contenere la sola sorgente richiesta. Nello scope `enabled` contiene tutte le sorgenti abilitate del titolo.

**`GET /titles/{id}/similar?limit=20`**

Niente parametro di scope: l’appartenenza è sempre `enabled` (BR-REC-14). Stessa forma degli item, con `reco.similarity`. `sourceId` facoltativo per la lingua del nome; se manca, si usa la lingua della sorgente selezionata se il client la passa, altrimenti l’inglese di fallback già usato dal dettaglio quando la sorgente non c’è.

**`GET /recommendations/status`**

```json
{
  "enabled": true,
  "stage": "warming",
  "ratingCount": 14,
  "effectiveN": 17.4,
  "targetN": 30,
  "personalWeight": 0.58,
  "model": { "status": "ready", "id": "all-MiniLM-L6-v2@q8", "sizeBytes": 23100000 },
  "embeddings": { "coverage": 0.97, "count": 11840, "pending": 360 },
  "pool": { "titles": 9120 },
  "lastBuiltAt": "2026-11-05T21:30:12Z",
  "version": 42,
  "lastError": null
}
```

**`GET /recommendations/onboarding`** — i 24 titoli del pool (§12.9). Query opzionale `exclude` (CSV di `titleId`) per un nuovo lotto senza i titoli già mostrati.

### 14.3 Amministrazione

Sotto `/api/v1/admin/recommendations`, stesso stile di `AdminEndpoints`.

| Metodo | Percorso | Effetto |
|---|---|---|
| `POST` | `/rebuild` | Accoda `RecoBuildJob`. `202` e id del `SyncRun`. |
| `POST` | `/embeddings/rebuild` | Query `all=true` per ricalcolare tutto. |
| `POST` | `/model/download` | Avvia il download. |
| `POST` | `/model/detect` | Rileva i file copiati a mano. |
| `POST` | `/evaluate` | Avvia `RecoEvalJob`. |
| `GET` | `/evaluations/latest` | Ultimo report. |
| `POST` | `/reset` | Query obbligatoria `includeRatings=true|false`. |

Errori: `RECO_DISABLED` (409), `RECO_MODEL_UNAVAILABLE` (503, solo sulle azioni che richiedono il modello), `RECO_BUILD_IN_PROGRESS` (409).

### 14.4 Aggiornamento della UI

Dopo un voto il client invalida le query `titles`, `title`, `recommendations`, `ratings` e riprova lo stato per qualche secondo finché `version` cambia. Un evento SSE `recommendations-updated` si aggancia se esiste già un canale `/events` (FR-ST-08); altrimenti basta il refetch.

---

## 15. Frontend

### 15.1 File

Si aggiungono feature accanto a quelle esistenti, senza una shell nuova.

```
src/PickArr.WebApp/src/features/ratings/   RatingButtons, MyRatingsPage, import/export
src/PickArr.WebApp/src/features/reco/      ForYouPage, TastePage, LearningProgress, ReasonChips
src/PickArr.WebApp/src/features/settings/  RecommendationsSettingsPage
```

`RatingButtons` si monta in `TitleCard` e `TitleDialog`, accanto a `FavoriteButton`.
`SimilarRow` si monta in fondo a `TitleDialog` come griglia multi-riga.
`ForYouPage` riusa `FilterBar` (selettore provider/paese), `TitleGrid`, `TitleDialog`.

### 15.2 Route

In `AppRouter`, dentro `AppShell`:

| Percorso | Pagina |
|---|---|
| `/for-you` | *Per te*. `scope` nei search params. `source` è quello condiviso. |
| `/for-you?title=:id` | Scheda sulla griglia, come `/catalog?title=`. |
| `/taste` | Onboarding |
| `/ratings` | Le mie valutazioni |
| `/settings/recommendations` | Sezione in `SettingsLayout` |

`Header`: link *Per te* tra catalogo e *La mia lista*.

### 15.3 Sorgente condivisa

`ForYouPage` chiama `loadPreferredSourceId` / `savePreferredSourceId` e, quando le sorgenti sono caricate (`GET /catalog/sources`), scrive `source` nei search params come fa `CatalogPage`. Cambiare provider o paese aggiorna la stessa chiave. Lo scope `enabled` non cambia la sorgente ricordata: serve ancora per la lingua e per il badge in *Le mie valutazioni*.

### 15.4 Stato

- `useRecommendations` con `useInfiniteQuery`, chiave che include `scope` e `sourceId`.
- `useSimilar(titleId)`.
- `useRecoStatus()`.
- Mutazione del voto sullo stesso schema di `FavoriteButton`: ottimistica sulle chiavi `['titles']`, `['title', id]`, `['recommendations']`, `['ratings']`, rollback su errore.
- Un *Non mi piace* su una card di *Per te* la toglie subito dalla lista, perché i votati non sono consigli (FR-REC-RT-04).
- Lo scope scelto sta nei search params della pagina (condivisibile). La sorgente sta in `localStorage`, come il catalogo.

### 15.5 Componenti

- **`RatingButtons`**: due pulsanti. Il voto attivo è premuto. Un secondo click sullo stesso lo rimuove.
- **`LearningProgress`**: `N_eff / targetN` e lo stadio. Link a `/taste`.
- **`ReasonChips`**: chiavi i18n `reco.reason.similarTo`, `reco.reason.terms`, `reco.reason.popular`, `reco.reason.explore`.
- **Scope**: due voci. Con una sola sorgente abilitata resta solo *Questa sorgente*.
- **`OnboardingTaste`**: 24 card, *Mostra altri titoli*, *Fatto*, scorciatoie `L` e `D`.

### 15.6 i18n

Namespace `reco` e `ratings` in `en.json` e `it.json`. Chiave mancante → inglese (FR-I18N-02).

---

## 16. Configurazione

Chiavi in `AppSetting` / `SettingsKeys`, lette come le altre impostazioni.

| Chiave | Default | Note |
|---|---|---|
| `reco.enabled` | `true` | FR-REC-ST-01 |
| `reco.useImplicit` | `true` | |
| `reco.weights.like` | `1.0` | |
| `reco.weights.dislike` | `-1.0` | |
| `reco.weights.favorite` | `0.5` | |
| `reco.weights.arrAdd` | `0.6` | |
| `reco.weights.downloaded` | `0.4` | |
| `reco.halfLifeDays` | `0` | 0 = nessun decadimento |
| `reco.n0` | `30` | |
| `reco.priorResidual` | `0.1` | ε |
| `reco.sparse.weight` | `0.4` | |
| `reco.knn.weight` | `0.6` | |
| `reco.knn.k` | `10` | |
| `reco.knn.gamma` | `3` | |
| `reco.knn.simMin` | `0.25` | |
| `reco.mmr.lambda` | `0.7` | 0 disattiva il MMR |
| `reco.explore.rate` | `0.10` | 0 disattiva |
| `reco.excludeInLibrary` | `true` | default del filtro in lettura |
| `reco.embeddings.enabled` | `true` | ha effetto se il modello c’è |
| `reco.embeddings.modelId` | `all-MiniLM-L6-v2@q8` | |
| `reco.embeddings.threads` | `2` | |
| `reco.embeddings.batchSize` | `16` | |
| `reco.embeddings.minCoverage` | `0.8` | |
| `reco.embeddings.throttle` | `true` | |
| `reco.ml.minRatings` | `100` | livello 3 |
| `reco.events.enabled` | `false` | |

Variabili d’ambiente:

| Variabile | Default | |
|---|---|---|
| `PickArr__Models__Path` | `/config/models` | |
| `PickArr__Models__DownloadBaseUrl` | URL del repository del modello | mirror interno |
| `PickArr__Reco__MaxThreads` | `2` | tetto ONNX, vince se è più basso dell’impostazione |

Non c’è una lingua di visualizzazione separata: si usa `CatalogSource.MetadataLanguage`.

---

## 17. Test e valutazione

### 17.1 Automatici

| Livello | Cosa |
|---|---|
| **Unit — algoritmo** | Funzioni pure in `PickArr.Core`: vettori sparsi, TF-IDF, profilo, kNN, prior, fusione, MMR, esplorazione deterministica. Dataset sintetico: solo fantascienza nei mi piace → fantascienza in cima; un mi piace non peggiora l’ordine dei simili; un non mi piace abbassa i vicini; zero segnali → ordine = prior. |
| **Proprietà** | Componenti in [0, 1]. Nessun titolo con voto nella pagina di consigli. Stesso input → stesso ordine. |
| **Unit — appartenenza** | Scope `source` tiene i titoli con `Availability` su quella sorgente anche se `RemovedAt` è valorizzato. Scope `enabled` tiene l’unione delle sorgenti accese, una riga per `Title`. Una sorgente spenta non contribuisce. Un titolo senza `Availability` non entra. |
| **Unit — voto** | Secondo `PUT` sostituisce il valore. `DELETE` toglie la riga e scrive un evento con valore nullo. Due sorgenti, stesso `Title.Id`: un voto. |
| **Unit — segnali** | Preferito, `Add` riuscito e `Downloaded` si sommano e si tagliano a 1. Un voto esplicito li sostituisce. `Delete` non nega l’`Add`. |
| **Unit — import** | Id TMDB sconosciuto → `unknown`, nessuna riga `Titles` nuova. |
| **Integrazione** | `WebApplicationFactory`: voto → rebuild → `GET /recommendations` per i due scope. Titolo su Netflix IT e Prime Video: una volta nello scope `enabled`, con due place; nello scope Netflix IT una volta, e il mi piace dato «dall’altra parte» è lo stesso `rating`. Simili: un vicino solo su Prime Video è restituito. |
| **Modello** | Test condizionale, saltato se il file non c’è: frasi di riferimento, coseno ≥ 0,99; stesso testo → stesso vettore. |
| **Frontend** | Mutazione ottimistica e rollback. Scope. `ReasonChips`. Sorgente condivisa: cambiare provider su *Per te* aggiorna `pickarr.sourceId`. |

### 17.2 Dati

`tests/fixtures/reco/`: titoli con generi e, dove serve, embedding sintetici. Nessun dato reale.

### 17.3 Valutazione offline

Leave-one-out sui voti: si ricalcola il profilo senza quel titolo e si confronta il suo punteggio con un campione di titoli del pool non votati.

Metriche: AUC, hit@10, AUC *Like* contro *Dislike*. Confronto tra prior, livello 1, livello 2, combinazione, livello 3. Il report include la numerosità. Sotto circa 30 voti avvisa che il numero è basso.

Serve a tarare `a`, `b`, `N0`, `γ`, `K` e a decidere il livello 3. Non a inseguire i decimali con pochi voti.

### 17.4 Eventi

Se `reco.events.enabled`: (mi piace o aggiunte a Sonarr/Radarr tra i titoli mostrati in *Per te*) / (titoli mostrati), per versione e stadio. Solo in locale.

---

## 18. Build, container e CI

- Dipendenze: `Microsoft.ML.OnnxRuntime` (CPU, native `linux-x64` e `linux-arm64`), un tokenizzatore WordPiece (SP-R2), `System.Numerics.Tensors`. `Microsoft.ML` solo se si implementa il livello 3.
- Dockerfile: verificare il caricamento della libreria nativa sull’immagine `aspnet` per amd64 e arm64, e che `/config/models` sia scrivibile dall’utente non root (SP-5).
- CI: i test di algoritmo e API nel job di test del backend; un job manuale scarica il modello ed esegue i test condizionali; lo smoke dell’immagine carica una sessione ONNX minima; lo schema OpenAPI include i percorsi nuovi.
- `README` / `NOTICE`: licenza del modello. Nessuna attribuzione JustWatch: i watch providers non si usano.

---

## 19. Rischi, spike e decisioni aperte

### 19.1 Spike

| ID | Domande |
|---|---|
| **SP-R1** | Il payload TMDB già richiesto basta per keyword, id persona, crew e `created_by` senza cambiare URL? Costo della chiamata extra `language=en-US` sul limitatore attuale. |
| **SP-R2** | Tokenizzatore .NET, pooling, vettori di riferimento, titoli/secondo su x64 e arm64, variante quantizzata, RAM, dimensione immagine, qualità dei simili su un campione del catalogo reale. |
| **SP-R3** | Tempo di rebuild sul pool dell’installazione (una e più sorgenti) con 500 segnali. Tempo di `GET /recommendations` con la join su `Availabilities` e il filtro libreria. MMR in lettura. |
| **SP-R4** | Con 30–50 voti reali: confronto livelli 0/1/2 e prima taratura di `a`, `b`, `N0`, `γ`, `K`, `m`. |
| **SP-R5** | Termini d’uso TMDB per inferenza locale sui testi già in cache, e licenza del modello. Controllo manuale prima del rilascio. |

### 19.2 Rischi

| ID | Rischio | Mitigazione |
|---|---|---|
| RR-01 | Pochi voti. | Prior, simili senza voti, onboarding, segnali impliciti, indicatore. |
| RR-02 | Bolla di filtraggio. | Esplorazione, MMR, prior residuo, azzeramento. |
| RR-03 | Titolo con metadati sottili (non arricchito, senza generi). | Resta nel pool con prior debole. Il livello 1 lo tratta come vettore quasi vuoto. |
| RR-04 | CPU debole (NAS ARM). | Modello piccolo, un worker `ml`, copertura minima prima del livello 2, livello 1 sempre disponibile. |
| RR-05 | Download del modello impossibile. | Copia manuale, hash, self-test. L’app funziona senza. |
| RR-06 | La query di *Per te* copia il filtro `RemovedAt == null` del catalogo e nasconde titoli che vanno consigliati. | BR-REC-07 e test di appartenenza dedicati. Predicato scritto in un solo punto, diverso da `TitleQueryService` sulla vista griglia. |
| RR-07 | Due righe `Titles` per lo stesso film, perché il merge del catalogo non le ha unite. | Il voto segue `Title.Id` (BR-REC-08). Non si aggiunge un merge in questa feature. |
| RR-08 | Segnali impliciti fuorvianti (scaricato per un’altra persona). | Pesi sotto al voto, interruttore, voto esplicito che prevale. |
| RR-09 | Termini TMDB sull’uso dei testi per un modello locale. | SP-R5. Inferenza soltanto, nessun modello ridistribuito addestrato su quei testi. |
| RR-10 | Scritture SQLite durante il rebuild. | Coda `ml` a un worker, transazione di pubblicazione, WAL. |

### 19.3 Decisioni

| ID | Decisione |
|---|---|
| D-REC-01 | Scope predefinito di *Per te*: la sorgente selezionata (`source`). |
| D-REC-02 | L’altro scope è l’unione delle sorgenti abilitate. Non esiste uno scope oltre i cataloghi importati. |
| D-REC-03 | Il profilo è unico. Il filtro di catalogo si applica alla lista, non all’apprendimento. |
| D-REC-04 | I simili usano sempre l’unione delle sorgenti abilitate e mostrano `places`. |
| D-REC-05 | `RemovedAt` non esclude. |
| D-REC-06 | Voti: solo *Mi piace* e *Non mi piace*. |
| D-REC-07 | Pagina dedicata `/for-you`. L’ordinamento *Per te* nella griglia del catalogo è fuori ambito. |
| D-REC-08 | *Le mie valutazioni* è globale, con badge sulla sorgente selezionata. L’onboarding pesca nel pool dei cataloghi accesi. |
| D-REC-09 | Titoli già in libreria esclusi di default, con interruttore. |
| D-REC-10 | Funzione attiva di default. Il modello locale si scarica su richiesta. |
| D-REC-11 | Modello di partenza: `all-MiniLM-L6-v2` quantizzato, da rivedere dopo SP-R2 e SP-R4. |
| D-REC-12 | Livello 3 fuori dall’MVP. |
| D-REC-13 | Segnali impliciti attivi di default, disattivabili. |

---

## 20. Fasi di rilascio

| Fase | Contenuto | Valore |
|---|---|---|
| **R1 — Voto e prior** | `Ratings`, `RatingEvents`, pulsanti, campo `rating` sui DTO, pagina valutazioni, export/import, `/for-you` con prior, i due scope, sorgente condivisa, indicatore. | Si vota subito. I primi consigli sono i titoli forti del catalogo che si sta guardando. |
| **R2 — Profilo** | Persistenza keyword/crediti e `en-US` sull’arricchimento esistente, livello 1, segnali impliciti, motivi, simili via profilo sparso, rebuild con debounce. | I voti, anche quelli dati su un altro servizio, spostano l’ordine dentro il catalogo corrente. |
| **R3 — Modello** | Download, embedding, kNN, simili via embedding, pannello impostazioni, MMR, esplorazione, onboarding. | Consigli più fini quando i gusti sono più di un genere. |
| **R4 — Misura** | Valutazione offline, taratura, eventi, livello 3 se i numeri lo reggono. | Si vede se un livello è meglio del precedente. |

Ordine: **R1 → R2 → R3 → R4**. SP-R1 prima di R2. SP-R2 prima di R3.

---

## 21. Impatto sui documenti esistenti

**`documento-funzionale.md`**

- Ambito: i consigli personalizzati sono pianificati, con il vincolo di questo documento (voto sul titolo, liste filtrate sui cataloghi importati).
- Decisioni: riportare D-REC-01 … D-REC-13.
- Fasi: R1–R4 dopo l’MVP del catalogo.
- FR-DET-04: azioni di voto in scheda. FR-UI-04: voto sulla card.
- FR-FAV-02 resta il modello del dato globale: il voto si comporta come il preferito rispetto alla sorgente, con valori *Mi piace* / *Non mi piace* al posto del booleano.

**`specifica-tecnica.md`**

- §4: tabelle di §10. Nessuna colonna `Origin` su `Titles`.
- §3: modulo `PickArr.Core` per l’algoritmo puro; persistenza, ONNX e il completamento metadati in Infrastructure, agganciati a `EnrichmentService`.
- §6: coda Hangfire `ml`, second server a un worker, `SyncRunKinds` nuovi.
- §8: percorsi di §14, campo `rating` sui DTO di elenco e dettaglio.
- §9: route e feature di §15.
- §12: chiavi di §16.
- §14: dipendenze di §18.
- §17: spike SP-R1 … SP-R5.
- §10: cartella `/config/models` e hash del file scaricato.
