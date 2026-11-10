import type { AdminJob } from '../../api/admin'

export type JobCountryGroup = {
  countryCode: string
  jobs: AdminJob[]
}

export type JobProviderGroup = {
  providerName: string
  countries: JobCountryGroup[]
}

export type GroupedJobs = {
  providers: JobProviderGroup[]
  leaving: AdminJob[]
  system: AdminJob[]
}

const STATE_RANK: Record<string, number> = {
  Running: 0,
  Queued: 1,
  Failed: 2,
  NeedsReview: 3,
  Paused: 4,
  PartiallySucceeded: 5,
  Succeeded: 6,
  Scheduled: 7,
  Idle: 8,
}

export function sortJobsByState(jobs: AdminJob[]): AdminJob[] {
  return [...jobs].sort(
    (a, b) => (STATE_RANK[a.executionState] ?? 9) - (STATE_RANK[b.executionState] ?? 9),
  )
}

/** Short label when the job already sits under provider + country headings. */
export function shortCatalogJobName(job: AdminJob): string {
  switch (job.kind) {
    case 'CatalogIncremental':
      return 'Import recent titles'
    case 'CatalogFull':
      return 'Full catalog import'
    default:
      return job.displayName
  }
}

export function groupAdminJobs(jobs: AdminJob[]): GroupedJobs {
  const catalog: AdminJob[] = []
  const leaving: AdminJob[] = []
  const system: AdminJob[] = []

  for (const job of jobs) {
    if (job.providerName && job.countryCode) {
      catalog.push(job)
    } else if (job.kind === 'Leaving' || (job.countryCode && !job.providerName)) {
      leaving.push(job)
    } else {
      system.push(job)
    }
  }

  const byProvider = new Map<string, Map<string, AdminJob[]>>()
  for (const job of catalog) {
    const provider = job.providerName!
    const country = job.countryCode!.toUpperCase()
    let countries = byProvider.get(provider)
    if (!countries) {
      countries = new Map()
      byProvider.set(provider, countries)
    }
    const list = countries.get(country) ?? []
    list.push(job)
    countries.set(country, list)
  }

  const providers: JobProviderGroup[] = [...byProvider.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([providerName, countries]) => ({
      providerName,
      countries: [...countries.entries()]
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([countryCode, countryJobs]) => ({
          countryCode,
          jobs: sortJobsByState(countryJobs),
        })),
    }))

  return {
    providers,
    leaving: sortJobsByState(leaving),
    system: sortJobsByState(system),
  }
}
