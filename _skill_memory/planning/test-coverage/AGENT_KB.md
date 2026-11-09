# AGENT KB — test-coverage (ADDENDUM)

Addendum dominio `test-coverage`.
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

1. Priorità a test che proteggono refactor: contratti API (status/code body), filtri catalogo/lista, Arr, setup cataloghi, empty states UI.
2. Preferire assert su comportamento (argomenti request, route, eccezioni/codici) rispetto a “componente montato”.
3. Happy path + edge + eccezioni nello stesso task quando lo scope resta ≤ L; altrimenti split.
4. Non introdurre dipendenze di produzione solo per alzare coverage; non testare DTO/interfacce vuote.

---

## 3. Vincoli tecnici non negoziabili

| ID | Vincolo | Fonte |
| --- | --- | --- |
| TC-01 | Documenti KB in italiano | C-01 + skill |
| TC-02 | Nessun commit senza richiesta esplicita | C-02 |
| TC-03 | Scope stretto al task; finding fuori scope → STATUS §3 | C-03 + skill |
| TC-04 | Core resta libero da tipi Infrastructure; test Core non referenziano Infrastructure | `AGENTS.md` |
| TC-05 | Integration: factory con `Hangfire:Storage=Memory`, `PickArr:Catalog:AutoInitialImport=false`; non puntare al DB `.data` | `AGENTS.md` |
| TC-06 | Test che assumono settings Arr assenti o DB vuoto: collection/factory isolate, non `"api"` condivisa | `AGENTS.md` + ANALYSIS A-002 |
| TC-07 | Non stampare/committare secret Watchmode/TMDB/Sonarr/Radarr | `AGENTS.md` |
| TC-08 | UI copy solo via i18n `en.json`; test usano stringhe EN già presenti | `AGENTS.md` |
| TC-09 | Prima di `dotnet test`/`build` su progetti che lockano `PickArr.Api/bin`, fermare l’API in esecuzione | `AGENTS.md` |
| TC-10 | Catalogo ≠ My list: non reintrodurre `view=favorites`/`favorite=true` sul catalogo | `AGENTS.md` product boundaries |
| TC-11 | Criteri di test: assert comportamento (route, request args, empty state, status/code), non solo render | `AGENTS.md` Tests |

---

## 4. Definition of Done locale

Oltre ai criteri comuni, una tranche `test-coverage` è chiusa solo se:

- Ogni acceptance del task ha evidenza (comando + esito sintetico o passi manuali)
- Nuovi/aggiornati test falliscono se si rompe il comportamento protetto (almeno un caso negativo o edge dove rilevante)
- Nessun test nuovo dipende dallo stato lasciato da un altro test della collection `"api"` senza seed idempotente

---

## 5. Tooling dominio-specifico

- Backend: `dotnet test` (filtro per progetto/FQN come in `AGENTS.md`)
- Frontend: `cd src/PickArr.WebApp && npm test -- --run` (+ `typecheck`/`lint` se si tocca FE)
- Coverage: coverlet.collector + `npm run test:coverage` (baseline T01)
- Comandi e path canonici: `AGENTS.md` (non `backend/`/`frontend/` del file comune CONV-04)
- FE RTL: testo spezzato su più nodi → matcher su pezzi (`getByText('Earlier')`) o function matcher; non regex sull’intero `1. Earlier`
- CatalogPage: stub `localStorage` per preferred source; mock `CatalogForYouRow`/`TitleDialog` per isolare empty/filtri
- Reco/ratings `enabled: settings.data?.reco.enabled !== false` → con settings ancora undefined le query partono; per assert “non chiama API” seedare `queryClient.setQueryData(['settings'], …)` prima del render

