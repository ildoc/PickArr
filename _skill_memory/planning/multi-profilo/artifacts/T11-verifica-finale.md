# T11 — Verifica finale indipendente

> Data: 2026-11-10
> Esito complessivo: **PASS**
> Revisore: sessione dedicata TR-09 + subagente explore verify-only `3e8932bb-1821-436f-88ad-850bf2f32889` (sola lettura codice); suite rieseguite nel parent senza correzioni a codice/test/piano

## Versione verificata

| Campo | Valore |
| --- | --- |
| git HEAD | `073a4bbeb32631ab2dc5bc903514434e42ba85af` |
| Worktree | 147 path porcelain (dirty/untracked); digest path+contenuti foglia `45de786dd962baf3` (156 entry dopo espansione directory) |
| Isolamento | Contesto nuovo post TR-08; subagente read-only; nessun fix applicato |
| Precedente | §9 NON ESEGUITA |

## Requisiti ANALYSIS §1

| ID | Esito | Evidenza |
| --- | --- | --- |
| R-01 | PASS | `Profile.IsAdmin`; indice filtrato `IX_Profiles_IsAdmin`; seed `EnsureAdminProfileAsync`; create senza admin; DELETE admin 409. `ProfileAdminMigrationTests`, `ProfilesApiTests` |
| R-02 | PASS | Backfill `AddProfiles.Up` su preferiti/voti/eventi/visti/reco/state/log/ArrAudit; `ArrLibraryItem` senza `ProfileId`. `ProfileAdminMigrationTests` |
| R-03 | PASS | `Profile.MaxProfiles = 10`; nome 1–40 trim, univoco case-insensitive; palette fissa. `ProfilesLimitApiTests`, `ProfilesApiTests` |
| R-04 | PASS | Create/delete/edit-altri richiedono actor admin (403); self-edit ok; admin non eliminabile (409). `ProfilesApiTests` |
| R-05 | PASS | PK `(ProfileId, TitleId)` su Favorite/Rating/Watched. Flag divergenti in `TitleQueryServiceTests` / `CatalogApiTests` |
| R-06 | PASS | `RequirePersonalAsync` → 400 senza header; catalogo/scheda `ReadOptionalPersonalAsync` → 200 flag vuoti. `PersonalHeaderApiTests` |
| R-07 | PASS | `TitleQueryService.PersonalFlags` per profilo dichiarato. `CatalogApiTests.Catalog_and_detail_flags_follow_the_profile_header` |
| R-08 | PASS | Build/state/log per profilo; modello su riga admin; `ArrAuditLog.ProfileId` da header. `RecoBuildServiceTests` |
| R-09 | PASS | `profile.UseImplicit` / `ExcludeInLibrary` dal profilo, non chiavi globali. `ProfileRecoPreferenceTests`, `Build_reads_use_implicit_from_the_profile_not_the_global_key` |
| R-10 | PASS | `RecoHousehold` tetto 0,5, solo Love/Like, zero a `N_eff ≥ N0`; rebuild sotto N0. `RecoHouseholdTests`, `RecoBuildServiceTests`, `RatingServiceTests` |
| R-11 | PASS | `resolveActiveProfile` + `ProfileGate` `/who`; Header → `/who`. `router.test.tsx` |
| R-12 | PASS | Settings link solo admin; redirect non-admin in `ProfileGate` (`router.tsx`); API settings aperte. `Header.test.tsx`, `PersonalHeaderApiTests` |
| R-13 | PASS | `WhoPage`: admin crea/elimina/modifica tutti; self-edit; avatar colore+iniziale. `WhoPage.test.tsx` |
| R-14 | PASS | `documento-funzionale.md`, `specifica-tecnica.md`, `raccomandazioni.md`, `AGENTS.md` descrivono profili, migrazione, header, settings UI-only, prior di casa |

## Decisioni

| ID | Esito | Nota |
| --- | --- | --- |
| D-006 | CONFERMATA | `RecoHousehold.SlotWeight` / `MaxShare=0.5`; solo Love/Like; zero a raw=1; livello sparso; docs BR-REC-15 |
| D-008 / D-014 | CONFERMATE | Ruoli sull’id dichiarato (spoofabile); settings API aperte; UI admin-only |

## Spot-check indipendente (subagente)

R-01…R-14 PASS su codice/test/docs del worktree. FLUSSO-02 in STATUS §2 era stale (IN CORSO) nonostante T02/T04/T05 DONE: corretto a DONE in chiusura. Redirect settings realizzato in `ProfileGate`, non in `SettingsLayout`.

## Comandi (parent, 2026-11-10)

| Comando | Esito |
| --- | --- |
| `dotnet test tests/Pickarr.Core.Tests` | **94 PASS** |
| `dotnet test tests/Pickarr.Infrastructure.Tests --filter FullyQualifiedName!~SpR3PerformanceSmokeTests` | **149 PASS** |
| `dotnet test tests/Pickarr.Integration.Tests` | **52 PASS** |
| `cd src/Pickarr.WebApp && npm test -- --run` | **85 PASS** (34 file) |
| `npm run typecheck` | OK |
| `npm run lint` | OK (warning preesistenti `set-state-in-effect` / `only-export-components`, non errori) |

Nota: smoke `SpR3PerformanceSmokeTests` escluso come nelle verifiche tranche recenti (NFR host, non requisito multi-profilo).

## Difetti bloccanti

Nessuno.

## Non bloccanti

- F-001: worktree già sporco prima del dominio (non revertare); accettato in verifica.
- Unicità nome profilo: enforcement applicativo, senza unique index SQLite.
- Warning oxlint su settings/arr/`profileContext` (preesistenti o non bloccanti).

## Rinvii / rischi approvati

Nessuno obbligatorio. Nessun requisito R-01…R-14 rinviato.

## Verdetto

**PASS** su HEAD `073a4bbe` + worktree digest `45de786dd962baf3`.
