# multi-profilo STATUS

> Ultimo aggiornamento: 2026-11-10
> Branch: `main`
> Storico operativo: `ARCHIVE.md`

## 0. Quick Context

- Obiettivo: fino a 10 profili senza autenticazione. Admin iniziale «Admin», dati personali migrati su di lui. Settings solo in UI. Prior di casa sotto `N0`. Requisiti: ANALYSIS.md §1 R-01..R-14.
- Stato dominio: **CHIUSO** (verifica finale PASS). TR-01…TR-09 chiuse.
- Codebase: `073a4bbeb32631ab2dc5bc903514434e42ba85af`
- Snapshot locale: HEAD invariato, non committato. Worktree digest verifica `45de786dd962baf3` (147 path porcelain). F-001 ACCEPTED (diff preesistente, non revertare).
- Tranche/task candidato: nessuno. Dominio completo.
- Dettagli: PLAN.md TR-09 T11 eseguita. Rapporto `artifacts/T11-verifica-finale.md`; ARCHIVE A-001; §9.
- Blocchi e findings critici: nessuno bloccante.
- Input umano: nessuno pendente. Commit del worktree solo su richiesta.
- Handoff: dominio multi-profilo chiuso. Nessuna tranche successiva in questo piano.
- Verifica finale: PASS — vedi §9.

## 1. Roadmap

### TR-01 — Schema e admin

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T01 | Profilo, migrazione, backfill admin | M | 🟡 | DONE | — | PLAN.md TR-01 T01; `20261109164224_AddProfiles`; test Infrastructure 137 pass |

### TR-02 — Servizi dati personali

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T02 | Preferiti, voti e visti per profilo | L | 🟡 | DONE | T01 | PLAN.md TR-02 T02; `20261109170529_ScopeFavoritesRatingsWatched`; Infrastructure 140 pass, Integration 45 pass |

### TR-03 — API profili e header

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T03 | API profili e invarianti | M | 🟡 | DONE | T01 | PLAN.md TR-03 T03; Integration 51 pass |
| T04 | Header sulle mutazioni e liste personali | M | 🟢 | DONE | T02, T03 | PLAN.md TR-03 T04; client `pickarr.profileId`; Vitest `client.test.ts` 8 pass |

### TR-04 — Flag nel catalogo

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T05 | DTO catalogo e scheda col profilo | M | 🟡 | DONE | T04 | PLAN.md TR-04 T05; TitleQueryServiceTests 10 pass; Integration 52 pass; `client.test.ts` 9 pass |

### TR-05 — Raccomandazioni per profilo

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T06 | Build, stato e segnali Arr per profilo | L | 🔴 | DONE | T02, T04 | PLAN.md TR-05 T06; `20261110083115_ScopeRecommendationsByProfile`; Infrastructure 144 pass; Integration 52 pass |

### TR-06 — Prior di casa

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T07 | Prior di casa sotto N0 | M | 🟡 | DONE | T06 | PLAN.md TR-06 T07; `RecoHouseholdTests` 4 pass; Infrastructure 147 pass |

### TR-07 — Interfaccia profili

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T08 | Selettore, memoria, guard settings | L | 🟡 | DONE | T03, T04 | PLAN.md TR-07 T08; Vitest router/header/source; browser su database temporaneo |
| T09 | Gestione profilo e preferenze reco | M | 🟡 | DONE | T06, T08 | PLAN.md TR-07 T09; WhoPage e settings Vitest; `ProfileRecoPreferenceTests` |

### TR-08 — Documenti

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T10 | Allineare docs e AGENTS.md | M | 🟢 | DONE | T07, T09 | PLAN.md TR-08 T10; lettura acceptance R-14 |

### TR-09 — Verifica finale indipendente

| ID | Task | Size | Rischio | Stato | Dipende da | Dettagli |
| --- | --- | --- | --- | --- | --- | --- |
| T11 | Verifica finale indipendente | M | 🔴 | DONE | TR-01, TR-02, TR-03, TR-04, TR-05, TR-06, TR-07, TR-08 | PLAN.md TR-09 T11; PASS; artifacts/T11-verifica-finale.md; ARCHIVE A-001; §9 |

## 2. Flussi — stato per area

| Flusso | Stato | Note |
| --- | --- | --- |
| FLUSSO-01 Apertura | ✅ DONE | Un profilo entra in catalogo. Più profili con id valido entrano diretti. Id assente o cancellato apre `/who`. Il controllo in header apre sempre `/who` |
| FLUSSO-02 Dati personali | ✅ DONE | Chiave `(ProfileId, TitleId)`. Liste, mutazioni, flag di catalogo/scheda e simili leggono `X-Pickarr-Profile`. Senza header il catalogo e la scheda restano 200 con flag vuoti |
| FLUSSO-03 Build reco | ✅ DONE | Build, versione, `ProfileJson` e log sono per profilo. Modello, copertura ed eval restano sulla riga admin. Il prior di casa è nel punteggio sotto `N0`. `useImplicit` e `excludeInLibrary` vengono dal profilo |
| FLUSSO-04 Arr | ✅ DONE | Un Add con header salva `ProfileId` e alimenta solo quel profilo. Senza header l'azione resta valida e il segnale non si attribuisce. Downloaded segue l'Add. Delete non cancella il segnale. La libreria resta globale |
| FLUSSO-05 Settings | ✅ DONE | Il link e `/settings` ci sono solo per l'admin. Le API settings restano aperte. I due toggle di gusto stanno sul profilo |
| FLUSSO-06 Prior di casa | ✅ DONE | `RecoHousehold`: tetto 0,5, solo Love/Like altrui, zero a `N_eff ≥ N0`, livello sparso. Un voto ricostruisce anche i profili sotto `N0` |

## 3. Findings aperti

| ID | Gravità | Descrizione | Task | Stato |
| --- | --- | --- | --- | --- |
| F-001 | Media | Worktree già modificato su file che T08, T09 e T10 devono toccare (`router.tsx`, `Header.tsx`, `SettingsLayout.tsx`, `en.json`, i tre doc). Non revertare il diff preesistente. T10 ha preservato gli hunk doc preesistenti (system/stats, LearningProgress). | T08, T09, T10 | ACCEPTED |

## 4. Decisioni architetturali

| ID | Data | Decisione | Owner | Motivazione | Fonte |
| --- | --- | --- | --- | --- | --- |
| D-001 | 2026-11-09 | Dominio nuovo `multi-profilo`. Non si estendono `pickarr-mvp`, `raccomandazioni`, `test-coverage`. | utente | Regola della skill: una richiesta, un dominio | messaggio 2026-11-09 |
| D-002 | 2026-11-09 | Massimo 10 profili. Il primo si chiama Admin, colore di default, modificabile. `IsAdmin` non si cede e non si elimina. | utente | admin-rinominabile | risposte 2026-11-09 |
| D-003 | 2026-11-09 | Dati personali: preferiti, voti, `RatingEvents`, visti, raccomandazioni, stato gusto, log reco. Catalogo, settings, embedding, libreria Arr restano globali. | utente | opzione gusti | risposte 2026-11-09 |
| D-004 | 2026-11-09 | Il divieto settings è solo UI: niente link e redirect da `/settings` per un profilo non admin. Nessun controllo sulle API settings, job, sistema, cataloghi. | utente | opzione ui | risposte 2026-11-09 |
| D-005 | 2026-11-09 | L'ultimo profilo valido è ricordato in `localStorage`. Con un solo profilo si salta il selettore. Il comando in header apre `/who`. | utente | opzioni ricorda e salta | risposte 2026-11-09 |
| D-006 | 2026-11-09 | Il motore resta content-based per profilo. Il prior di casa è la formula in ANALYSIS.md §6.2: tetto 0,5 sul complemento di `raw`, solo Love/Like altrui, zero a `raw = 1`, livello sparso soltanto. Niente CF. | piano, confermata T11 | Direzione «freddo» approvata. Il tetto 0,5 lascia metà slot al prior TMDB così un profilo vuoto non copia per intero un altro | opzione freddo; formula proposta nel piano; T11 |
| D-007 | 2026-11-09 | Header `X-Pickarr-Profile`. Mutazioni e liste personali senza header: 400. Catalogo senza header: 200 e flag personali vuoti. | piano | Serve a partizionare, non a autenticare | conseguenza di D-004 e R-06 |
| D-008 | 2026-11-09 | POST/DELETE profilo e PATCH di un altro profilo: 403 se l'id dichiarato non è admin. DELETE admin: 409. Il controllo è spoofabile. | piano, confermata T11 | La domanda UI-only riguardava i settings. Creare ed eliminare profili resta un requisito distinto | richiesta originale; D-014; T11 |
| D-009 | 2026-11-09 | La sorgente catalogo ricordata diventa `pickarr.sourceId.{profileId}`. Se manca e il profilo è admin, si legge una volta `pickarr.sourceId`. | piano | Evita che il cambio profilo trascini la sorgente dell'altro. La chiave legacy è già nel browser di chi aggiorna | A-005 |
| D-010 | 2026-11-09 | `useImplicit` e `excludeInLibrary` si leggono dal profilo solo da T09. Fino a T06 il build continua a leggere le chiavi globali, così la UI settings non mente nell'intervallo. | piano | Ordine delle tranche | ANALYSIS.md FLUSSO-05 |
| D-011 | 2026-11-09 | Nuove righe `ArrAuditLog` salvano il `ProfileId` se il header c'è. Senza header l'azione resta valida e il segnale di gusto non si attribuisce. Le righe vecchie vanno all'admin. `ArrLibraryItem` non è per profilo. | piano | Libreria condivisa, gusto dell'attore. K-006 | opzione tutti + R-08 |
| D-012 | 2026-11-09 | Stato modello (ONNX, copertura, level 3, eval modello) letto e scritto solo sulla riga `RecoState` dell'admin. Le altre righe portano versione, `N_eff`, stadio, `ProfileJson`, errore di build. | piano | A-002: un solo scrittore del modello | assessment |
| D-013 | 2026-11-09 | D-006 è confermata. Formula e tetto 0,5 restano quelli di ANALYSIS.md §6.2. Si implementa in T07. | utente | Conferma esplicita prima di TR-02 | messaggio 2026-11-09 |
| D-014 | 2026-11-09 | D-008 è confermata come divisione logica, senza autenticazione. L'admin vede i settings, crea ed elimina gli altri profili e modifica sé e tutti gli altri. Un profilo non admin non vede i settings e modifica solo sé. Selezionare il profilo admin concede i poteri admin. Il 403 sull'id dichiarato è spoofabile e accettato. | utente | «è sufficiente cambiare il proprio utente con quello amministratore»; «solo una divisione logica dei ruoli» | messaggio 2026-11-09 |
| D-015 | 2026-11-09 | Palette icona, in ordine: `blue`, `teal`, `green`, `yellow`, `orange`, `red`, `pink`, `purple`. Si salva in minuscolo. Il default è `blue`. I colori si possono ripetere. Un valore fuori palette è 400. | piano | T03 deve rifiutare i colori fuori palette; l'utente non ha elencato i nomi. T09 usa lo stesso elenco | `Profile.Palette` |

## 5. Activity log

| Data | Task | Azione | Esito | Evidenza |
| --- | --- | --- | --- | --- |
| 2026-11-09 | INIT | Creati ANALYSIS, STATUS, PLAN, addendum, ARCHIVE | DONE | `_skill_memory/planning/multi-profilo/` |
| 2026-11-09 | T01 | Profilo admin, migrazione e backfill | DONE | `dotnet test tests/Pickarr.Infrastructure.Tests`: 137 pass, 0 fail. K-006 confermata, vedi K-008 |
| 2026-11-09 | T02 | Preferiti, voti e visti per profilo | DONE | Infrastructure 140 pass, 0 fail; Integration 45 pass, 0 fail. Migrazione `20261109170529_ScopeFavoritesRatingsWatched`. K-009 |
| 2026-11-09 | T03 | API profili e invarianti | DONE | `dotnet test tests/Pickarr.Integration.Tests`: 51 pass, 0 fail. D-015 |
| 2026-11-09 | T04 | Header sulle liste personali | DONE | Stessa suite Integration 51 pass. `npm test -- --run src/api/client.test.ts`: 8 pass. K-010 |
| 2026-11-10 | T05 | Flag di catalogo e scheda per profilo | DONE | `TitleQueryServiceTests` 10 pass. Integration 52 pass. `client.test.ts` 9 pass. K-011 |
| 2026-11-10 | T06 | Build, stato e segnali Arr per profilo | DONE | Infrastructure 144 pass, Integration 52 pass. Migrazione `20261110083115_ScopeRecommendationsByProfile`. K-012 |
| 2026-11-10 | T07 | Prior di casa sotto N0 | DONE | `RecoHouseholdTests` 4 pass. Infrastructure 147 pass, smoke non rieseguito. K-013 |
| 2026-11-10 | T08 | Selettore, memoria, guard settings | DONE | Vitest di router, header e sorgente. Browser: un profilo in catalogo, id sconosciuto su `/who`, header apre `/who`, non admin senza Settings. K-014 |
| 2026-11-10 | T09 | Gestione profilo e preferenze reco | DONE | WhoPage e pagine settings. `ProfileRecoPreferenceTests` e `Build_reads_use_implicit_from_the_profile_not_the_global_key`. Browser: admin crea Kid, Kid non entra in Settings. K-014 |
| 2026-11-10 | T10 | Docs e AGENTS.md allineati a R-14 | DONE | Lettura: profili max 10, migrazione admin, header, settings UI-only, preferenze profilo, prior di casa, Arr condiviso+audit. Hunk F-001 sui tre doc preservati. Nessuna suite automatica |
| 2026-11-10 | T11 | Verifica finale indipendente R-01…R-14 | PASS | Spot-check subagente `3e8932bb`; Core 94 / Infra 149 (no smoke) / Integration 52 / Vitest 85; typecheck+lint OK; D-006 e D-008 confermate; F-001 ACCEPTED; artifacts/T11-verifica-finale.md; ARCHIVE A-001 |

## 7. Checklist archiviazione

| Condizione | Verificata? |
| --- | --- |
| Activity log ha > 10 righe DONE | ☐ |
| Bootstrap/pacchetto supera il budget orientativo | ☐ |
| ≥ 3 finding RESOLVED/ACCEPTED in §3 | ☐ |
| Una decisione nuova rende obsolete note precedenti | ☐ |

## 8. Tooling

| Aspetto | Convenzione |
| --- | --- |
| Branch | non creare branch senza richiesta |
| Commit | solo su richiesta esplicita |
| Test backend | `dotnet test` filtrato sul progetto toccato. Fermare `Pickarr.Api` prima di build/test che bloccano `src/Pickarr.Api/bin` |
| Test frontend | in `src/Pickarr.WebApp`: `npm test -- --run`, `npm run typecheck`, `npm run lint` |
| Migrazioni | `dotnet ef` da `src/Pickarr.Infrastructure`, startup `src/Pickarr.Api`, output `Persistence/Migrations` |
| UI | copy in `src/Pickarr.WebApp/src/i18n/locales/en.json` |

I percorsi `backend/` e `frontend/` di CONV-04 non esistono in questo repo. Per il dominio valgono i percorsi di AGENTS.md, sopra.

## 9. Verifica finale indipendente

| Campo | Valore |
| --- | --- |
| Task finale | T11 |
| Esito | **PASS** |
| Revisore e sessione | sessione TR-09 + explore verify-only `3e8932bb-1821-436f-88ad-850bf2f32889` |
| Isolamento effettivo | contesto nuovo post TR-08; sola lettura codice; nessun fix |
| Versione verificata | HEAD `073a4bbeb32631ab2dc5bc903514434e42ba85af` + digest `45de786dd962baf3` |
| Rapporto | `artifacts/T11-verifica-finale.md`; ARCHIVE A-001 |
| Rinvii/rischi approvati | nessuno; F-001 ACCEPTED (worktree preesistente) |
