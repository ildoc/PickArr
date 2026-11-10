import { apiDelete, apiPut } from './client'

export function addWatched(titleId: number) {
  return apiPut(`/api/v1/titles/${titleId}/watched`, undefined, { personal: true })
}

export function removeWatched(titleId: number) {
  return apiDelete(`/api/v1/titles/${titleId}/watched`, { personal: true })
}
