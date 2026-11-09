import { apiDelete, apiPut } from './client'

export function addWatched(titleId: number) {
  return apiPut(`/api/v1/titles/${titleId}/watched`)
}

export function removeWatched(titleId: number) {
  return apiDelete(`/api/v1/titles/${titleId}/watched`)
}
