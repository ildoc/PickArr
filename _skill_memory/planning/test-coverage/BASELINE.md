# test-coverage BASELINE

> Data analisi: 2026-11-09
> Branch analizzato: `main` @ `c576634b3809b2c1f598ce6c1ae114b37f17f49c`
> Analista: agente incremental-dev-planning

⚠️ DOCUMENTO IMMUTABILE — Non aggiornare dopo la creazione.

---

## 1. Componenti coinvolti

| Area | Responsabilità | Path | Note |
| --- | --- | --- | --- |
| Core.Tests | Logica pura (budget, reco math, settings, Arr matcher) | `tests/Pickarr.Core.Tests` | 18 file test |
| Infrastructure.Tests | Service EF + WireMock sync/Arr/reco | `tests/Pickarr.Infrastructure.Tests` | 29 file test |
| Integration.Tests | API WebApplicationFactory | `tests/Pickarr.Integration.Tests` | 12 file test (+ factory/collection) |
| WebApp Vitest | UI/component/hook | `src/Pickarr.WebApp/src/**/*.test.*` | 21 file test |
| Produzione FE | App React | `src/Pickarr.WebApp/src` | ~52 moduli prod ts/tsx |

---

## 2. Flussi as-is

Vedi ANALYSIS.md §3 (FLUSSO-01…09). Snapshot: copertura forte su sync/reco/ratings; debole su catalog setup API, seasons HTTP, Arr HTTP happy path, CatalogPage/AddToArr/client FE.

---

## 3. Metriche baseline

| Metrica | Valore baseline | Comando/metodo misura |
| --- | --- | --- |
| Fact/Theory Core | 64 | conteggio `[Fact]`/`[Theory]` in `tests/Pickarr.Core.Tests/*.cs` |
| Fact/Theory Infrastructure | 106 | idem Infra |
| Fact Integration (stimati) | ~29 | `[Fact]` in Integration.Tests (esclusi helper) |
| Casi Vitest (`it`/`test`) | 39 | grep su `*.test.ts(x)` |
| File test backend `*Tests.cs` | 59 | glob `tests/**/*Tests.cs` |
| File test frontend | 21 | glob `**/*.test.ts(x)` |
| Moduli FE prod senza test sibling | ~31 | confronto prod vs test 2026-11-09 |
| Endpoint API mappati | ~40+ | `MapGet/Post/Put/Delete` in Endpoints/ |
| Coverage % line backend | non misurata | coverlet non eseguito in assessment |
| Coverage % FE | non misurata | vitest coverage non configurato |
| Tooling coverage presente | coverlet.collector 6.x in packages; Vitest no coverage | `Directory.Packages.props`, `vite.config.ts` |

---

## 4. Note tecniche

- `ApiFactory` usa DataPath temp unico per factory; collection `"api"` **condivide una factory** → DB e settings persistono tra classi della collection.
- Test che assumono Arr non configurato o griglia vuota devono usare collection dedicata (già fatto per favorites/ratings/reco; non per ArrAction delete-settings).
- FE: pattern consolidato QueryClient `retry: false`, mock moduli `api/*`, stub `localStorage` per preferenza source.
- CONV-04 nel AGENT_KB comune cita `backend/`/`frontend/`; path reali sono `src/` e `tests/` (`AGENTS.md`).
