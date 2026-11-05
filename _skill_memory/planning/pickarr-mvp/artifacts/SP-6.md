# SP-6 — Frontend smoke (griglia, CSP, route dettaglio)

> Data: 2026-11-05  
> Task: T17 / TR-05  
> Scope: fluidità griglia base, CSP immagini TMDB, route modale con filtri in URL

## Setup verificato

- Build: `npm run build` (Vite → `frontend/dist`)
- Unit: `npm test` (Vitest: ArrStatusBadge, FavoriteButton, format)
- Backend headers: `SecurityHeadersMiddleware` con  
  `img-src 'self' https://image.tmdb.org data:` (+ `X-Content-Type-Options`, `Referrer-Policy: no-referrer`)
- UI MVP: `/catalog` + `?title=` dialog, filtri in search params, empty-state Arr (`emptyReason=arrNotConfigured`)

## Checklist smoke

| Check | Esito | Note |
| --- | --- | --- |
| Tema scuro desktop, min-width warning &lt; 1280 | OK (codice) | avviso non bloccante |
| Selettore sorgente + ricerca debounce 300 ms | OK (codice) | URL = source of truth |
| Quick filters All/New/Leaving/Removed/My list | OK (codice) | `view` + `favorite` |
| Griglia `auto-fill` 180px, lazy posters TMDB | OK (codice) | infinite scroll via IntersectionObserver |
| Fluidità senza virtualizzazione | **OK per MVP** | migliaia di card via paginazione 48; DOM tiene solo pagine caricate |
| Dettaglio modale `?title=` preserva filtri | OK (codice) | chiusura ripristina params senza `title` |
| Preferiti toggle ottimistico via invalidation | OK (test) | FavoriteButton Vitest |
| Attribuzioni Watchmode link + TMDB disclaimer | OK | logo TMDB come badge testo (compatibile CSP) |
| CSP non blocca `image.tmdb.org` | OK (header) | middleware API |
| Virtualizzazione `@tanstack/react-virtual` | **Deferred** | non necessaria con pageSize 48 + infinite load |

## Finding

Nessun finding bloccante. Virtualizzazione **non** introdotta: se SP-5/NFR-01 su 10k titoli mostra jank dopo molte pagine caricate, rivalutare in T25.

## Decisione

- Griglia base accettata per chiusura TR-05 / T17.
- Arr add/delete UI rimane stub “coming soon” fino a TR-07.
