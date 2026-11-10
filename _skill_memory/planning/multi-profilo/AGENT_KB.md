# AGENT KB — multi-profilo (ADDENDUM)

Addendum dominio `multi-profilo`.
Personalizzazioni comuni in: `_skill_memory/planning/AGENT_KB.md`.
Workflow canonico nella skill `incremental-dev-planning`.

## 1. File locali — ordine di lettura

1. Questo file
2. `STATUS.md` §0
3. `STATUS.md` §1, sole righe per riconciliare e scegliere il task
4. `PLAN.md`, solo la tranche corrente

## 2. Focus operativo

1. Partizionare il gusto per profilo. Catalogo, metadata, embedding, quote, job e libreria Arr restano dell'installazione.
2. Non introdurre autenticazione né collaborative filtering. Il prior di casa è D-006.
3. Da T09 il build legge `useImplicit` e `excludeInLibrary` dal profilo. Le chiavi globali possono restare memorizzate.
4. Il worktree era sporco il 2026-11-09 (F-001). Non revertare diff preesistenti. T10 ha solo aggiunto hunk documentali.

## 3. Vincoli tecnici non negoziabili

- V-01. Core senza tipi Infrastructure, HTTP o file I/O. Fonte: AGENTS.md.
- V-02. Migrazioni EF generate, snapshot allineato, output `src/Pickarr.Infrastructure/Persistence/Migrations`. Non editare lo snapshot a mano. Fonte: AGENTS.md.
- V-03. Nessuna autenticazione. Il profilo è un id dichiarato. Fonte: richiesta 2026-11-09, D-004.
- V-04. Massimo 10 profili. Un solo admin, non eliminabile, ruolo non cedibile. Fonte: D-002.
- V-05. Settings negati solo nell'UI. Le API settings non si chiudono. Fonte: D-004.
- V-06. Non revertare il worktree preesistente elencato in STATUS.md §0. Fonte: F-001.
- V-07. Fermare `Pickarr.Api` prima di `dotnet build`, `dotnet test` su un progetto che riferisce l'API, o `dotnet ef`. Fonte: AGENTS.md.
- V-08. Non stampare segreti Watchmode, TMDB, Sonarr, Radarr. Fonte: AGENTS.md.
- V-09. Copy UI in `src/Pickarr.WebApp/src/i18n/locales/en.json`. Fonte: AGENTS.md.
- V-10. Test Infrastructure con `EnsureCreated` non eseguono l'SQL di backfill. Il backfill va provato con una migrazione applicata. La collezione Integration `api` condivide factory e database. Fonte: AGENTS.md, K-003.
- V-11. I percorsi di CONV-04 (`backend/`, `frontend/`) non sono quelli del repo. Comandi in STATUS.md §8. Fonte: conflitto con AGENTS.md, registrato 2026-11-09.
- V-12. Nessun commit se l'utente non lo chiede. Fonte: C-02.

## 4. Definition of Done locale

Oltre ai criteri comuni, una tranche UI (T08, T09) è chiusa solo se il flusso è stato esercitato nel browser, non solo renderizzato. Una tranche di migrazione (T01) è chiusa solo se il backfill è stato eseguito su uno schema vecchio, non solo su `EnsureCreated`.

## 5. Tooling dominio-specifico

- Tooling: STATUS.md §8.
