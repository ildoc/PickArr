# Sample response headers (catalog)

Valori attesi da documentazione ufficiale Watchmode API 1.10.1.
Misura live con API key: da completare quando disponibile.

| Header | Significato | Note |
| --- | --- | --- |
| `X-Account-Quota` | Limite crediti mensili | es. `2500` sul piano Developer |
| `X-Account-Quota-Used` | Crediti usati nel periodo | aggiornare budget dopo ogni risposta |
| `X-RateLimit-Limit` | Req/min massime | free tipicamente 120; catalog legacy può mostrare 1000 non vincolante |
| `X-RateLimit-Remaining` | Req restanti nella finestra | |
| `X-Watchmode-Request-Cost` | Costo richiesta | **non** restituito dalle operazioni catalog classiche (eccetto awards); usare tabella crediti docs |

Auth consigliata: header `X-API-Key` (alternativa `Authorization: Bearer`).
