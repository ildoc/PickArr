import type { AdminJob } from '../../api/admin'

export type JobKindGroup = {
  kind: string
  jobs: AdminJob[]
}

export type GroupedJobs = {
  kinds: JobKindGroup[]
  recommendations: AdminJob[]
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

const KIND_ORDER = ['CatalogIncremental', 'CatalogFull', 'Leaving'] as const

const RECO_KIND_ORDER = [
  'RecoFeature',
  'RecoEmbedding',
  'RecoBuild',
  'RecoEval',
  'RecoModelDownload',
] as const

const RECO_KINDS = new Set<string>(RECO_KIND_ORDER)

export function sortJobsByState(jobs: AdminJob[]): AdminJob[] {
  return [...jobs].sort(
    (a, b) => (STATE_RANK[a.executionState] ?? 9) - (STATE_RANK[b.executionState] ?? 9),
  )
}

function sortByProviderCountry(jobs: AdminJob[]): AdminJob[] {
  return [...jobs].sort((a, b) => {
    const provider = (a.providerName ?? '').localeCompare(b.providerName ?? '', undefined, {
      sensitivity: 'base',
    })
    if (provider !== 0) return provider
    return (a.countryCode ?? '').localeCompare(b.countryCode ?? '', undefined, {
      sensitivity: 'base',
    })
  })
}

function sortRecoJobs(jobs: AdminJob[]): AdminJob[] {
  return [...jobs].sort((a, b) => {
    const ai = RECO_KIND_ORDER.indexOf(a.kind as (typeof RECO_KIND_ORDER)[number])
    const bi = RECO_KIND_ORDER.indexOf(b.kind as (typeof RECO_KIND_ORDER)[number])
    const aRank = ai === -1 ? RECO_KIND_ORDER.length : ai
    const bRank = bi === -1 ? RECO_KIND_ORDER.length : bi
    if (aRank !== bRank) return aRank - bRank
    return (STATE_RANK[a.executionState] ?? 9) - (STATE_RANK[b.executionState] ?? 9)
  })
}

/** Label when the job already sits under a kind heading (provider + country, or country alone). */
export function shortCatalogJobName(job: AdminJob): string {
  if (job.providerName && job.countryCode) {
    return `${job.providerName} ${job.countryCode.toUpperCase()}`
  }
  if (job.countryCode) {
    return job.countryCode.toUpperCase()
  }
  return job.displayName
}

export function groupAdminJobs(jobs: AdminJob[]): GroupedJobs {
  const byKind = new Map<string, AdminJob[]>()
  const recommendations: AdminJob[] = []
  const system: AdminJob[] = []

  for (const job of jobs) {
    if (job.kind === 'CatalogIncremental' || job.kind === 'CatalogFull' || job.kind === 'Leaving') {
      const list = byKind.get(job.kind) ?? []
      list.push(job)
      byKind.set(job.kind, list)
      continue
    }
    if (RECO_KINDS.has(job.kind)) {
      recommendations.push(job)
      continue
    }
    system.push(job)
  }

  const kinds: JobKindGroup[] = KIND_ORDER.flatMap((kind) => {
    const kindJobs = byKind.get(kind)
    if (!kindJobs?.length) return []
    return [{ kind, jobs: sortByProviderCountry(kindJobs) }]
  })

  return {
    kinds,
    recommendations: sortRecoJobs(recommendations),
    system: sortJobsByState(system),
  }
}
