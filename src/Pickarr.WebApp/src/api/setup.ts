import { apiGet, apiPost } from './client'

export type SetupStatus = { completed: boolean }

export function fetchSetupStatus() {
  return apiGet<SetupStatus>('/api/v1/setup')
}

export function completeSetup() {
  return apiPost<SetupStatus>('/api/v1/setup/complete')
}
