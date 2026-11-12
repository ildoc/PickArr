import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { addFavorite } from '../../api/favorites'
import type { RecommendationsPage, TitleListItem } from '../../api/types'
import i18n from '../../i18n'
import { FavoriteButton } from './FavoriteButton'

vi.mock('../../api/favorites', () => ({
  addFavorite: vi.fn(async () => undefined),
  removeFavorite: vi.fn(async () => undefined),
}))

function listItem(id: number, isFavorite: boolean): TitleListItem {
  return {
    id,
    name: `Title ${id}`,
    originalTitle: null,
    year: 2020,
    category: 'movie',
    posterUrl: null,
    genres: [],
    ratingTmdb: null,
    isFavorite,
    rating: null,
    availability: null,
    arr: null,
    places: [],
  }
}

describe('FavoriteButton', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('optimistically colors the heart on For you recommendations', async () => {
    const user = userEvent.setup()
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })
    client.setQueryData(
      ['recommendations', 'source', 1, 'catalog-row'],
      {
        version: 1,
        stage: 'cold',
        page: 1,
        pageSize: 25,
        total: 1,
        items: [listItem(9, false)],
      } satisfies RecommendationsPage,
    )

    render(
      <QueryClientProvider client={client}>
        <I18nextProvider i18n={i18n}>
          <FavoriteButton titleId={9} isFavorite={false} />
        </I18nextProvider>
      </QueryClientProvider>,
    )

    await user.click(screen.getByRole('button', { name: /Add to My list/i }))

    await waitFor(() => expect(addFavorite).toHaveBeenCalledWith(9))
    const row = client.getQueryData<RecommendationsPage>([
      'recommendations',
      'source',
      1,
      'catalog-row',
    ])
    expect(row?.items[0]?.isFavorite).toBe(true)
  })
})
