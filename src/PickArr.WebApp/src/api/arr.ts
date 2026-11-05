import { ApiError, apiGet, buildQuery } from './client'
import type { TitleDetail } from './types'

export type ArrOptionsResponse = {
  kind: string
  configured: boolean
  reachable: boolean
  qualityProfiles: Array<{ id: number; name: string }>
  rootFolders: Array<{ path: string; freeSpace: number | null }>
  tags: Array<{ id: number; label: string }>
  defaults: { qualityProfileId: number | null; rootFolderPath: string | null } | null
  monitorOptions?: string[]
  seriesTypes?: string[]
  minimumAvailabilityOptions?: string[]
}

export type ArrTestResponse = {
  ok: boolean
  version?: string
  appName?: string
  error?: string
  qualityProfiles?: Array<{ id: number; name: string }>
  rootFolders?: Array<{ path: string; freeSpace: number | null }>
}

export type ArrAddBody =
  | {
      qualityProfileId: number
      rootFolderPath: string
      monitor: string
      monitorNewItems: string
      seriesType: string
      seasonFolder: boolean
      tags: number[]
      searchNow: boolean
      seasons?: number[]
    }
  | {
      qualityProfileId: number
      rootFolderPath: string
      monitored: boolean
      minimumAvailability: string
      tags: number[]
      searchNow: boolean
    }

export function fetchArrOptions(kind: 'sonarr' | 'radarr') {
  return apiGet<ArrOptionsResponse>(`/api/v1/arr/options${buildQuery({ kind })}`)
}

export async function postArrTest(body: {
  kind: string
  url?: string
  apiKey?: string
}): Promise<ArrTestResponse> {
  const response = await fetch('/api/v1/arr/test', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    throw new ApiError(`Arr test failed: ${response.status}`, response.status)
  }
  return (await response.json()) as ArrTestResponse
}

export async function refreshTitleArr(titleId: number) {
  const response = await fetch(`/api/v1/titles/${titleId}/arr/refresh`, { method: 'POST' })
  if (!response.ok) {
    throw new ApiError(`Refresh failed: ${response.status}`, response.status)
  }
  return response.json()
}

export async function postTitleArr(titleId: number, body: ArrAddBody): Promise<TitleDetail['arr']> {
  const response = await fetch(`/api/v1/titles/${titleId}/arr`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  const payload = await response.json().catch(() => ({}))
  if (!response.ok) {
    const detail =
      typeof payload === 'object' && payload && 'detail' in payload
        ? String((payload as { detail: string }).detail)
        : `Add failed: ${response.status}`
    throw new ApiError(detail, response.status)
  }
  return payload as TitleDetail['arr']
}

export async function deleteTitleArr(
  titleId: number,
  deleteFiles: boolean,
  addImportExclusion: boolean,
): Promise<TitleDetail['arr']> {
  const qs = `?deleteFiles=${deleteFiles ? 'true' : 'false'}&addImportExclusion=${addImportExclusion ? 'true' : 'false'}`
  const response = await fetch(`/api/v1/titles/${titleId}/arr${qs}`, { method: 'DELETE' })
  const payload = await response.json().catch(() => ({}))
  if (!response.ok) {
    const detail =
      typeof payload === 'object' && payload && 'detail' in payload
        ? String((payload as { detail: string }).detail)
        : `Delete failed: ${response.status}`
    throw new ApiError(detail, response.status)
  }
  return payload as TitleDetail['arr']
}
