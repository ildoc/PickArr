import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchGenres, fetchSources, fetchTitles, fetchCatalogTypes } from '../../api/catalog'
import { fetchMyList, fetchMyListGenres, fetchMyListTypes } from '../../api/favorites'
import i18n from '../../i18n'
import { MyListPage } from './MyListPage'

vi.mock('../../api/catalog', () => ({
  fetchSources: vi.fn(),
  fetchTitles: vi.fn(),
  fetchCatalogTypes: vi.fn(),
  fetchGenres: vi.fn(),
  fetchTitle: vi.fn(),
}))

vi.mock('../../api/favorites', () => ({
  fetchMyList: vi.fn(),
  fetchMyListTypes: vi.fn(),
  fetchMyListGenres: vi.fn(),
  addFavorite: vi.fn(),
  removeFavorite: vi.fn(),
}))

const listItem = {
  id: 9,
  name: 'Overseas Only',
  originalTitle: 'Overseas Only',
  year: 2021,
  category: 'movie',
  posterUrl: null,
  genres: [],
  ratingTmdb: 7.2,
  isFavorite: true,
  availability: null,
  arr: { kind: 'radarr', state: 'notInLibrary' },
  places: [{ providerName: 'Netflix', countryCode: 'US' }],
}

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <MemoryRouter initialEntries={['/list']}>
          <MyListPage />
        </MemoryRouter>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('MyListPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchMyListTypes).mockResolvedValue([{ type: 'movie', count: 1 }])
    vi.mocked(fetchMyListGenres).mockResolvedValue([])
    vi.mocked(fetchMyList).mockResolvedValue({
      page: 1,
      pageSize: 48,
      total: 1,
      items: [listItem],
    })
  })

  it('loads favorites without a catalog source and shows where each title is available', async () => {
    renderPage()

    expect(await screen.findByRole('link', { name: /Overseas Only/ })).toHaveAttribute(
      'href',
      '/list?title=9',
    )
    expect(screen.getByText('Netflix · United States')).toBeTruthy()
    expect(screen.getByText('My list includes every provider and country.')).toBeTruthy()
    expect(screen.queryByLabelText('Provider')).toBeNull()
    expect(screen.queryByLabelText('Country')).toBeNull()
    expect(screen.queryByRole('button', { name: 'New' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'My list' })).toBeNull()

    await waitFor(() => expect(fetchMyList).toHaveBeenCalled())
    expect(fetchMyList).toHaveBeenCalledWith(
      expect.objectContaining({ page: 1, pageSize: 48 }),
    )
    expect(fetchTitles).not.toHaveBeenCalled()
    expect(fetchSources).not.toHaveBeenCalled()
    expect(fetchCatalogTypes).not.toHaveBeenCalled()
    expect(fetchGenres).not.toHaveBeenCalled()
  })
})
