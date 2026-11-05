import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchGenres, fetchSources, fetchTitles, fetchCatalogTypes } from '../api/catalog'
import { fetchMyList, fetchMyListGenres, fetchMyListTypes } from '../api/favorites'
import i18n from '../i18n'
import { AppRouter } from './router'

vi.mock('../api/catalog', () => ({
  fetchSources: vi.fn(),
  fetchTitles: vi.fn(),
  fetchCatalogTypes: vi.fn(),
  fetchGenres: vi.fn(),
  fetchTitle: vi.fn(),
}))

vi.mock('../api/favorites', () => ({
  fetchMyList: vi.fn(),
  fetchMyListTypes: vi.fn(),
  fetchMyListGenres: vi.fn(),
  addFavorite: vi.fn(),
  removeFavorite: vi.fn(),
}))

const emptyPage = { page: 1, pageSize: 48, total: 0, items: [] }

function renderAt(path: string) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <MemoryRouter initialEntries={[path]}>
          <AppRouter />
        </MemoryRouter>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('catalog and my list routes', () => {
  beforeEach(() => {
    const store = new Map<string, string>()
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => {
        store.set(key, value)
      },
      removeItem: (key: string) => {
        store.delete(key)
      },
      clear: () => {
        store.clear()
      },
    })
    vi.clearAllMocks()
    vi.mocked(fetchSources).mockResolvedValue([
      {
        id: 1,
        providerKey: 'netflix',
        providerName: 'Netflix',
        countryCode: 'IT',
        metadataLanguage: 'it-IT',
        baselineCompleted: true,
        lastFullSyncAt: null,
        dataStale: false,
      },
    ])
    vi.mocked(fetchTitles).mockResolvedValue(emptyPage)
    vi.mocked(fetchCatalogTypes).mockResolvedValue([{ type: 'movie', count: 1 }])
    vi.mocked(fetchGenres).mockResolvedValue([])
    vi.mocked(fetchMyList).mockResolvedValue(emptyPage)
    vi.mocked(fetchMyListTypes).mockResolvedValue([])
    vi.mocked(fetchMyListGenres).mockResolvedValue([])
  })

  it('keeps my list off the catalog page and on its own route', async () => {
    const user = userEvent.setup()
    renderAt('/catalog')

    expect(screen.getByRole('link', { name: 'Catalog' })).toHaveAttribute('href', '/catalog')
    expect(screen.getByRole('link', { name: 'My list' })).toHaveAttribute('href', '/list')
    expect(await screen.findByLabelText('Provider')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'All' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'My list' })).toBeNull()

    await waitFor(() => expect(fetchTitles).toHaveBeenCalled())
    const catalogQuery = vi.mocked(fetchTitles).mock.calls.at(-1)?.[1]
    expect(catalogQuery).not.toHaveProperty('favorite')
    expect(catalogQuery?.view).not.toBe('favorites')

    await user.click(screen.getByRole('link', { name: 'My list' }))

    expect(await screen.findByText('My list includes every provider and country.')).toBeTruthy()
    expect(screen.queryByLabelText('Provider')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Leaving' })).toBeNull()
    await waitFor(() => expect(fetchMyList).toHaveBeenCalled())
  })

  it('sends the old catalog favorites view to the my list page', async () => {
    renderAt('/catalog?view=favorites&q=matrix&source=4')

    expect(await screen.findByText('My list includes every provider and country.')).toBeTruthy()
    expect(screen.getByPlaceholderText('Search titles…')).toHaveValue('matrix')
    expect(screen.queryByLabelText('Provider')).toBeNull()
    expect(fetchTitles).not.toHaveBeenCalled()
    await waitFor(() =>
      expect(fetchMyList).toHaveBeenCalledWith(expect.objectContaining({ q: 'matrix' })),
    )
  })
})
