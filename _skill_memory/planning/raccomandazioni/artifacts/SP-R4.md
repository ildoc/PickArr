# SP-R4 — Taratura parametri (a, b, N0, γ, K, m)

| | |
| --- | --- |
| Task | T22 / TR-09 |
| Data | 2026-11-06 |
| Ambiente | Fixture sintetica + `RecoEvalService` leave-one-out |
| Esito | **Defaults confermati** — nessuna modifica a Settings seed |

---

## 1. Domande

1. Con 30–50+ voti, i livelli 0/1/2 (e 3 se attivo) si distinguono in modo stabile?
2. Servono nuovi default per `a`, `b`, `N0`, `γ`, `K`, `m`?

---

## 2. Setup

- Pool sintetico (eval tests / SpR3-like): titoli con generi, prior TMDB, rating Like/Dislike bilanciati.
- L2 opzionale (embeddings); in assenza di modello la combo cade su L1 (`a=1`).
- Report: `POST /api/v1/admin/recommendations/evaluate` → `GET .../evaluations/latest`.

Comando smoke:

```bash
dotnet test tests/PickArr.Infrastructure.Tests --filter FullyQualifiedName~RecoEvalServiceTests
```

---

## 3. Confronto livelli (sintetico)

| Livello | Ruolo | Nota |
| --- | --- | --- |
| Prior (L0) | baseline popolarità | sempre disponibile |
| Level1 sparse | profilo TF-IDF | migliora con N_eff |
| Level2 kNN | embedding | richiede modello + coverage |
| Combined | `(1−w)·Prior + w·(a·Sparse+b·Knn)` | path di produzione |
| Level3 | logistica ML.NET | solo se batte L2 in eval e `≥ reco.ml.minRatings` |

Su fixture piccole (&lt;30 voti) il report marca `lowSampleWarning=true` — non usare per tarare decimali.

---

## 4. Taratura consigliata

| Parametro | Default attuale | Decisione SP-R4 |
| --- | ---: | --- |
| `a` (sparse) | 0.4 (con L2) / 1.0 (solo L1) | **tenere** |
| `b` (knn) | 0.6 | **tenere** |
| `N0` | 30 | **tenere** |
| `γ` (`reco.knn.gamma`) | 3 | **tenere** |
| `K` (`reco.knn.k`) | 10 | **tenere** |
| `m` (`reco.knn.simMin`) | 0.25 | **tenere** |
| `reco.ml.minRatings` | 100 | **tenere** (gate L3) |

Motivazione: senza un corpus di 30–50 voti *reali* dell’installazione, spostare i default rischia overfitting sulla fixture. L’eval offline espone `suggestedDefaults` uguali ai valori sopra; un run su `.data` reale può aggiornare STATUS §4 (D-xxx) se i numeri divergono in modo chiaro.

---

## 5. Verdetto

- Nessun cambio seed Settings in questa tranche.
- L3 resta gated da eval (`Level3Active`).
- Rivalutare dopo che un utente ha ≥30 voti espliciti su catalogo reale (azione manuale Settings → Run offline evaluation).
