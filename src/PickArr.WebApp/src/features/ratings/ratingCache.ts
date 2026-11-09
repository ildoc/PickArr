import type { InfiniteData, QueryClient } from '@tanstack/react-query'
import type { PagedTitles, RecommendationsPage, TitleDetail, TitleListItem } from '../../api/types'

export type UserRating = 'love' | 'like' | 'dislike' | 'not_interested' | null

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

function patchRecommendationsPage(
  page: RecommendationsPage,
  titleId: number,
  rating: UserRating,
): RecommendationsPage {
  // Rated titles leave For you immediately (FR-REC-RT-04).
  const items =
    rating == null
      ? page.items.map((item) => (item.id === titleId ? { ...item, rating: null } : item))
      : page.items.filter((item) => item.id !== titleId)
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

export async function invalidateRatingQueries(queryClient: QueryClient, titleId: number) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ['titles'] }),
    queryClient.invalidateQueries({ queryKey: ['my-list'] }),
    queryClient.invalidateQueries({ queryKey: ['title', titleId] }),
    queryClient.invalidateQueries({ queryKey: ['recommendations'] }),
    queryClient.invalidateQueries({ queryKey: ['ratings'] }),
    queryClient.invalidateQueries({ queryKey: ['reco-status'] }),
  ])
}
