# SP-R1 — Payload TMDB e costo en-US

| | |
| --- | --- |
| Task | T01 / TR-01 |
| Data | 2026-11-06 |
| HEAD | `58acd390c989de2f68e789c03b1bc01c9d683aab` |
| Esito | **GO** per T10 senza secondo client TMDB |

---

## 1. Domanda: bastano URL e payload già richiesti?

### URL attuali (invariati)

`TmdbMetadataProvider.GetMovieAsync` / `GetTvAsync`:

```
…/{movie|tv}/{id}?language=…&append_to_response=external_ids,keywords,credits
```

Fonte: `src/Pickarr.Infrastructure/Metadata/Tmdb/TmdbMetadataProvider.cs`.

**Conclusione:** non serve cambiare l’URL né aggiungere endpoint dedicati (`/credits` separato) per ottenere keywords e credits nel medesimo round-trip già usato dall’enrichment.

### Cosa offre l’API live (documentazione TMDB)

| Dato | Dove nel JSON | Serve append? | Già nell’URL? |
| --- | --- | --- | --- |
| Keywords (movie: `keywords.keywords[]`, tv: `keywords.results[]`) | blocco append | sì (`keywords`) | sì |
| `credits.cast[].id`, `name`, `order` | blocco append | sì (`credits`) | sì |
| `credits.crew[]` (`id`, `name`, `job`, `department`) — Director | blocco append | sì (`credits`) | sì |
| `created_by[]` (`id`, `name`, …) — serie | **root** TV details | no | già nella risposta details |
| `origin_country` (string[]) | **root** movie/TV details | no | già nella risposta details |

Evidenza cast id: esempio ufficiale movie credits id 550 include `cast[].id` (es. 819 Edward Norton).  
Evidenza `created_by` / `origin_country`: esempio ufficiale TV series details (Game of Thrones / schema movie details).

### Cosa mappa oggi il codice

| Pezzo | Stato |
| --- | --- |
| Keywords → `TmdbKeyword` | Sì, in memoria; usate solo da `TmdbStandUp` in `EnrichmentService.ApplyDetails`; **non** tabelle Keywords/TitleKeywords |
| Cast | Solo `name`+`order` in `TmdbCastDto`; `CastNames` → `CastJson` (max 12 nomi). **Nessun person id** |
| Crew / Director | **Assente** da `TmdbCreditsDto` |
| `created_by` | **Assente** da `TmdbTvDto` |
| `origin_country` | **Assente** da movie/TV DTO |

Fonte DTO: `TmdbJsonModels.cs` (`TmdbCreditsDto`, `TmdbCastDto`, `TmdbTvDto`).

### Fixtures di test

`tests/fixtures/tmdb/movie-550-en.json`, `tv-1396-en.json`, `tv-standup-keywords.json`, `movie-550-it-empty-overview.json`:

- contengono **keywords** (e external_ids/genres);
- **non** contengono blocco `credits` né `created_by` / `origin_country`.

Quindi i test attuali non esercitano la deserializzazione credits: in T10 andranno arricchiti i fixture (sample minimo cast id + crew Director + created_by su TV).

### Verdetto payload

**Sì:** id persona, crew Director e `created_by` sono già disponibili **senza cambiare URL**, a patto di estendere i DTO di deserializzazione e il mapping verso `TmdbTitleDetails` / tabelle. Non serve un secondo client HTTP TMDB.

---

## 2. Costo chiamata extra `language=en-US`

### Comportamento attuale

In `EnrichmentService.EnrichCoreAsync`:

1. Per ogni `MetadataLanguage` distinta delle availability non rimosse → 1× `GetMovie`/`GetTv`.
2. Extra en-US **solo se** `LanguageFallback.NeedsEnglishFallback(title, overview)` (title **o** overview vuoti) **e** la lingua corrente non è già `en-US`.

Limiter: `TmdbRateLimitHandler` + `TmdbOptions.RequestsPerSecond` default **20** (serializzato, 1 richiesta alla volta con min interval `1/rps`).

Test esistente: `Enrich_falls_back_to_en_US_when_overview_empty` (it-IT vuoto → +1 en-US).

### Gap vs BR-REC-09 / §11.2 raccomandazioni

Per gli embedding serve una riga `TitleLocalizations` `en-US` anche quando la localizzazione della sorgente ha overview completa in altra lingua. Oggi quel caso **non** genera la chiamata en-US.

### Stima impatto sul limiter (T10 / RecoFeatureJob)

| Scenario | Chiamate TMDB aggiuntive |
| --- | --- |
| Sorgente già `en-US` | 0 (localizzazione già scritta) |
| Enrichment “nuovo” con lingua ≠ en-US e testo completo | **+1** per titolo (policy nuova: assicurare en-US) rispetto a oggi |
| Enrichment “nuovo” con overview/title vuoti | 0 delta (già +1 oggi) |
| Backfill `RecoFeatureJob` su Ready senza en-US | **+1** per titolo eleggibile, una tantum |
| Solo persist keywords/crediti da payload già in mano al re-fetch | se si ri-chiama details nella lingua primaria: 1 call/titolo; se en-US manca: fino a 2 |

Throughput teorico col default 20 rps: ~20 titoli/s se una call/titolo; ~10 titoli/s se due call (lingua + en-US). Su pool tipico 5–15k titoli il backfill una tantum è nell’ordine di **minuti**, non ore, senza alzare il rate limit. Il job deve restare throttled sulla coda `ml` e rispettare lo stesso handler.

**Nota:** keywords/cast/crew/`created_by`/`origin_country` non richiedono call dedicate oltre al details già previsto; l’unico incremento strutturale di traffico è la policy en-US più ampia + backfill.

---

## 3. Go / no-go per T10

| Criterio | Esito |
| --- | --- |
| Stesso `IMetadataProvider` / `TmdbMetadataProvider` | **GO** |
| Nessun secondo client o URL Watchmode | **GO** (BR-REC-10) |
| Estendere solo DTO + mapping + persistenza + fixture | **GO** |
| `created_by` da root TV, non da credits | Documentare in T10 |
| `origin_country` opzionale da root senza append | **GO** se si aggiunge colonna |
| CastJson resta lista nomi per UI | Compatibile (id in tabelle nuove) |

**Decisione spike: GO** — procedere con T10 come da PLAN (dopo T01 e dipendenze R1 build T05).

### Checklist implementativa per T10 (non eseguita qui)

1. Estendere `TmdbCastDto` con `id`; `TmdbCreditsDto` con `crew`; `TmdbCrewDto` (`id`,`name`,`job`,`department`).
2. Aggiungere `created_by` a `TmdbTvDto`; opz. `origin_country` a movie/TV.
3. Estendere `TmdbTitleDetails` (Core) con crediti strutturati; mantenere `Cast` nomi per scheda.
4. Persistere Keywords/TitleKeywords/People/TitleCredits in `EnrichCoreAsync`.
5. Policy en-US: se manca localizzazione `en-US`, fetch anche con overview non vuota.
6. Aggiornare fixtures + test enrichment/feature job.
7. `RecoFeatureJob` backfill Ready senza pezzi.

---

## 4. Rivalidazione

Rivalidare questo spike se cambiano: path `append_to_response`, shape JSON TMDB, `TmdbOptions.RequestsPerSecond`, o `LanguageFallback` / `ResolveLanguages`.
