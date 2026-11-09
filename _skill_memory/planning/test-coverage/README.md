# Knowledge Base per Refactoring Incrementale

> Questo file vive dentro `_skill_memory/planning/<dominio>/` del progetto: è la guida per
> chi legge o continua questa KB. Per installare o configurare la skill stessa,
> vedi il `README.md` del pacchetto `incremental-dev-planning`.

Guida rapida per usare questo sistema di documenti.

---

## Struttura file

| File | Obbligatorio | Creato quando |
| --- | --- | --- |
| `AGENT_KB.md` | ✅ | Prima sessione (addendum dominio) |
| `ANALYSIS.md` | ✅ | Assessment iniziale |
| `STATUS.md` | ✅ | Dopo assessment, insieme ad ANALYSIS |
| `PLAN.md` | ✅ | Dettagli task per tranche, prima dell'approvazione |
| `ARCHIVE.md` | ✅ | Creare vuoto subito, riempire on-demand |
| `BASELINE.md` | ⚠️ opzionale | Solo se KPI misurabili |

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
  → confronta SHA e snapshot locale
  → riprendi IN_PROGRESS, rivaluta BLOCKED, poi seleziona primo TODO non bloccato
  → esegui (scope stretto) → verifica → aggiorna STATUS.md → archivia se trigger
  → handoff: nuovo agente/contesto per la tranche successiva

Ultima tranche — Verifica finale indipendente
  → revisore in contesto separato, senza correzioni
  → rapporto in ARCHIVE.md, esito in STATUS.md §9

Chiusura dominio
  → PASS indipendente valido → COMPLETATO oppure CHIUSO CON RINVII APPROVATI
```

---

## Regole chiave

- Log e decisioni append-only; STATUS = stato/indice; PLAN = scope/acceptance
- Senza PASS indipendente niente chiusura; non sostituirlo con autoverifica
- Dominio: `test-coverage`
