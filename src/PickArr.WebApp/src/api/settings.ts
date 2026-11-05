import { apiGet, apiPut } from './client'
import type { SettingsResponse } from './types'

export function fetchSettings() {
  return apiGet<SettingsResponse>('/api/v1/settings')
}

export function updateSettings(body: Record<string, unknown>) {
  return apiPut<SettingsResponse>('/api/v1/settings', body)
}
