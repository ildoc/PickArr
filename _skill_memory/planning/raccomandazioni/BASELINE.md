# raccomandazioni BASELINE

> Data analisi: 2026-11-06
> Branch analizzato: `main` @ `58acd390c989de2f68e789c03b1bc01c9d683aab`
> Analista: agente incremental-dev-planning

⚠️ DOCUMENTO IMMUTABILE — Non aggiornare dopo la creazione.

---

## 1. Componenti coinvolti

| Componente | Responsabilità | File | LOC circa |
| --- | --- | --- | --- |
| Title / Availability / Favorite | Catalogo e segnali futuri | `src/PickArr.Core/Domain/Entities/*` | — |
| TitleQueryService | Griglia catalogo | `TitleQueryService.cs` | — |
| EnrichmentService + TMDB | Metadati | `EnrichmentService.cs`, `TmdbMetadataProvider.cs` | — |
| HangfireBootstrap | Job queues default/sync/arr | `HangfireBootstrap.cs` | — |
| FavoriteButton / CatalogPage / TitleDialog | UI catalogo | `src/PickArr.WebApp/src/features/**` | — |
| Ratings / Reco / ONNX | Feature target | assenti | 0 |

## 2. Flussi as-is

### FLUSSO-01 — Catalogo

- **Trigger**: GET catalog titles
- **Sequenza**: filtri + RemovedAt==null default → TitleListItemDto
- **Output**: pagina titoli per sorgente
- **Problemi noti**: predicato non adatto alle reco

### FLUSSO-02 — Preferiti

- **Trigger**: PUT/DELETE favorites
- **Sequenza**: FavoriteService → invalidate query onSuccess
- **Output**: cuore aggiornato
- **Problemi noti**: non ottimistico

### FLUSSO-03 — Enrichment

- **Trigger**: EnrichmentJob
- **Sequenza**: TMDB details → generi/voti/CastJson nomi; keywords solo stand-up
- **Output**: Title Ready
- **Problemi noti**: no TitleKeywords/People

## 3. Metriche baseline

| Metrica | Valore baseline | Comando/metodo misura |
| --- | --- | --- |
| File `.cs` in `src`+`tests` | 230 | find |
| File `.ts`/`.tsx` in WebApp `src` | 51 | find |
| Match `[Fact]`/`[Theory]` in tests (grep, output truncato a 100+) | ≥100 | Grep tool su `tests/**/*.cs` |
| Endpoint rating/recommendations | 0 | assenza Endpoints |
| Chiavi `reco.*` in SettingsKeys | 0 | SettingsKeys.cs |
| Code Hangfire | default, sync, arr | HangfireBootstrap |
| Locale i18n | solo `en.json` | glob locales |
| SSE `/events` | assente | Program.cs |

## 4. Note tecniche

- Worktree al momento dell’assessment: molti path `M` con diff tipicamente EOL-only su `src`.
- `PickArr:DataPath` sviluppo: `.data` (launchSettings); non usare per test di integrazione.
- Spec feature: `docs/raccomandazioni.md` v0.2; path UI nel doc §15 (`frontend/`) non allineato al repo.
