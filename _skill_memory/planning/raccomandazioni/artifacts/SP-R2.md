# SP-R2 — ONNX + tokenizer + perf

| | |
| --- | --- |
| Task | T13 / TR-07 |
| Data | 2026-11-06 |
| HEAD base | `58acd390c989de2f68e789c03b1bc01c9d683aab` + worktree reco |
| Esito | **GO** per TR-08 (T15+) con modello quantizzato |

---

## 1. Domande dello spike

1. Quale tokenizer WordPiece .NET?
2. Pooling mean + L2 riproducibile?
3. Variante quantizzata int8 ~23 MB usabile con ONNX Runtime CPU?
4. Ordine di grandezza titoli/s e RAM?
5. Conferma `modelId` = `all-MiniLM-L6-v2@q8` (D-REC-11)?

---

## 2. Decisioni

| Aspetto | Scelta | Note |
| --- | --- | --- |
| Runtime | `Microsoft.ML.OnnxRuntime` (CPU) | Native rid/win/linux-x64 + linux-arm64 via package |
| Tokenizer | `Microsoft.ML.Tokenizers` `BertTokenizer` + `vocab.txt` | WordPiece; max 256 token; special tokens `[CLS]`/`[SEP]` |
| Modello | Xenova `all-MiniLM-L6-v2` `onnx/model_quantized.onnx` (~23 MB) | Apache-2.0; allineato D-REC-11 |
| modelId | `all-MiniLM-L6-v2@q8` | Invariato vs ANALYSIS K-014 |
| Dimensione | 384 | mean pooling su last_hidden_state × attention_mask, poi L2 |
| Varianti CPU | Un solo ONNX quantizzato per amd64/arm64 | Le varianti avx* del doc HF sentence-transformers non sono necessarie: il Runtime gestisce le istruzioni; file unico più semplice da hashare |
| Distribuzione | Download HF → `/config/models/{modelId}/model.onnx` + `vocab.txt` + `model.json` | URL configurabile; SHA-256 in codice |

**Fonte file:** `https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx` e `…/vocab.txt`.

---

## 3. Self-test (design)

Tre frasi di riferimento; dopo load, coseno tra embedding runtime e vettori golden ≥ 0,99.  
Golden generati al primo download valido e commitati in `RecoModelSelfTest.Golden` (o calcolati e fissati in T15).  
Se manca il file modello: stato `Absent`, L0/L1 restano attivi (FR-REC-ML-03).

---

## 4. Perf (stime / misure dichiarate)

| Metrica | Valore | Come |
| --- | --- | --- |
| Dimensione file | ~23 MB | listing HF `model_quantized.onnx` / `model_int8.onnx` |
| RAM sessione (ordine) | ~80–150 MB RSS extra | tipico MiniLM int8 + ORT; da rifare su host reale in smoke |
| Throughput CPU (ordine) | ~20–80 titoli/s batch 16, 2 thread | stima; smoke T15/T16 aggiorna se misurato |
| Immagine Docker | modello **fuori** immagine base | FR-REC-ML-02 |

Niente bench bloccante sulla CI senza cache modello: test ONNX **condizionali** (`Pickarr:Models:Path` con file presenti).

---

## 5. Rischi

| ID | Rischio | Mitigazione |
| --- | --- | --- |
| RR-04 | Tokenizer .NET ≠ transformers | Self-test coseno golden; BertTokenizer ufficiale ML |
| RR-05 | Quantizzazione degrada simili | Soglia coverage + fallback L1; SP-R4 taratura |

---

## 6. Verdetto

**GO** per T15–T17 con:

- `Microsoft.ML.OnnxRuntime` + `Microsoft.ML.Tokenizers`
- `all-MiniLM-L6-v2@q8` da Xenova quantized ONNX
- K-014 → **CONFIRMED** (ipotesi accettata per implementazione; misure fine in smoke)

ANALYSIS §7: nessuna cambio di `modelId`.
