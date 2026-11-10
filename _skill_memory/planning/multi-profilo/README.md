# Knowledge Base per Refactoring Incrementale

> Questo file vive dentro `_skill_memory/planning/<dominio>/` del progetto: è la guida per
> chi legge o continua questa KB. Per installare o configurare la skill stessa,
> vedi il `README.md` del pacchetto `incremental-dev-planning`.

Guida rapida per usare questo sistema di documenti.

---

## Struttura file

| File              | Obbligatorio | Creato quando                          |
| ----------------- | ------------ | -------------------------------------- |
| `AGENT_KB.md`     | ✅            | Prima sessione (addendum dominio)      |
| `ANALYSIS.md`     | ✅            | Assessment iniziale                    |
| `STATUS.md`       | ✅            | Dopo assessment, insieme ad ANALYSIS   |
| `PLAN.md`         | ✅            | Dettagli task per tranche, prima dell'approvazione |
| `ARCHIVE.md`      | ✅            | Creare vuoto subito, riempire on-demand|
| `BASELINE.md`     | ⚠️ opzionale | Solo se KPI misurabili                 |

---

## Setup nuovo dominio

1. Crea `_skill_memory/planning/<dominio>/`
2. Crea il file comune `_skill_memory/planning/AGENT_KB.md` se assente, senza sovrascriverlo
3. Copia i template obbligatori, compila i campi e rimuovi commenti, esempi e placeholder
4. In PLAN.md definisci scope/acceptance; in STATUS.md stati/dipendenze e ultima tranche indipendente
5. Usa `/incremental-dev-planning <richiesta>` per riprendere il dominio

---

## Percorso di lettura agente

```
AGENT_KB.md (personalizzazioni comuni) → AGENT_KB.md (dominio/vincoli)
→ STATUS.md §0 + sole righe roadmap pertinenti → PLAN.md (tranche corrente)
→ requisiti/findings/decisioni/evidenze per ID, su richiesta
```

Bootstrap orientativo: circa 1.000 token; pacchetto operativo: circa 1.500-2.500,
esclusi codice e risultati strumenti. Sono stime, non motivi per omettere vincoli o prove.
Il pacchetto è una vista delle fonti canoniche, non un altro documento da mantenere.
Lo storico non si legge per intero: usare indice e ID. Workflow completo solo nella skill.

---

## Ciclo di vita

```
Sessione 1 — Assessment
  → crea ANALYSIS.md, STATUS.md, PLAN.md, ARCHIVE.md (vuoto)
  → opzionale: BASELINE.md

Sessioni 2–N — Tranche operative
  → orienta (KB + STATUS §0)
  → confronta SHA e snapshot locale (senza git: manifest con hash)
  → riprendi IN_PROGRESS, rivaluta BLOCKED, poi seleziona primo TODO non bloccato
  → carica solo il pacchetto della tranche corrente dalle fonti canoniche
  → esegui (scope stretto)
  → verifica (test PASS, no regressioni)
  → aggiorna STATUS.md
  → archivia se trigger scatta
  → handoff: nuovo agente con contesto separato oppure nuova sessione per la tranche successiva

Ultima tranche — Verifica finale indipendente
  → nuovo agente con contesto separato e mandato neutro, senza correzioni del revisore
  → se non disponibile: handoff a nuova sessione, task BLOCKED
  → verifica requisiti/codice/test, regressioni e integrazione sulla versione identificata
  → rapporto in ARCHIVE.md, esito e versione in STATUS.md §9
  → FAIL/INCONCLUSIVE: correggi/sblocca e ripeti la verifica indipendente

Chiusura dominio
  → PASS indipendente valido sulla versione corrente
  → tutti i task DONE o DEFERRED con approvazione esplicita
  → finding tutti in stato finale
  → retrospettiva in ARCHIVE.md
  → COMPLETATO oppure CHIUSO CON RINVII APPROVATI
```

---

## Regole chiave

- Log e decisioni: **append-only**; stato findings aggiornabile con transizioni nel log
- STATUS.md: stato/indice; PLAN.md: scope/acceptance; ANALYSIS.md: requisiti e conoscenza tecnica
- §0 è derivato, non una seconda fonte degli stati; evidenze estese solo tramite riferimenti
- FACT: fonte/versione; DECISION: approvazione/motivazione; HYPOTHESIS: controllo da eseguire
- Record tecnici: indicare quando rivalidare; non trasformare ipotesi in fatti nei riassunti
- BASELINE.md: **immutabile** dopo creazione
- ANALYSIS.md: scoperte nuove → §7 (mai riscrivere sezioni precedenti)
- Ogni task: scope + acceptance + size + rollback
- Scope/acceptance/dipendenze definiti prima di approvare il piano
- Rinvii/rischi accettati: motivazione, impatto, owner e approvazione utente
- Senza PASS indipendente niente chiusura; non sostituirlo con autoverifica o DEFERRED
- Modifiche a requisiti/codice/test/configurazione invalidano PASS: DA RIPETERE e riapertura
- Il contesto separato riduce il bias, non lo elimina: dichiarare limiti e materiali ereditati
- Compattare o chiedere di dimenticare non svuota la context window; non riusare il contesto esecutivo tra tranche
