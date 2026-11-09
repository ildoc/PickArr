import { apiDelete, apiGet, apiPost, apiPut, buildQuery } from './client'
import type { PagedTitles, TitleQueryParams } from './types'

export type RatingValue = 'love' | 'like' | 'dislike'

export function setRating(titleId: number, value: RatingValue) {
  return apiPut(`/api/v1/titles/${titleId}/rating`, { value })
}

export function clearRating(titleId: number) {
  return apiDelete(`/api/v1/titles/${titleId}/rating`)
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
  return apiGet<RatingExportItem[]>('/api/v1/ratings/export')
}

export function importRatings(body: {
  mode: 'merge' | 'replace'
  items: Array<{ tmdbType: string; tmdbId: number; value: RatingValue }>
}) {
  return apiPost<RatingImportResult>('/api/v1/ratings/import', body) as Promise<RatingImportResult>
}
