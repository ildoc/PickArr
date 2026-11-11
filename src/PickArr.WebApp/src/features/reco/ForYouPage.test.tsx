import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  fetchCatalogTypes,
  fetchEnabledCatalogGenres,
  fetchEnabledCatalogTypes,
  fetchGenres,
  fetchSources,
} from '../../api/catalog'
import { fetchRecommendations, fetchRecoStatus } from '../../api/recommendations'
import { fetchSettings } from '../../api/settings'
import { fetchProfiles } from '../../api/profiles'
import i18n from '../../i18n'
import { ProfileProvider } from '../who/profileContext'
import { ForYouPage } from './ForYouPage'

vi.mock('../../api/catalog', () => ({
  fetchSources: vi.fn(),
  fetchCatalogTypes: vi.fn(),
  fetchEnabledCatalogTypes: vi.fn(),
  fetchGenres: vi.fn(),
  fetchEnabledCatalogGenres: vi.fn(),
}))

vi.mock('../../api/recommendations', () => ({
  fetchRecommendations: vi.fn(),
  fetchRecoStatus: vi.fn(),
}))

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
}))

vi.mock('../../api/profiles', () => ({
  fetchProfiles: vi.fn(),
}))

const emptyPage = { version: 1, stage: 'cold', page: 1, pageSize: 48, total: 0, items: [] }

function renderPage(path: string) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <ProfileProvider>
          <MemoryRouter initialEntries={[path]}>
            <Routes>
              <Route path="/for-you" element={<ForYouPage />} />
            </Routes>
          </MemoryRouter>
        </ProfileProvider>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('ForYouPage', () => {
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
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: { enabled: true },
    } as Awaited<ReturnType<typeof fetchSettings>>)
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
      {
        id: 2,
        providerKey: 'prime',
        providerName: 'Prime Video',
        countryCode: 'IT',
        metadataLanguage: 'it-IT',
        baselineCompleted: true,
        lastFullSyncAt: null,
        dataStale: false,
      },
    ])
    vi.mocked(fetchCatalogTypes).mockResolvedValue([{ type: 'movie', count: 1 }])
    vi.mocked(fetchEnabledCatalogTypes).mockResolvedValue([
      { type: 'movie', count: 2 },
      { type: 'series', count: 1 },
    ])
    vi.mocked(fetchGenres).mockResolvedValue([])
    vi.mocked(fetchEnabledCatalogGenres).mockResolvedValue([
      { id: 18, name: 'Drama', count: 3 },
    ])
    vi.mocked(fetchRecommendations).mockResolvedValue(emptyPage)
    vi.mocked(fetchRecoStatus).mockResolvedValue({
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
    })
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

  it('defaults to All sources with scope enabled', async () => {
    renderPage('/for-you')
    expect(
      await screen.findByText(/Across every enabled catalog/i),
    ).toBeTruthy()
    expect(await screen.findByLabelText('Provider')).toHaveValue('')
    await waitFor(() =>
      expect(fetchRecommendations).toHaveBeenCalledWith(
        expect.objectContaining({ scope: 'enabled', includeInLibrary: false }),
      ),
    )
    const lastCall = vi.mocked(fetchRecommendations).mock.calls.at(-1)?.[0]
    expect(lastCall?.sourceId).toBeUndefined()
    expect(localStorage.getItem('pickarr.sourceId.1')).toBeNull()
    await waitFor(() => expect(fetchEnabledCatalogTypes).toHaveBeenCalled())
    expect(fetchEnabledCatalogGenres).toHaveBeenCalled()
    expect(fetchCatalogTypes).not.toHaveBeenCalled()
  })

  it('filters by type and genre while All sources is selected', async () => {
    const user = userEvent.setup()
    renderPage('/for-you')
    await screen.findByLabelText('Type')
    await waitFor(() => expect(fetchEnabledCatalogTypes).toHaveBeenCalled())
    await user.selectOptions(screen.getByLabelText('Type'), 'series')
    await user.selectOptions(screen.getByLabelText('Genre'), '18')
    await waitFor(() =>
      expect(fetchRecommendations).toHaveBeenCalledWith(
        expect.objectContaining({
          scope: 'enabled',
          type: 'series',
          genre: '18',
        }),
      ),
    )
    expect(fetchCatalogTypes).not.toHaveBeenCalled()
    expect(fetchGenres).not.toHaveBeenCalled()
  })

  it('switches to scope source immediately when a provider is chosen', async () => {
    const user = userEvent.setup()
    renderPage('/for-you')
    await screen.findByLabelText('Provider')
    await user.selectOptions(screen.getByLabelText('Provider'), 'prime')
    await waitFor(() =>
      expect(fetchRecommendations).toHaveBeenCalledWith(
        expect.objectContaining({ scope: 'source', sourceId: 2 }),
      ),
    )
    expect(localStorage.getItem('pickarr.sourceId.1')).toBe('2')
    expect(
      await screen.findByText(/Showing recommendations available on the selected provider/i),
    ).toBeTruthy()
  })

  it('honors an explicit source query', async () => {
    renderPage('/for-you?source=2')
    await waitFor(() =>
      expect(fetchRecommendations).toHaveBeenCalledWith(
        expect.objectContaining({ scope: 'source', sourceId: 2 }),
      ),
    )
    expect(localStorage.getItem('pickarr.sourceId.1')).toBe('2')
  })

  it('can include library titles', async () => {
    const user = userEvent.setup()
    renderPage('/for-you')
    await user.click(await screen.findByRole('checkbox', { name: /Include titles already in the library/i }))
    await waitFor(() =>
      expect(fetchRecommendations).toHaveBeenCalledWith(
        expect.objectContaining({ includeInLibrary: true }),
      ),
    )
  })
})
