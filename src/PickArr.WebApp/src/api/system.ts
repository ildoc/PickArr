import { apiGet, ApiError } from './client'

export type SystemStatus = {
  version: string
  utcNow: string
}

export type NamedTitleCount = {
  name: string
  titleCount: number
}

export type SourceTitleCount = {
  providerName: string
  countryCode: string
  titleCount: number
}

export type StorageEntry = {
  key: string
  relativePath: string | null
  bytes: number
}

export type SystemStats = {
  totalTitles: number
  byProvider: NamedTitleCount[]
  byCountry: NamedTitleCount[]
  bySource: SourceTitleCount[]
  storage: {
    totalBytes: number
    entries: StorageEntry[]
  }
}

export type LogFileInfo = {
  fileName: string
  bytes: number
  lastWriteUtc: string
}

export type LogFilesList = {
  items: LogFileInfo[]
}

export type ClearLogsResult = {
  deleted: number
  failed: number
}

export function fetchSystemStatus() {
  return apiGet<SystemStatus>('/api/v1/system/status')
}

export function fetchSystemStats() {
  return apiGet<SystemStats>('/api/v1/system/stats')
}

export function fetchSystemLogs() {
  return apiGet<LogFilesList>('/api/v1/system/logs')
}

export async function clearSystemLogs() {
  const response = await fetch('/api/v1/system/logs', { method: 'DELETE' })
  if (!response.ok) {
    throw new ApiError('DELETE /api/v1/system/logs failed', response.status)
  }
  return (await response.json()) as ClearLogsResult
}

export async function downloadSystemLog(fileName: string) {
  const response = await fetch(`/api/v1/system/logs/${encodeURIComponent(fileName)}`)
  if (!response.ok) {
    throw new ApiError(`GET /api/v1/system/logs/${fileName} failed`, response.status)
  }
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}
