import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchSources } from '../../api/catalog'
import { exportRatings, fetchRatings, importRatings } from '../../api/ratings'
import { fetchSettings } from '../../api/settings'
import { fetchProfiles } from '../../api/profiles'
import i18n from '../../i18n'
import { ProfileProvider } from '../who/profileContext'
import { MyRatingsPage } from './MyRatingsPage'

vi.mock('../../api/catalog', () => ({
  fetchSources: vi.fn(),
  fetchTitle: vi.fn(),
}))

vi.mock('../../api/ratings', () => ({
  fetchRatings: vi.fn(),
  exportRatings: vi.fn(),
  importRatings: vi.fn(),
  setRating: vi.fn(),
  clearRating: vi.fn(),
}))

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
}))

vi.mock('../../api/profiles', () => ({
  fetchProfiles: vi.fn(),
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
]

function renderAt(path: string, seedSettings?: Awaited<ReturnType<typeof fetchSettings>>) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  if (seedSettings) {
    client.setQueryData(['settings'], seedSettings)
  }
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <ProfileProvider>
          <MemoryRouter initialEntries={[path]}>
            <Routes>
              <Route path="/ratings" element={<MyRatingsPage />} />
            </Routes>
          </MemoryRouter>
        </ProfileProvider>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('MyRatingsPage', () => {
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
    vi.mocked(fetchRatings).mockResolvedValue({
      page: 1,
      pageSize: 48,
      total: 0,
      items: [],
    })
    vi.mocked(exportRatings).mockResolvedValue([])
    vi.mocked(importRatings).mockResolvedValue({ imported: 0, unknown: 0, ignored: 0 })
  })

  it('shows the empty state and loads the liked tab by default', async () => {
    renderAt('/ratings?source=1')

    expect(await screen.findByText('No ratings in this tab yet.')).toBeTruthy()
    await waitFor(() =>
      expect(fetchRatings).toHaveBeenCalledWith(
        expect.objectContaining({ value: 'like', page: 1 }),
      ),
    )
  })

  it('reloads with value dislike when the Disliked tab is chosen', async () => {
    const user = userEvent.setup()
    renderAt('/ratings?source=1')

    await waitFor(() => expect(fetchRatings).toHaveBeenCalled())
    await user.click(screen.getByRole('button', { name: 'Disliked' }))

    await waitFor(() =>
      expect(fetchRatings).toHaveBeenCalledWith(
        expect.objectContaining({ value: 'dislike' }),
      ),
    )
  })

  it('shows the disabled message and skips ratings when reco is off', async () => {
    const disabled = {
      reco: { enabled: false },
    } as Awaited<ReturnType<typeof fetchSettings>>
    vi.mocked(fetchSettings).mockResolvedValue(disabled)

    renderAt('/ratings?source=1', disabled)

    expect(await screen.findByText('Ratings are turned off in Settings.')).toBeTruthy()
    expect(fetchRatings).not.toHaveBeenCalled()
  })
})

