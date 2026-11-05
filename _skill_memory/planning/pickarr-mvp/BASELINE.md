# pickarr-mvp BASELINE

> Data analisi: 2026-11-04
> Branch analizzato: `main` (greenfield)
> Analista: agente incremental-dev-planning

> Documento immutabile — non aggiornare dopo la creazione.

---

## 1. Componenti coinvolti

| Componente / Classe | Responsabilità | File | LOC circa |
| ------------------- | -------------- | ---- | --------- |
| Documento funzionale | Requisiti prodotto | `docs/documento-funzionale.md` | ~616 righe |
| Specifica tecnica | Decisioni implementative | `docs/specifica-tecnica.md` | ~1388 righe |
| Codice applicativo | — | assente | 0 |

---

## 2. Flussi as-is

Nessun flusso runtime. Comportamento attuale: repository documentazione-only.

### FLUSSO-00 — Nessun runtime

- **Trigger**: nessuno
- **File sorgente**: n/a
- **Sequenza operazioni**: n/a
- **Dipendenze**: nessuna
- **Output atteso**: n/a
- **Problemi noti**: implementazione da zero

---

## 3. Metriche baseline

| Metrica | Valore baseline | Comando/metodo misura |
| ------- | --------------- | --------------------- |
| Test unit backend | 0 | `dotnet test` (assente) |
| Test frontend | 0 | `npm test` (assente) |
| Progetti .NET | 0 | filesystem |
| Bundle SPA | assente | — |
| Pipeline CI | assente | — |
| Immagine Docker | assente | — |
| Titoli in DB | 0 | — |
| p95 lista/ricerca | n/d | NFR-01 da misurare in T25 |

---

## 4. Note tecniche

- API key Watchmode disponibile presso l'utente (non nel repo).
- Nessuna istanza Sonarr/Radarr swagger accessibile per spike: solo docs ufficiali.
- Ambiente locale: Docker, .NET 10 SDK, Node 24 dichiarati presenti.
