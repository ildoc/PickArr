import { ApiError, apiGet, buildQuery } from './client'

export type AdminJob = {
  jobKey: string
  displayName: string
  kind: string
  sourceId: number | null
  countryCode: string | null
  cron: string | null
  lastExecution: string | null
  nextExecution: string | null
  lastOutcome: string | null
  lastRunId: string | null
  canTrigger: boolean
  executionState: string
  lastError: string | null
  description: string
}

export type AdminRun = {
  id: string
  kind: string
  sourceId: number | null
  countryCode: string | null
  triggeredBy: string
  status: string
  startedAt: string
  finishedAt: string | null
  creditsUsed: number
  pagesFetched: number
  titlesAdded: number
  titlesUpdated: number
  titlesRemoved: number
  errorMessage: string | null
}

export type PagedAdminRuns = {
  page: number
  pageSize: number
  total: number
  items: AdminRun[]
}

export type AdminQuota = {
  service: string
  quota: number
  used: number
  reserve: number
  spendable: number
  projectedMonthEnd: number | null
  lastUpdatedAt: string | null
  periodStart: string
  periodEnd: string
}

export type TriggerResult = {
  runId: string | null
  hangfireJobId: string
}

export function fetchAdminJobs() {
  return apiGet<AdminJob[]>('/api/v1/admin/jobs')
}

export function fetchAdminRuns(
  params: { kind?: string; status?: string; jobKey?: string; page?: number; pageSize?: number } = {},
) {
  return apiGet<PagedAdminRuns>(`/api/v1/admin/runs${buildQuery(params)}`)
}

export function fetchAdminQuota() {
  return apiGet<AdminQuota>('/api/v1/admin/quota')
}

export async function triggerAdminJob(
  jobKey: string,
  body?: { sourceId?: number; mode?: string },
): Promise<TriggerResult> {
  const response = await fetch(`/api/v1/admin/jobs/${encodeURIComponent(jobKey)}/trigger`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body ?? {}),
  })
  const payload = await response.json().catch(() => ({}))
  if (!response.ok) {
    const detail =
      typeof payload === 'object' && payload && 'detail' in payload
        ? String((payload as { detail: string }).detail)
        : `Trigger failed: ${response.status}`
    throw new ApiError(detail, response.status)
  }
  return payload as TriggerResult
}
