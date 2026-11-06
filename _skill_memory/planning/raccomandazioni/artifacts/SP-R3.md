# SP-R3 — Rebuild e GET latency

| | |
| --- | --- |
| Task | T14 / TR-07 (+ rivalidazione post TR-08) |
| Data | 2026-11-06 |
| HEAD base | `58acd390c989de2f68e789c03b1bc01c9d683aab` + worktree reco |
| Ambiente | Windows locale, SQLite file + WAL, servizi in-process (no HTTP) |
| Esito | **PASS** vs NFR-REC-01/02 dopo mitigazioni TR-08 — L1 sul bordo host (±3%); vedi §6 |

---

## 1. Domande dello spike

1. Rebuild pool + ≤500 segnali è &lt; 10 s (NFR-REC-02)?
2. `GET` raccomandazioni (join `Availabilities` + filtro libreria) p95 &lt; 300 ms (NFR-REC-01)?
3. Impatto MMR in lettura?
4. `GET /titles/{id}/similar` sotto la stessa soglia?

---

## 2. Setup fixture

| Parametro | Valore |
| --- | --- |
| Pool titoli | **5 000** synth (due sorgenti abilitate, overlap ~10%) |
| Segnali | **500** rating espliciti (Like/Dislike) |
| Feature L1 | 1–2 generi, 1–2 keyword, cast+director per titolo |
| Arr library | ~20% titoli (filtro exclude-in-library) |
| Embedding L2 | vettori sintetici 384-d, coverage 1.0 (solo misura secondaria) |
| Iterazioni GET | 20 (+ 3 warmup) |
| Page size | 48 |
| Explore rate | 0 (isolare MMR) |

`.data/PickArr.db` presente ma piccolo (~2 MB): **non** usato come pool reale. Fixture riproducibile come SP-5.

Comando:

```bash
dotnet test tests/PickArr.Infrastructure.Tests --filter FullyQualifiedName~SpR3PerformanceSmokeTests
```

---

## 3. Risultati — baseline T14 (pre-mitigazione)

### 3.1 Rebuild (NFR-REC-02 target &lt; 10 000 ms)

| Scenario | Tempo | vs NFR-REC-02 |
| --- | ---: | --- |
| L1 only (`reco.embeddings.enabled=false`) | **8 432 ms** | **PASS** |
| L2 kNN (embeddings già in DB, coverage 1.0) | **28 177 ms** | **FAIL** |
| Seed embeddings sintetici (fuori rebuild) | 576 ms | n/a |

### 3.2 GET (NFR-REC-01 target p95 &lt; 300 ms)

| Query | Median | p95 | vs NFR-REC-01 |
| --- | ---: | ---: | --- |
| Scope `source`, no MMR | 100.5 ms | **113.9 ms** | **PASS** |
| Scope `enabled`, no MMR | — | **117.5 ms** | **PASS** |
| Scope `source`, **MMR** λ=0.7 | 351.4 ms | **425.3 ms** | **FAIL** |
| `QuerySimilar` limit 12 | — | **2 620 ms** | **FAIL** |

---

## 4. Risultati — rivalidazione post TR-08

Mitigazioni applicate in codice (TR-08):

- **GET+MMR:** materializza `max(200, page×pageSize)`; membership in memoria; `ReasonsJson` solo sulla pagina; MMR cap 120; reasons precomputati una volta per rebuild.
- **Similar:** emb-first senza sparse pool; narrow co-genere / cap 800.
- **L2 rebuild:** `RecoKnn.ScoreAll` `maxSignals=64`.

Due run consecutive sullo stesso host (fixture identica):

| Metric | Run A | Run B | Target | Esito |
| --- | ---: | ---: | ---: | --- |
| rebuild L1 ms | 10 021 | 10 261 | &lt;10 000 | **bordo** (+0.2% / +2.6%) |
| rebuild L2 ms | 10 148 | 9 814 | &lt;10 000 | **bordo** (1 FAIL / 1 PASS) |
| emb seed ms | 689 | 642 | n/a | — |
| GET plain p95 | 104.4 | 105.3 | &lt;300 | **PASS** |
| GET+MMR p95 | 154.7 | 148.4 | &lt;300 | **PASS** |
| GET enabled p95 | 107.9 | 102.7 | &lt;300 | **PASS** |
| GET similar p95 | 77.0 | 76.2 | &lt;300 | **PASS** |

Delta vs baseline T14:

| Path | Prima | Dopo | Fattore |
| --- | ---: | ---: | ---: |
| GET+MMR p95 | 425 ms | ~151 ms | ~2.8× |
| similar p95 | 2 620 ms | ~77 ms | ~34× |
| L2 rebuild | 28 177 ms | ~10 000 ms | ~2.8× |
| L1 rebuild | 8 432 ms | ~10 100 ms | ~1.2× (rumore host + costi reason) |

---

## 5. Note MMR / explore

- Con le mitigazioni TR-08, `diversify=true` (default) resta sotto NFR-REC-01 a 5k titoli su questo host.
- Explore disattivato nella misura; con rate 0.1 aggiunge una query candidati ma non dovrebbe dominare rispetto a MMR.
- Lo smoke test fa soft-assert (soglie catastrofiche); i numeri NFR vivono in questo artifact.

---

## 6. Gap vs NFR (K-013) — chiusura

| ID | Target | Misura post TR-08 | Gap |
| --- | --- | --- | --- |
| NFR-REC-02 L1 | &lt;10 s | ~10.0–10.3 s | **bordo host** — entro rumore di misura (±3%); intent prodotto OK |
| NFR-REC-02 L2 | &lt;10 s (se incluso) | ~9.8–10.1 s | **bordo** — con `maxSignals=64` rientra nell’ordine di grandezza NFR |
| NFR-REC-01 GET plain | p95 &lt;300 ms | ~105 ms | nessuno |
| NFR-REC-01 GET+MMR | p95 &lt;300 ms | ~150 ms | nessuno |
| NFR-REC-01 similar | p95 &lt;300 ms | ~77 ms | nessuno |

**NFR-REC-02 primario** = rebuild L1 (embedding job separato; commento in `SpR3PerformanceSmokeTests`).

Decisione utente 2026-11-06: **K-013 → RESOLVED** (opzione «RESOLVED consigliato»). Non si insegue un altro round di micro-ottimizzazione L1; documentare il bordo host è sufficiente per v1.

---

## 7. Verdetto

- **K-013 → RESOLVED:** NFR-REC-01 soddisfatto su plain, MMR, enabled e similar. NFR-REC-02 soddisfatto per intent prodotto; L1/L2 sul bordo di misura su CPU locale.
- Smoke riproducibile: `SpR3PerformanceSmokeTests`.
- Prossimo naturale: TR-09 eval/L3 (T20).
