# AGENT KB — PERSONALIZZAZIONI COMUNI

Workflow canonico: skill `incremental-dev-planning`, invocata prima di usare questa KB.

## 1. Vincoli comuni attivi

| ID | Vincolo | Fonte |
| --- | --- | --- |
| C-01 | Documenti KB in italiano | skill + approvazione 2026-11-04 |
| C-02 | Nessun commit senza richiesta esplicita dell'utente | user rules |
| C-03 | Non modificare file fuori dallo scope del task corrente | skill |

## 2. Convenzioni locali

| ID | Convenzione |
| --- | --- |
| CONV-01 | Namespace e progetto: `Pickarr.*` |
| CONV-02 | CI: GitLab CI con **buildah** (non docker buildx); registry GitLab |
| CONV-03 | Immagine Docker target: solo `linux/amd64` |
| CONV-04 | Comandi verifica tipici: `dotnet test` in `backend/`; `npm test` / `npm run lint` in `frontend/` |

## 3. Riferimenti

- Specifica funzionale: `docs/documento-funzionale.md` v0.1
- Specifica tecnica: `docs/specifica-tecnica.md` v0.1
