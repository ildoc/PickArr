import { apiGet } from './client'

export type SystemStatus = {
  version: string
  utcNow: string
}

export function fetchSystemStatus() {
  return apiGet<SystemStatus>('/api/v1/system/status')
}
