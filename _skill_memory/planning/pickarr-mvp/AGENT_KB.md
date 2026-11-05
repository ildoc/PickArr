# AGENT KB — pickarr-mvp (ADDENDUM)

Addendum dominio `pickarr-mvp`.
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

1. Seguire l'ordine di implementazione di `docs/specifica-tecnica.md` §18.
2. MVP = solo requisiti **Must (M)** + decisioni utente esplicite (filtro Arr, sezione scadenze).
3. Frontend stack vincolante ma **semplificabile** (deferred virtualizzazione/shadcn avanzato se non necessario).
4. Spike (SP-*) possono essere distribuiti su più tranche; output in note + `tests/fixtures/`.

---

## 3. Vincoli tecnici non negoziabili

| ID | Vincolo | Fonte |
| --- | --- | --- |
| V-01 | Scope prodotto: solo Netflix IT/US; niente nuovi servizi in UI MVP | utente 2026-11-04; FR-CAT-01 |
| V-02 | Solo Must; password, change feed, Could → fuori scope | utente 2026-11-04 |
| V-03 | Nessun login in v1 (LAN / reverse proxy) | D-02 + utente |
| V-04 | Stack backend vincolante: .NET 10, Minimal API, EF Core + SQLite, Hangfire, Serilog | specifica §2 + utente |
| V-05 | Frontend: React 19 + Vite + TS + TanStack Query; semplificabile nell'MVP | utente |
| V-06 | CI GitLab + buildah; registry GitLab; solo amd64 | utente |
| V-07 | DoD MVP iniziale: pipeline verde + container avviabile | utente Q27 |
| V-08 | **Filtro catalogo per Arr**: Sonarr configurato → serie; Radarr configurato → film; nessuno → catalogo vuoto. Stand-up segue `MediaType` TMDB | utente Q7; STATUS D-011 |
| V-09 | Hangfire: tentare SQLite community; se fallisce → **storage in memoria** | D-10 + utente Q13 |
| V-10 | Sonarr/Radarr: spike su **documentazione ufficiale** (nessuna istanza swagger accessibile in pianificazione) | utente Q15 |
| V-11 | Sezione "In scadenza" in MVP (dati free + nota limite) | utente Q11; D-07 |
| V-12 | Nome/namespace: `PickArr` / `PickArr.*` | utente Q5 |
| V-13 | Seguire ordine specifica §18 per le tranche | utente Q3 |

---

## 4. Definition of Done locale

Oltre ai criteri comuni, una tranche `pickarr-mvp` è chiusa solo se:

- I test automatici dello scope del task sono PASS (o dichiarato non eseguibile con task BLOCKED)
- Nessuna regressione sui test preesistenti del monorepo
- Per tranche packaging/CI: job GitLab rilevanti verdi e immagine avviabile su amd64 (quando applicabile)
