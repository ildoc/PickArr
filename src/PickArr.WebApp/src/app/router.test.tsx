import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchGenres, fetchSources, fetchTitles, fetchCatalogTypes } from '../api/catalog'
import { fetchMyList, fetchMyListGenres, fetchMyListTypes } from '../api/favorites'
import { fetchProfiles } from '../api/profiles'
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

vi.mock('../api/profiles', () => ({
  fetchProfiles: vi.fn(),
  createProfile: vi.fn(),
  updateProfile: vi.fn(),
  deleteProfile: vi.fn(),
}))

vi.mock('../api/settings', () => ({
  fetchSettings: vi.fn(async () => ({
    watchmode: { apiKey: { isSet: false } },
    tmdb: { apiKey: { isSet: false } },
    sonarr: { url: null, apiKey: { isSet: false }, defaults: null },
    radarr: { url: null, apiKey: { isSet: false }, defaults: null },
    reco: { enabled: true },
  })),
}))

vi.mock('../api/recommendations', () => ({
  fetchRecommendations: vi.fn(async () => ({
    version: 1,
    stage: 'cold',
    page: 1,
    pageSize: 48,
    total: 0,
    items: [],
  })),
  fetchRecoStatus: vi.fn(async () => ({
    enabled: true,
    stage: 'cold',
    ratingCount: 0,
    effectiveN: 0,
    targetN: 30,
    personalWeight: 0,
    model: { status: 'absent', id: null, sizeBytes: null },
    embeddings: { coverage: 0, count: 0, pending: 0 },
    pool: { titles: 0 },
    lastBuiltAt: null,
    version: 1,
    lastError: null,
  })),
}))

vi.mock('../api/ratings', () => ({
  fetchRatings: vi.fn(async () => ({ page: 1, pageSize: 48, total: 0, items: [] })),
  setRating: vi.fn(),
  clearRating: vi.fn(),
  exportRatings: vi.fn(),
  importRatings: vi.fn(),
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
    vi.mocked(fetchProfiles).mockResolvedValue([
      {
        id: 1,
        name: 'Admin',
        color: 'blue',
        isAdmin: true,
        useImplicit: true,
        excludeInLibrary: true,
        createdAt: '2026-11-10T00:00:00Z',
      },
    ])
  })

  it('keeps my list off the catalog page and on its own route', async () => {
    const user = userEvent.setup()
    renderAt('/catalog')

    expect(screen.getByRole('link', { name: 'Catalog' })).toHaveAttribute('href', '/catalog')
    expect(screen.getByRole('link', { name: 'For you' })).toHaveAttribute('href', '/for-you')
    expect(screen.getByRole('link', { name: 'My list' })).toHaveAttribute('href', '/list')
    expect(screen.getByRole('link', { name: 'My ratings' })).toHaveAttribute('href', '/ratings')
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

  it('opens the catalog for one profile and remembers it', async () => {
    renderAt('/')

    expect(await screen.findByLabelText('Provider')).toBeTruthy()
    expect(localStorage.getItem('pickarr.profileId')).toBe('1')
  })

  it('opens the catalog when the remembered profile still exists', async () => {
    vi.mocked(fetchProfiles).mockResolvedValue([
      profile(1, 'Admin', true),
      profile(2, 'Kid', false),
    ])
    localStorage.setItem('pickarr.profileId', '2')

    renderAt('/')

    expect(await screen.findByLabelText('Provider')).toBeTruthy()
    expect(screen.queryByRole('heading', { name: "Who's watching?" })).toBeNull()
  })

  it('opens the profile picker when the remembered profile was deleted', async () => {
    vi.mocked(fetchProfiles).mockResolvedValue([
      profile(1, 'Admin', true),
      profile(2, 'Kid', false),
    ])
    localStorage.setItem('pickarr.profileId', '9')

    renderAt('/')

    expect(await screen.findByRole('heading', { name: "Who's watching?" })).toBeTruthy()
    expect(screen.queryByLabelText('Provider')).toBeNull()
  })

  it('opens the profile picker from the header even when a profile is remembered', async () => {
    const user = userEvent.setup()
    renderAt('/catalog')

    await user.click(await screen.findByRole('link', { name: 'Switch profile, Admin' }))

    expect(await screen.findByRole('heading', { name: "Who's watching?" })).toBeTruthy()
  })

  it('sends a non-admin profile away from settings', async () => {
    vi.mocked(fetchProfiles).mockResolvedValue([
      profile(1, 'Admin', true),
      profile(2, 'Kid', false),
    ])
    localStorage.setItem('pickarr.profileId', '2')

    renderAt('/settings/jobs')

    expect(await screen.findByLabelText('Provider')).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Settings' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Settings' })).toBeNull()
  })

  it('keeps settings reachable for the admin profile', async () => {
    renderAt('/settings')

    expect(await screen.findByRole('heading', { name: 'Connections' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Settings' })).toHaveAttribute('href', '/settings')
  })
})

function profile(id: number, name: string, isAdmin: boolean) {
  return {
    id,
    name,
    color: 'blue',
    isAdmin,
    useImplicit: true,
    excludeInLibrary: true,
    createdAt: '2026-11-10T00:00:00Z',
  }
}
