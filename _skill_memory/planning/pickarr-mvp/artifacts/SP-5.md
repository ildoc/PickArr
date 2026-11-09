# SP-5 — SQLite performance smoke (T25)

> Data: 2026-11-05  
> Ambiente: Windows locale, SQLite file + WAL, `TitleQueryService` in-process (no HTTP)

## Setup

| Parametro | Valore |
| --- | --- |
| Titoli | 10 000 (synth) |
| Localizzazioni | 1× en-US per titolo |
| Availabilities | 1× source US |
| ArrLibraryItems | ~20% titoli (join badge) |
| Iterazioni misura | 25 (+ 5 warmup) |
| Page size | 48 |

Comando:

```bash
dotnet test tests/Pickarr.Infrastructure.Tests --filter FullyQualifiedName~Sp5PerformanceSmokeTests
```

## Risultati (NFR-01 target p95 &lt; 300 ms)

| Query | Median | p95 | Esito |
| --- | --- | --- | --- |
| Lista `sort=added` | 43.4 ms | **49.6 ms** | PASS |
| Ricerca `q=title 4242` | 44.7 ms | **50.2 ms** | PASS |

Margine ampio rispetto a 300 ms → **FTS5 non necessario** per MVP.

## WAL / volume Docker

- Runtime: `DatabaseInitializer` esegue `PRAGMA journal_mode=WAL` e `busy_timeout=5000` dopo migrate.
- Hangfire storage già in WAL (SP-4).
- Su volumi di rete (SMB/NFS) WAL può essere fragile: preferire volume locale bind (`./config:/config`) o named volume Docker sul nodo. Evitare share SMB per `/config`.
- Immagine gira come utente `app` non-root; directory `/config` chown in Dockerfile.

## Conclusioni

- KPI STATUS §6 p95 aggiornabile a ~50 ms su 10k titoli (misura locale).
- Nessun finding performance aperto.
- Opzione FTS5 resta documentata in specifica § ricerca come fallback se cataloghi multi-servizio futuri superano ~50–100k.
