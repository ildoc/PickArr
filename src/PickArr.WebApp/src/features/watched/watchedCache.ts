import type { InfiniteData, QueryClient } from '@tanstack/react-query'
import type { RecommendationsPage, TitleDetail } from '../../api/types'

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

function patchRecommendationsPage(
  page: RecommendationsPage,
  titleId: number,
  isWatched: boolean,
): RecommendationsPage {
  if (!isWatched) {
    return {
      ...page,
      items: page.items.map((item) =>
        item.id === titleId ? { ...item, isWatched: false } : item,
      ),
    }
  }

  const items = page.items.filter((item) => item.id !== titleId)
  const removed = page.items.length - items.length
  return {
    ...page,
    items,
    total: Math.max(0, page.total - removed),
  }
}

export function applyOptimisticWatched(
  queryClient: QueryClient,
  titleId: number,
  isWatched: boolean,
) {
  queryClient.setQueriesData<InfiniteData<RecommendationsPage> | RecommendationsPage>(
    { queryKey: ['recommendations'] },
    (data) => {
      if (isRecommendationsInfinite(data)) {
        return {
          ...data,
          pages: data.pages.map((page) => patchRecommendationsPage(page, titleId, isWatched)),
        }
      }
      if (isRecommendationsPage(data)) {
        return patchRecommendationsPage(data, titleId, isWatched)
      }
      return data
    },
  )
  queryClient.setQueriesData<TitleDetail>({ queryKey: ['title', titleId] }, (detail) =>
    detail && typeof detail === 'object' && 'id' in detail
      ? { ...detail, isWatched }
      : detail,
  )
}
