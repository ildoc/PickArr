# AGENT KB — raccomandazioni (ADDENDUM)

Addendum dominio `raccomandazioni`.
Personalizzazioni comuni in: `_skill_memory/planning/AGENT_KB.md`.
Workflow canonico nella skill `incremental-dev-planning`, non duplicato qui.

---

## 1. File locali — ordine di lettura

1. Questo file (`AGENT_KB.md`)
2. `STATUS.md` § 0 — Quick Context
3. `STATUS.md` § 1 — sole righe necessarie a riconciliare/selezionare task e dipendenze
4. `PLAN.md` — solo tranche corrente; altri record recuperati per ID su richiesta

---

## 2. Focus operativo

1. Fonte prodotto: `docs/raccomandazioni.md` v0.2 (ID `FR-REC-*`, `BR-REC-*`, `D-REC-*`, `SP-R*`). Non inventare scope oltre quel documento, `AGENTS.md` e i documenti collegati.
2. Ordine di rilascio: spike → R1 → R2 → R3 → R4 → docs prodotto → verifica finale indipendente. Non saltare le dipendenze spike (SP-R1 prima di R2 enrichment; SP-R2 prima di R3 ONNX).
3. Predicato di appartenenza raccomandazioni ≠ filtro griglia catalogo: `RemovedAt` non esclude (BR-REC-07 / RR-06). Un solo helper di query dedicato.
4. Core resta puro: algoritmo in `PickArr.Core`; ONNX, EF, Hangfire, TMDB in Infrastructure. UI in `src/PickArr.WebApp` (non nel path `frontend/` citato nel doc §15).

---

## 3. Vincoli tecnici non negoziabili

| ID | Vincolo | Fonte |
| --- | --- | --- |
| V-REC-01 | Dominio nuovo isolato: non modificare `_skill_memory/planning/pickarr-mvp/` | utente 2026-11-06 |
| V-REC-02 | Nessun commit/push senza richiesta esplicita | utente + AGENT_KB comune C-02 |
| V-REC-03 | `reco.enabled` (default true) già in R1: UI/API di voto e pagine reco rispettano il flag | utente 2026-11-06; FR-REC-ST-01 |
| V-REC-04 | Voto unico su `Title.Id`; pool = Availability su sorgenti abilitate; zero chiamate Watchmode dalla feature | D-REC / BR-REC in `docs/raccomandazioni.md` |
| V-REC-05 | i18n solo `en.json` (namespace `reco` / `ratings`); niente `it.json` in questo dominio | utente 2026-11-06; AGENTS.md |
| V-REC-06 | Perimetro piano = R1→R4 + tutti i requisiti M/S/C del documento; livello 3 da implementare in R4; eventi (FR-REC-AD-05) in R4 default off | utente 2026-11-06 |
| V-REC-07 | SP-R5 (licenze TMDB/modello) = nota rischio in ANALYSIS, non task di roadmap | utente 2026-11-06 |
| V-REC-08 | Aggiornare `documento-funzionale.md` e `specifica-tecnica.md` (§21) in tranche docs prima della verifica finale | utente 2026-11-06 |
| V-REC-09 | Layout repo: `src/PickArr.*`, `tests/*`, comandi in `AGENTS.md` (non i path `backend/`/`frontend/` dello AGENT_KB comune CONV-04 se divergono) | AGENTS.md |

---

## 4. Definition of Done locale

Oltre ai criteri comuni, una tranche `raccomandazioni` è chiusa solo se:

- Test automatici del perimetro toccato PASS (`dotnet test` filtrato e/o `npm test` / typecheck / lint sul WebApp quando si tocca UI).
- Acceptance collegata a ID `FR-REC-*` / `BR-REC-*` con evidenza (comando o passi).
- Nessuna regressione su catalogo, preferiti, Sonarr/Radarr, sync Hangfire esistenti.
- Se la tranche introduce predicatidi pool/scope: test espliciti su `RemovedAt` e sorgente disabilitata.

---

## 5. Tooling dominio-specifico

- Tooling: STATUS.md §8
- Spec feature: `docs/raccomandazioni.md`
- Comandi tipici: vedi `AGENTS.md` (`dotnet test`, `npm test -- --run`, `npm run typecheck`, `npm run lint`)
