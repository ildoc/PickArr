# SP-4 — Hangfire storage SQLite vs memory (T07)

> Data: 2026-11-04  
> Pacchetto: `Hangfire.Storage.SQLite` **0.4.3** (+ `Hangfire.AspNetCore` 1.8.23, fallback `Hangfire.InMemory` 1.0.0)

## Prove eseguite

1. Host wiring in `Pickarr.Infrastructure.Jobs.HangfireBootstrap` con file dedicato `{DataPath}/jobs.db` (WAL).
2. Test `HangfireSqliteStorageTests.Sqlite_storage_survives_restart_and_keeps_recurring_job` — recurring job presente dopo riapertura storage.
3. Test `ScheduleReconciler_reapplies_probe_after_storage_reset` — dopo wipe di `jobs.db`, `ScheduleReconcilerStub` ri-registra il cron (modello per T12).

## Decisione (D-013 / V-09 / F-002)

| Scelta | Dettaglio |
| --- | --- |
| **Default** | **SQLite** community su `jobs.db` separato da `Pickarr.db` |
| **Fallback** | `Hangfire:Storage=Memory` (`Hangfire.InMemory`) oppure fallback automatico se init SQLite fallisce |
| **Schedule applicativi** | Sempre ricreati da DB app (`ScheduleReconciler`); Hangfire tiene solo code/retry/transienti |

## Impatto

- F-002 → **RESOLVED** (SQLite usable; memory resta opzione documentata).
- T12 userà lo stesso bootstrap; stub probe va sostituito con job reali catalog/arr.

## Config

```json
"Hangfire": {
  "Storage": "Sqlite",
  "SqliteFileName": "jobs.db",
  "DashboardEnabled": false
}
```
