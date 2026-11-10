import { apiGet, apiPost, buildQuery } from './client'
import type { RecoStatus, RecommendationsPage, SimilarTitlesPage } from './types'

export type RecommendationsQueryParams = {
  scope?: 'source' | 'enabled'
  sourceId?: number
  type?: string
  genre?: string
  includeInLibrary?: boolean
  diversify?: boolean
  page?: number
  pageSize?: number
}

export function fetchRecommendations(params: RecommendationsQueryParams) {
  return apiGet<RecommendationsPage>(
    `/api/v1/recommendations${buildQuery({
      scope: params.scope,
      sourceId: params.sourceId,
      type: params.type,
      genre: params.genre,
      includeInLibrary: params.includeInLibrary,
      diversify: params.diversify,
      page: params.page,
      pageSize: params.pageSize ?? 48,
    })}`,
    { personal: true },
  )
}

export function fetchRecoStatus() {
  return apiGet<RecoStatus>('/api/v1/recommendations/status', { personal: true })
}

export function fetchSimilarTitles(titleId: number, sourceId?: number | null, limit = 12) {
  return apiGet<SimilarTitlesPage>(
    `/api/v1/titles/${titleId}/similar${buildQuery({
      sourceId: sourceId ?? undefined,
      limit,
    })}`,
    { personal: true },
  )
}

export function fetchOnboarding(sourceId?: number | null, excludeTitleIds?: number[]) {
  return apiGet<RecommendationsPage>(
    `/api/v1/recommendations/onboarding${buildQuery({
      sourceId: sourceId ?? undefined,
      exclude: excludeTitleIds && excludeTitleIds.length > 0 ? excludeTitleIds.join(',') : undefined,
    })}`,
    { personal: true },
  )
}

export function postOnboardingRatings(items: Array<{ titleId: number; value: string | null }>) {
  return apiPost<void>('/api/v1/ratings/onboarding', items, { personal: true })
}

export function postRecoRebuild() {
  return apiPost<{ hangfireJobId: string }>('/api/v1/admin/recommendations/rebuild')
}

export function postRecoEmbeddingsRebuild(all = false) {
  return apiPost<{ hangfireJobId: string }>(
    `/api/v1/admin/recommendations/embeddings/rebuild${buildQuery({ all })}`,
  )
}

export function postRecoModelDownload() {
  return apiPost<{ hangfireJobId: string }>('/api/v1/admin/recommendations/model/download')
}

export function postRecoModelDetect() {
  return apiPost<{ hangfireJobId: string }>('/api/v1/admin/recommendations/model/detect')
}

export function postRecoReset(includeRatings: boolean) {
  return apiPost<{ includeRatings: boolean }>(
    `/api/v1/admin/recommendations/reset${buildQuery({ includeRatings })}`,
  )
}

export function postRecoEvaluate() {
  return apiPost<{ hangfireJobId: string }>('/api/v1/admin/recommendations/evaluate')
}

export function fetchLatestRecoEvaluation() {
  return apiGet<import('./types').RecoEvalReport>('/api/v1/admin/recommendations/evaluations/latest')
}

export function postRecoEvents(
  entries: Array<{ titleId: number; event: string; version?: number; rank?: number; scope?: string }>,
) {
  return apiPost<void>('/api/v1/recommendations/events', entries, { personal: true })
}
