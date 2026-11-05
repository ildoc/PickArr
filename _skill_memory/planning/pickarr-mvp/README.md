# Knowledge Base per Refactoring Incrementale

> Questo file vive dentro `_skill_memory/planning/pickarr-mvp/` del progetto: è la guida per
> chi legge o continua questa KB. Per installare o configurare la skill stessa,
> vedi il `README.md` del pacchetto `incremental-dev-planning`.

Guida rapida per usare questo sistema di documenti.

---

## Struttura file

| File | Obbligatorio | Creato quando |
| ---- | ------------ | ------------- |
| `AGENT_KB.md` | sì | Prima sessione (addendum dominio) |
| `ANALYSIS.md` | sì | Assessment iniziale |
| `STATUS.md` | sì | Dopo assessment, insieme ad ANALYSIS |
| `PLAN.md` | sì | Dettagli task per tranche, prima dell'approvazione |
| `ARCHIVE.md` | sì | Creare vuoto subito, riempire on-demand |
| `BASELINE.md` | sì (KPI misurabili) | Assessment iniziale |

---

## Percorso di lettura agente

```
AGENT_KB.md (comune) → AGENT_KB.md (dominio)
→ STATUS.md §0 + sole righe roadmap pertinenti → PLAN.md (tranche corrente)
→ requisiti/findings/decisioni/evidenze per ID, su richiesta
```

---

## Ciclo di vita (sintesi)

1. Assessment → KB creata (questa sessione)
2. Tranche operative TR-01…TR-09 con handoff tra sessioni
3. TR-10 verifica finale indipendente obbligatoria
4. Chiusura solo con PASS §9

Dominio: **pickarr-mvp**. Ripresa: `/incremental-dev-planning` sul dominio esistente.
