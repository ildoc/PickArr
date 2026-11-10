import { apiGet } from './client'

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

export function fetchSystemStatus() {
  return apiGet<SystemStatus>('/api/v1/system/status')
}

export function fetchSystemStats() {
  return apiGet<SystemStats>('/api/v1/system/stats')
}
