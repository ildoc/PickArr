import type { InfiniteData, QueryClient } from '@tanstack/react-query'
import type {
  PagedTitles,
  RatingValue,
  RecommendationsPage,
  TitleDetail,
  TitleListItem,
} from '../../api/types'

export type UserRating = RatingValue | null

function isPagedTitles(data: unknown): data is InfiniteData<PagedTitles> {
  return (
    typeof data === 'object' &&
    data !== null &&
    Array.isArray((data as InfiniteData<PagedTitles>).pages) &&
    (data as InfiniteData<PagedTitles>).pages.every(
      (page) => page != null && Array.isArray(page.items),
    )
  )
}

function isRecommendationsPage(data: unknown): data is RecommendationsPage {
  return (
    typeof data === 'object' &&
    data !== null &&
    Array.isArray((data as RecommendationsPage).items) &&
    !Array.isArray((data as InfiniteData<RecommendationsPage>).pages)
  )
}

function isRecommendationsInfinite(data: unknown): data is InfiniteData<RecommendationsPage> {
  return (
    typeof data === 'object' &&
    data !== null &&
    Array.isArray((data as InfiniteData<RecommendationsPage>).pages) &&
    (data as InfiniteData<RecommendationsPage>).pages.every(
      (page) => page != null && Array.isArray(page.items),
    )
  )
}

function patchPagedRating(
  data: InfiniteData<PagedTitles> | undefined,
  titleId: number,
  rating: UserRating,
): InfiniteData<PagedTitles> | undefined {
  if (!isPagedTitles(data)) return data
  return {
    ...data,
    pages: data.pages.map((page) => ({
      ...page,
      items: page.items.map((item) =>
        item.id === titleId ? { ...item, rating } : item,
      ),
    })),
  }
}

/** Hold the selected vote on the card, then fade out before leaving For you (FR-REC-RT-04). */
export const RECO_DISMISS_HOLD_MS = 450
export const RECO_DISMISS_FADE_MS = 350
export const RECO_DISMISS_TOTAL_MS = RECO_DISMISS_HOLD_MS + RECO_DISMISS_FADE_MS

function patchRecommendationsPage(
  page: RecommendationsPage,
  titleId: number,
  rating: UserRating,
): RecommendationsPage {
  // Keep the card briefly so the UI can fade it out; removal is scheduled separately.
  return {
    ...page,
    items: page.items.map((item) => (item.id === titleId ? { ...item, rating } : item)),
  }
}

function stripRecommendationTitle(
  page: RecommendationsPage,
  titleId: number,
): RecommendationsPage {
  const items = page.items.filter((item) => item.id !== titleId)
  const removed = page.items.length - items.length
  return {
    ...page,
    items,
    total: Math.max(0, page.total - removed),
  }
}

function patchRecommendations(
  data: InfiniteData<RecommendationsPage> | RecommendationsPage | undefined,
  titleId: number,
  rating: UserRating,
): InfiniteData<RecommendationsPage> | RecommendationsPage | undefined {
  if (isRecommendationsInfinite(data)) {
    return {
      ...data,
      pages: data.pages.map((page) => patchRecommendationsPage(page, titleId, rating)),
    }
  }
  if (isRecommendationsPage(data)) {
    return patchRecommendationsPage(data, titleId, rating)
  }
  return data
}

function patchRatingsLists(
  data: InfiniteData<PagedTitles> | undefined,
  titleId: number,
  rating: UserRating,
): InfiniteData<PagedTitles> | undefined {
  if (!isPagedTitles(data)) return data
  return {
    ...data,
    pages: data.pages.map((page) => {
      const items = page.items
        .map((item) => {
          if (item.id !== titleId) return item
          if (rating == null) return null
          return { ...item, rating }
        })
        .filter((item): item is TitleListItem => item != null)
      return { ...page, items }
    }),
  }
}

export function applyOptimisticRating(
  queryClient: QueryClient,
  titleId: number,
  rating: UserRating,
) {
  queryClient.setQueriesData<InfiniteData<PagedTitles>>({ queryKey: ['titles'] }, (data) =>
    patchPagedRating(data, titleId, rating),
  )
  // Only title pages — ['my-list','types'] / ['my-list','genres'] share the prefix.
  queryClient.setQueriesData<InfiniteData<PagedTitles>>({ queryKey: ['my-list', 'titles'] }, (data) =>
    patchPagedRating(data, titleId, rating),
  )
  queryClient.setQueriesData<InfiniteData<PagedTitles>>({ queryKey: ['ratings'] }, (data) =>
    patchRatingsLists(data, titleId, rating),
  )
  queryClient.setQueriesData<InfiniteData<RecommendationsPage> | RecommendationsPage>(
    { queryKey: ['recommendations'] },
    (data) => patchRecommendations(data, titleId, rating),
  )
  queryClient.setQueriesData<TitleDetail>({ queryKey: ['title', titleId] }, (detail) =>
    detail && typeof detail === 'object' && 'id' in detail ? { ...detail, rating } : detail,
  )
}

/** Drop a rated title from For you / catalog reco rows after the dismiss animation. */
export function removeRatedRecommendation(queryClient: QueryClient, titleId: number) {
  queryClient.setQueriesData<InfiniteData<RecommendationsPage> | RecommendationsPage>(
    { queryKey: ['recommendations'] },
    (data) => {
      if (isRecommendationsInfinite(data)) {
        return {
          ...data,
          pages: data.pages.map((page) => stripRecommendationTitle(page, titleId)),
        }
      }
      if (isRecommendationsPage(data)) {
        return stripRecommendationTitle(data, titleId)
      }
      return data
    },
  )
}

export async function invalidateRatingQueries(
  queryClient: QueryClient,
  titleId: number,
  options?: { includeRecommendations?: boolean },
) {
  const includeRecommendations = options?.includeRecommendations !== false
  const tasks = [
    queryClient.invalidateQueries({ queryKey: ['titles'] }),
    queryClient.invalidateQueries({ queryKey: ['my-list'] }),
    queryClient.invalidateQueries({ queryKey: ['title', titleId] }),
    queryClient.invalidateQueries({ queryKey: ['ratings'] }),
    queryClient.invalidateQueries({ queryKey: ['reco-status'] }),
  ]
  if (includeRecommendations) {
    tasks.push(queryClient.invalidateQueries({ queryKey: ['recommendations'] }))
  }
  await Promise.all(tasks)
}
