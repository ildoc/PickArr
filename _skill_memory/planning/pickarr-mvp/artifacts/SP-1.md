# SP-1 — Watchmode (T06)

> Data: 2026-11-04  
> Fonti: OpenAPI `https://api.watchmode.com/openapi.json` (API **1.10.1**), `llms-full.txt`, pricing Developer  
> Live call con API key: **non eseguita** in questa sessione (chiave assente in env) — stime Netflix IT/US e header reali da conferma opzionale

## Endpoint e auth

| Uso | Path | Crediti (docs) |
| --- | --- | --- |
| Quota | `GET /v1/status/` | **0** |
| Sorgenti | `GET /v1/sources/` | 1 |
| Regioni | `GET /v1/regions/` | 1 |
| Lista titoli | `GET /v1/list-titles/` | **1 per regione richiesta** per pagina; **1** se `regions` omesso |
| Leaving | `GET /v1/title-leaving-dates/` | 1 per pagina (free: preview 10) |
| Releases semplice | `GET /v1/releases/` | 1/pagina — pricing free: release tracking **non** incluso; trattare come **fuori MVP free** (preferire leaving preview) |
| Change feed | `GET /v1/changes/*` | 1/pagina — **solo paid** (badge OpenAPI) |

Auth: header `X-API-Key` (preferito) o `Authorization: Bearer`. Query `apiKey` legacy — non usare.

## `list-titles` — parametri confermati

| Param | Note |
| --- | --- |
| `source_ids` | es. `203` (Netflix) |
| `regions` | ISO alpha-2; **una regione per chiamata** nel sync PickArr → 1 credito/pagina |
| `types` | `movie,tv_series,tv_miniseries,tv_special` (+ `tv_movie`,`short_film` se utili) |
| `sort_by` | default `relevance_desc`; valori: `relevance_*`, `popularity_*`, `release_date_*`, **`title_asc` / `title_desc`** |
| `page` | ≥ 1 |
| `limit` | **1–250**, default **250** |

Risposta: `{ titles[], page, total_results, total_pages }` (+ campi awards sample se applicabile).  
Item: `id`, `title`, `year`, `imdb_id`, `tmdb_id`, `tmdb_type` (`movie`|`tv`), `type`, `popularity_percentile`.

## Ordinamento stabile (K-008)

**Confermato:** `title_asc` / `title_desc` esistono. Usare `title_asc` (o `title_desc` coerente) per la sync completa.  
La doppia conferma rimozioni (BR-05) resta necessaria: ranking `popularity_*` / `relevance_*` non sono stabili tra run.

## Leaving dates (free)

- Path: `/v1/title-leaving-dates/`
- Free: prossime **10** partenze da oggi nei paesi del piano; `regions` ok; `limit` ≤ 10
- `start_date` / `end_date` / `page>1` / `limit>10` → **403 `LEAVING_PREVIEW_LIMIT`**
- `total_results` può contare oltre la preview; `total_pages` ≤ 1 in free
- Fixture: `backend/tests/fixtures/watchmode/title-leaving-dates-preview.json`, `error-leaving-preview-limit.json`

## Quota esaurita

Codice tipico Live/catalog: **`MONTHLY_QUOTA_EXCEEDED`** (HTTP 429). Fixture: `error-quota-exceeded.json`.  
Dopo ogni risposta catalog aggiornare used da `X-Account-Quota-Used` (costo per-request header spesso **assente** sulle chiamate catalog classiche).

## Stima Netflix IT/US (pagine)

Senza live call: ordine di grandezza da cataloghi Netflix pubblici + esempio OpenAPI (`total_results` ~4–6k su query filtrate).

| Assunzione | Valore |
| --- | --- |
| Titoli per paese (movie+tv+special+miniseries) | **~5 000–8 000** |
| `limit=250` | **~20–32 pagine** / paese |
| Full sync IT+US | **~40–64 crediti** (1 regione/chiamata) |
| Incr. giornaliera (es. 3 pagine × 2 paesi) | **~6 crediti/giorno** |
| Mensile orientativo (1 full/sett. + incr. giorni) | **≪ 2500** con ampio margine |

**Confermare** con una sola pagina `list-titles?source_ids=203&regions=US&types=…&limit=250&sort_by=title_asc` e leggere `total_pages` (1 credito).

## Impatto su K-007 / K-008 / F-001

| ID | Esito |
| --- | --- |
| K-007 | **FACT aggiornato**: 1 credito/pagina **per regione**; limit max 250 |
| K-008 | **FACT**: `title_asc`/`title_desc` disponibili |
| F-001 (Watchmode) | Contratto list-titles/leaving/status **chiuso da OpenAPI**; gap residuo = sole misure live Netflix pages/header |

## Fixture

Directory: `backend/tests/fixtures/watchmode/`

- `status.json`, `sources-netflix.json`, `regions.json`
- `list-titles-page1.json` (sintetica anonimizzata)
- `title-leaving-dates-preview.json`, `error-leaving-preview-limit.json`, `error-quota-exceeded.json`
- `headers-sample.md`

## Raccomandazioni per T08+

1. Chiamare `list-titles` con **una** `regions` alla volta (allineato a `CatalogSources`).
2. Full sync: `sort_by=title_asc`, `limit=250`.
3. Leaving job free: solo preview; UI con nota limite.
4. Non dipendere da `/releases` o change feed sul piano free.
5. Budget: usare headers quota; non aspettarsi `X-Watchmode-Request-Cost` sul catalog classico.
