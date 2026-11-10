import type { InfiniteData, QueryClient } from '@tanstack/react-query'
import type { PagedTitles, RecommendationsPage, TitleDetail, TitleListItem } from '../../api/types'

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

function patchFavorite(
  items: TitleListItem[],
  titleId: number,
  isFavorite: boolean,
): TitleListItem[] {
  return items.map((item) => (item.id === titleId ? { ...item, isFavorite } : item))
}

export function applyOptimisticFavorite(
  queryClient: QueryClient,
  titleId: number,
  isFavorite: boolean,
) {
  queryClient.setQueriesData<InfiniteData<PagedTitles>>({ queryKey: ['titles'] }, (data) => {
    if (!isPagedTitles(data)) return data
    return {
      ...data,
      pages: data.pages.map((page) => ({
        ...page,
        items: patchFavorite(page.items, titleId, isFavorite),
      })),
    }
  })
  queryClient.setQueriesData<InfiniteData<PagedTitles>>({ queryKey: ['my-list', 'titles'] }, (data) => {
    if (!isPagedTitles(data)) return data
    return {
      ...data,
      pages: data.pages.map((page) => ({
        ...page,
        items: patchFavorite(page.items, titleId, isFavorite),
      })),
    }
  })
  queryClient.setQueriesData<InfiniteData<RecommendationsPage> | RecommendationsPage>(
    { queryKey: ['recommendations'] },
    (data) => {
      if (isRecommendationsInfinite(data)) {
        return {
          ...data,
          pages: data.pages.map((page) => ({
            ...page,
            items: patchFavorite(page.items, titleId, isFavorite),
          })),
        }
      }
      if (isRecommendationsPage(data)) {
        return { ...data, items: patchFavorite(data.items, titleId, isFavorite) }
      }
      return data
    },
  )
  queryClient.setQueriesData<TitleDetail>({ queryKey: ['title', titleId] }, (detail) =>
    detail && typeof detail === 'object' && 'id' in detail ? { ...detail, isFavorite } : detail,
  )
}
