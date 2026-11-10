import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchCatalogTypes, fetchGenres, fetchSources, fetchTitles } from '../../api/catalog'
import { fetchProfiles } from '../../api/profiles'
import i18n from '../../i18n'
import { ProfileProvider } from '../who/profileContext'
import { CatalogPage } from './CatalogPage'

vi.mock('../../api/catalog', () => ({
  fetchSources: vi.fn(),
  fetchTitles: vi.fn(),
  fetchCatalogTypes: vi.fn(),
  fetchGenres: vi.fn(),
  fetchTitle: vi.fn(),
}))

vi.mock('../../api/profiles', () => ({
  fetchProfiles: vi.fn(),
}))

vi.mock('../reco/CatalogForYouRow', () => ({
  CatalogForYouRow: () => null,
}))

vi.mock('../title/TitleDialog', () => ({
  TitleDialog: () => null,
}))

const sources = [
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
  {
    id: 2,
    providerKey: 'netflix',
    providerName: 'Netflix',
    countryCode: 'US',
    metadataLanguage: 'en-US',
    baselineCompleted: true,
    lastFullSyncAt: null,
    dataStale: false,
  },
]

function renderAt(path: string) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <ProfileProvider>
          <MemoryRouter initialEntries={[path]}>
            <Routes>
              <Route path="/catalog" element={<CatalogPage />} />
            </Routes>
          </MemoryRouter>
        </ProfileProvider>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('CatalogPage', () => {
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
    vi.mocked(fetchSources).mockResolvedValue(sources)
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
    vi.mocked(fetchCatalogTypes).mockResolvedValue([{ type: 'movie', count: 1 }])
    vi.mocked(fetchGenres).mockResolvedValue([])
    vi.mocked(fetchTitles).mockResolvedValue({
      page: 1,
      pageSize: 48,
      total: 0,
      items: [],
    })
  })

  it('shows the Arr empty state and never sends favorite on title queries', async () => {
    vi.mocked(fetchTitles).mockResolvedValue({
      page: 1,
      pageSize: 48,
      total: 0,
      items: [],
      emptyReason: 'arrNotConfigured',
    })

    renderAt('/catalog?source=1')

    expect(
      await screen.findByText(/Configure Sonarr and\/or Radarr in Settings/i),
    ).toBeTruthy()
    expect(screen.getAllByRole('link', { name: 'Settings' }).length).toBeGreaterThan(0)

    await waitFor(() => expect(fetchTitles).toHaveBeenCalled())
    const args = vi.mocked(fetchTitles).mock.calls.at(-1)
    expect(args?.[0]).toBe(1)
    expect(args?.[1]).not.toHaveProperty('favorite')
  })

  it('updates the titles request view when a quick filter is chosen', async () => {
    const user = userEvent.setup()
    renderAt('/catalog?source=1')

    await waitFor(() => expect(fetchTitles).toHaveBeenCalled())
    await user.click(screen.getByRole('button', { name: 'Leaving' }))

    await waitFor(() =>
      expect(fetchTitles).toHaveBeenCalledWith(
        1,
        expect.objectContaining({ view: 'leaving' }),
      ),
    )
  })

  it('uses the preferred source from localStorage when the URL has none', async () => {
    localStorage.setItem('pickarr.sourceId', '2')
    renderAt('/catalog')

    await waitFor(() =>
      expect(fetchTitles).toHaveBeenCalledWith(2, expect.objectContaining({ page: 1 })),
    )
  })
})
