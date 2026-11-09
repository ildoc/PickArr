# T01 — Coverage baseline

Data: 2026-11-09  
HEAD: `c576634b3809b2c1f598ce6c1ae114b37f17f49c`  
Worktree: tooling coverage (runsettings, vitest coverage, package-lock)

## Comandi (riproducibili)

### Backend (coverlet.collector)

```bash
dotnet test PickArr.slnx --settings tests/coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/coverage-baseline
```

Output: `TestResults/coverage-baseline/**/coverage.cobertura.xml` (gitignored).  
Settings: `tests/coverlet.runsettings` (formato cobertura; esclude assembly `*.Tests`).

### Frontend (vitest + v8)

```bash
cd src/PickArr.WebApp
npm run test:coverage
```

Output: `src/PickArr.WebApp/coverage/` (gitignored); summary JSON copiato in `T01-vitest-coverage-summary.json`.

## Esito run baseline

| Suite | Passed | Failed | Note |
| --- | --- | --- | --- |
| PickArr.Core.Tests | 77 | 0 | Theory espandono i casi oltre i 64 Fact/Theory |
| PickArr.Infrastructure.Tests | 110 | 0 | |
| PickArr.Integration.Tests | 29 | 0 | |
| Vitest | (suite completa) | 0 | log `T01-vitest-coverage.log` |

## Line coverage backend (per collector run)

| Collector (test project) | Package | Line % | Branch % | Lines covered/valid |
| --- | --- | --- | --- | --- |
| Core.Tests | PickArr.Core | 48.9% | 50.9% | 808/1653 |
| Infrastructure.Tests | PickArr.Core | 69.0% | 60.7% | (in run aggregato) |
| Infrastructure.Tests | PickArr.Infrastructure | 92.3% | 58.3% | (run: 14062/15657 totale) |
| Integration.Tests | PickArr.Api | 93.8% | 34.0% | |
| Integration.Tests | PickArr.Infrastructure | 88.6% | 37.5% | |
| Integration.Tests | PickArr.Core | 57.6% | 44.3% | |
| Integration.Tests (totale run) | — | 85.8% | 39.7% | 14225/16572 |

I tre XML non sono fusi in un unico % ufficiale: usare i comandi sopra e confrontare per package.

## Line coverage frontend (vitest v8)

| Metrica | Valore |
| --- | --- |
| Statements / Lines | 65.07% (2774/4263) |
| Branches | 65.53% (443/676) |
| Functions | 40.75% (108/265) |

Hotspot a bassa copertura (indicativi, da report text):

- `api/client.ts`, `api/catalog.ts`, `api/arr.ts` (~0–14%)
- `features/ratings/MyRatingsPage.tsx` (~5%)
- `features/reco/SimilarRow.tsx` (~10%)
- `features/title/SeriesSeasons.tsx`, `SeasonTable.tsx` (~7–9%)
- `features/arr/AddToArrDialog.tsx` (~20%)
- `features/settings/SettingsLayout.tsx` (~13%)

Nota: `CatalogPage.tsx` risulta ~76% per effetti collaterali di `router.test` / MyList — non implica copertura comportamentale dedicata (vedi T11).

## File tooling aggiunti/modificati

- `tests/coverlet.runsettings` (nuovo)
- `src/PickArr.WebApp/vite.config.ts` — sezione `test.coverage`
- `src/PickArr.WebApp/package.json` — script `test:coverage`, devDep `@vitest/coverage-v8`
- lockfile aggiornato da `npm install`
