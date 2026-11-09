import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PagedTitles, TitleListItem } from '../../api/types'
import { clearRating, setRating } from '../../api/ratings'
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
    ratingImdb: null,
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
})
