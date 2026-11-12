import { apiGet, apiPost, apiPut } from './client'
import type { SettingsResponse } from './types'

export type ConnectionTestResponse = {
  ok: boolean
  detail?: string | null
}

export function fetchSettings() {
  return apiGet<SettingsResponse>('/api/v1/settings')
}

export function updateSettings(body: Record<string, unknown>) {
  return apiPut<SettingsResponse>('/api/v1/settings', body)
}

export function postConnectionTest(body: { service: 'watchmode' | 'tmdb' | 'omdb'; apiKey?: string }) {
  return apiPost<ConnectionTestResponse>('/api/v1/settings/test', body) as Promise<ConnectionTestResponse>
}
