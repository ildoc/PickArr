# SP-2 — TMDB (T13)

> Data: 2026-11-05  
> Fonti: docs ufficiali TMDB v3 (`append_to_response`, movie/tv details, find, configuration, genres); specifica PickArr §5.2 / §6.6  
> Live call con API token: **non eseguita** in questa sessione (token assente) — keyword stand-up id da conferma opzionale

## Auth e base

| Voce | Valore |
| --- | --- |
| Base URL | `https://api.themoviedb.org/3` |
| Auth | `Authorization: Bearer <read access token>` or `api_key` (settings `tmdb.apiKey`) |
| Rate limit consigliato | 20 req/s outbound (configurabile) |

## Endpoint usati (MVP)

| Uso | Path | Note |
| --- | --- | --- |
| Film | `GET /movie/{id}?language={lang}&append_to_response=external_ids,keywords` | `imdb_id` sul dettaglio; keywords in `keywords.keywords[]` |
| Serie | `GET /tv/{id}?language={lang}&append_to_response=external_ids,keywords` | `external_ids.tvdb_id`, `external_ids.imdb_id`; keywords in `keywords.results[]` |
| Find IMDb | `GET /find/{imdb_id}?external_source=imdb_id` | se manca `tmdb_id` |
| Generi | `GET /genre/movie/list`, `/genre/tv/list` | cache in tabella `Genres` |
| Config immagini | `GET /configuration` | `images.secure_base_url`; cache 7 giorni |

## Lingue (BR-15)

- Parametro `language`: BCP-47 come da sorgente (`it-IT`, `en-US`); TMDB accetta anche `it`, `en` — PickArr invia il tag completo della sorgente.
- Se `title`/`name` **oppure** `overview` risultano vuoti/null nella lingua richiesta → seconda chiamata con `en-US`.
- Si memorizzano **entrambe** le localizzazioni quando il fallback è usato (non si sovrascrive la riga primaria con l’inglese).

## TVDB / IMDb

| Campo | Movie | TV |
| --- | --- | --- |
| `imdb_id` | root `imdb_id` (e/o `external_ids`) | `external_ids.imdb_id` |
| `tvdb_id` | di solito assente | `external_ids.tvdb_id` (int) |

Copertura: non tutti i titoli TV hanno `tvdb_id`; enrichment non fallisce se manca — lascia `TvdbId` null (match Sonarr degradato, fuori scope T14).

## Stand-up — regola proposta (D-008)

1. Durante enrichment, ispezionare keywords appendate.
2. Se esiste keyword con **id `9715`** *oppure* nome normalizzato `stand-up comedy` / `standup comedy` → `Title.Category = StandUp`.
3. Altrimenti `Category` resta allineata a `MediaType` (`Movie` / `Series`).
4. `MediaType` **non** cambia: V-08 / filtro Arr restano su Movie vs Series; lo stand-up è classificazione UI/categoria (D-011).

> Id `9715` è il valore tipico pubblico per “stand-up comedy” su TMDB; **confermare** con `GET /search/keyword?query=stand-up%20comedy` quando c’è un token. Fixture e codice usano costante `TmdbStandUp.KeywordId = 9715` + match per nome.

## Config immagini (URL builder)

- `secure_base_url` + size + `poster_path` / `backdrop_path`
- Size suggerite: griglia `w342`, scheda `w500`, backdrop `w780`
- Attribuzione UI: logo + “This product uses the TMDB API but is not endorsed or certified by TMDB.” (FR-MET-07) — wiring UI in TR-05

## Fixture

Directory: `backend/tests/fixtures/tmdb/`

- `configuration.json`
- `movie-550-en.json` (dettaglio + external_ids + keywords)
- `movie-550-it-empty-overview.json` (simula overview vuota → trigger fallback)
- `tv-1396-en.json` (serie con `tvdb_id`)
- `tv-standup-keywords.json` (keywords include stand-up)
- `find-imdb.json`
- `genre-movie-list-en.json`

## Impatto su K / D

| ID | Esito |
| --- | --- |
| D-008 | **FACT proposta**: regola Category=StandUp via keyword 9715 / nome |
| BR-15 | **FACT**: doppia localizzazione su fallback en-US |
| R-03 | Contratto client TMDB chiuso da docs + fixture; gap = sole misure live opzionali |
