# T27 — Verifica finale indipendente

> Data: 2026-11-05  
> Esito complessivo: **PASS_WITH_DEFERRALS**  
> Revisore: subagente explore read-only `[T27 verify-only explorer](393e4150-61ab-4275-9fc2-e20e780739fd)` + rievidenza comandi in sessione parent (nessuna modifica codice applicativo)

## Versione verificata

| Campo | Valore |
| --- | --- |
| Parent git HEAD | `4869916ee25b013223e302c12885c3b3d9458bd2` |
| Tree PickArr | `todo/pickarr/` **untracked** nel repo padre `idee` |
| Docker image | `pickarr:local` id `2bac92ff0821` / sha256 `2bac92ff08212d1387f22268c2087f8e69550555fbcf4f6ed1ed2c025343b896` (2026-11-05 11:19 CET) |
| Isolamento | Revisore codice in contesto subagente separato (solo lettura); comandi test/health rieseguiti senza fix |

## Requisiti ANALYSIS §1

| ID | Esito | Evidenza |
| --- | --- | --- |
| R-01 | PASS_WITH_DEFERRAL | Dockerfile + compose amd64 docs; `.gitlab-ci.yml` buildah; smoke `/health` **200** su `pickarr:local`. **Rinvio:** pipeline GitLab remota non eseguita |
| R-02 | PASS | `CatalogIncrementalSync` / `CatalogFullSync` + guard rimozioni `NeedsReview`; seed Netflix IT/US |
| R-03 | PASS | `EnrichmentService` + fallback `LanguageFallback.English`; SP-2 |
| R-04 | PASS | `TitleQueryService` + `CatalogPage` / preferiti / `en.json`; Vitest + build OK |
| R-05 | PASS | `arrNotConfigured` / `ArrMediaFilter` (D-011) |
| R-06 | PASS | `ArrLibrarySync` cache intact; `ArrStatusBadge` |
| R-07 | PASS | `ArrAddService` / `ArrDeleteService` + audit + dialog |
| R-08 | PASS | `AdminJobService` + Jobs/Quota UI |
| R-09 | PASS | `LeavingSync` + nota `catalog.leavingFreeNote` |
| R-10 | PASS | `ProtectedSettingsStore` + Data Protection; Settings isSet / test connection |
| R-11 | PASS | Questo rapporto |

## Comandi (2026-11-05)

| Comando | Esito |
| --- | --- |
| `dotnet test` (backend) | **90 PASS** (35 Core + 45 Infra + 10 Integration) |
| `npm test -- --run` | **8 PASS** |
| `npm run build` | OK |
| `docker run pickarr:local` → `/health` | **HTTP 200** Healthy |

## KPI (STATUS §6)

| KPI | Atteso | Verificato |
| --- | --- | --- |
| p95 10k titoli | &lt; 300 ms | SP-5: list 49.6 / search 50.2 ms |
| Container | `/health` 200 | OK |
| CI remota | verde | **non eseguita** (YAML presente) |

## Difetti bloccanti

Nessuno.

## Rinvii / rischi espliciti

1. **Pipeline GitLab remota** non ancora run su runner (D-017 DoD parziale; smoke locale sostitutivo accettato con rinvio).
2. **Dockerfile** non pinna `--platform=linux/amd64` (claim in README/compose; rischio build su host non-amd64).
3. Codice PickArr ancora **untracked** nel monorepo padre (scoperta ANALYSIS §7).
4. Nessuna live call Watchmode/TMDB/Arr in verifica (fixture/WireMock + docs ufficiali).

## Fuori scope (non richiesti)

Auth, change feed, multi-servizio UI, webhook/SSE, OMDb, i18n IT, arm64 — confermati fuori MVP (D-015).
