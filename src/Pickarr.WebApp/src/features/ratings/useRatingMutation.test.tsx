import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { PagedTitles, RecommendationsPage, TitleListItem } from '../../api/types'
import { clearRating, setRating } from '../../api/ratings'
import { RECO_DISMISS_TOTAL_MS } from './ratingCache'
import { useRatingMutation } from './useRatingMutation'

vi.mock('../../api/ratings', () => ({
  setRating: vi.fn(async () => undefined),
  clearRating: vi.fn(async () => undefined),
}))

function listItem(id: number): TitleListItem {
  return {
    id,
    name: `Title ${id}`,
    originalTitle: null,
    year: 2020,
    category: 'movie',
    posterUrl: null,
    genres: [],
    ratingTmdb: null,
    isFavorite: false,
    rating: null,
    availability: null,
    arr: null,
    places: [],
  }
}

describe('useRatingMutation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('persists like even when my-list types/genres queries are cached', async () => {
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })
    client.setQueryData(['my-list', 'types'], [{ type: 'movie', count: 1 }])
    client.setQueryData(['my-list', 'genres'], [{ id: 1, name: 'Drama', count: 1 }])
    client.setQueryData(
      ['titles', 1],
      {
        pages: [{ page: 1, pageSize: 48, total: 1, items: [listItem(9)] }],
        pageParams: [1],
      } satisfies { pages: PagedTitles[]; pageParams: number[] },
    )

    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    )

    const { result } = renderHook(() => useRatingMutation(9), { wrapper })
    result.current.mutate('like')

    await waitFor(() => expect(setRating).toHaveBeenCalledWith(9, 'like'))
    expect(clearRating).not.toHaveBeenCalled()

    const titles = client.getQueryData<{ pages: PagedTitles[] }>(['titles', 1])
    expect(titles?.pages[0]?.items[0]?.rating).toBe('like')
  })

  it('keeps a For you card until the dismiss window elapses', async () => {
    vi.useFakeTimers()
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })
    client.setQueryData(
      ['recommendations', 'global'],
      {
        version: 1,
        stage: 'cold',
        page: 1,
        pageSize: 48,
        total: 1,
        items: [listItem(9)],
      } satisfies RecommendationsPage,
    )

    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    )

    const { result } = renderHook(() => useRatingMutation(9), { wrapper })
    act(() => {
      result.current.mutate('like')
    })

    await act(async () => {
      await Promise.resolve()
    })

    expect(client.getQueryData<RecommendationsPage>(['recommendations', 'global'])?.items).toHaveLength(
      1,
    )
    expect(
      client.getQueryData<RecommendationsPage>(['recommendations', 'global'])?.items[0]?.rating,
    ).toBe('like')

    act(() => {
      vi.advanceTimersByTime(RECO_DISMISS_TOTAL_MS)
    })

    expect(
      client.getQueryData<RecommendationsPage>(['recommendations', 'global'])?.items.map((i) => i.id),
    ).toEqual([])
  })
})
