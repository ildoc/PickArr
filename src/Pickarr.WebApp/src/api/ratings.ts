import { apiDelete, apiGet, apiPost, apiPut, buildQuery } from './client'
import type { PagedTitles, RatingValue, TitleQueryParams } from './types'

export type { RatingValue }

export function setRating(titleId: number, value: RatingValue) {
  return apiPut(`/api/v1/titles/${titleId}/rating`, { value }, { personal: true })
}

export function clearRating(titleId: number) {
  return apiDelete(`/api/v1/titles/${titleId}/rating`, { personal: true })
}

export function fetchRatings(params: TitleQueryParams & { value: RatingValue }) {
  return apiGet<PagedTitles>(
    `/api/v1/ratings${buildQuery({
      value: params.value,
      q: params.q,
      type: params.type,
      genre: params.genre,
      sort: params.sort,
      dir: params.dir,
      page: params.page,
      pageSize: params.pageSize ?? 48,
    })}`,
    { personal: true },
  )
}

export type RatingExportItem = {
  tmdbType: string
  tmdbId: number
  name: string
  value: RatingValue
  ratedAt: string
}

export type RatingImportResult = {
  imported: number
  unknown: number
  ignored: number
}

export function exportRatings() {
  return apiGet<RatingExportItem[]>('/api/v1/ratings/export', { personal: true })
}

export function importRatings(body: {
  mode: 'merge' | 'replace'
  items: Array<{ tmdbType: string; tmdbId: number; value: RatingValue }>
}) {
  return apiPost<RatingImportResult>('/api/v1/ratings/import', body, { personal: true }) as Promise<RatingImportResult>
}
