import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchArrOptions, postArrTest } from '../../api/arr'
import { fetchSettings } from '../../api/settings'
import i18n from '../../i18n'
import { SettingsPage } from './SettingsPage'

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
  updateSettings: vi.fn(),
}))

vi.mock('../../api/arr', () => ({
  fetchArrOptions: vi.fn(),
  postArrTest: vi.fn(),
}))

const emptySettings = {
  watchmode: { apiKey: { isSet: false }, plan: 'free', quota: 2500, reservePercent: 10, maxCreditsPerRun: 80 },
  tmdb: { apiKey: { isSet: false } },
  omdb: { apiKey: { isSet: false } },
  sonarr: { url: null, apiKey: { isSet: false }, syncIntervalMinutes: 15, defaults: null },
  radarr: { url: null, apiKey: { isSet: false }, syncIntervalMinutes: 15, defaults: null },
  webhooks: { token: { isSet: false } },
  ui: { language: 'en' },
  reco: {
    enabled: true,
    useImplicit: true,
    embeddingsEnabled: true,
    excludeInLibrary: true,
    n0: 30,
    weightLove: 1.5,
    weightLike: 1,
    weightDislike: -1,
    weightNotInterested: -0.75,
    weightFavorite: 0.5,
    weightArrAdd: 0.6,
    weightDownloaded: 0.4,
    weightWatched: 0,
    mmrLambda: 0.7,
    exploreRate: 0.1,
    mlMinRatings: 100,
    eventsEnabled: false,
  },
}

function wrap(ui: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>{ui}</I18nextProvider>
    </QueryClientProvider>,
  )
}

describe('SettingsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchSettings).mockResolvedValue(emptySettings)
    vi.mocked(fetchArrOptions).mockResolvedValue({
      kind: 'sonarr',
      configured: false,
      reachable: false,
      qualityProfiles: [],
      rootFolders: [],
      tags: [],
      defaults: null,
    })
  })

  it('fills quality profiles and root folders only after a successful connection test', async () => {
    const user = userEvent.setup()
    vi.mocked(postArrTest).mockResolvedValue({
      ok: true,
      version: '4.0.14',
      appName: 'Sonarr',
      qualityProfiles: [
        { id: 1, name: 'Any' },
        { id: 4, name: 'HD-1080p' },
      ],
      rootFolders: [{ path: '/tv', freeSpace: 10 }],
    })

    wrap(<SettingsPage />)

    await screen.findByRole('heading', { name: 'Sonarr' })
    expect(screen.getAllByText('These lists fill in when Test connection succeeds.').length).toBeGreaterThan(0)
    expect(screen.queryByRole('option', { name: 'HD-1080p' })).toBeNull()
    expect(fetchArrOptions).not.toHaveBeenCalled()

    const urlInputs = screen.getAllByRole('textbox')
    await user.type(urlInputs[0]!, 'http://sonarr.local')
    const keyInputs = document.querySelectorAll('input[type="password"]')
    await user.type(keyInputs[0] as HTMLInputElement, 'secret-key')
    expect(screen.queryByRole('option', { name: 'HD-1080p' })).toBeNull()

    await user.click(screen.getAllByRole('button', { name: 'Test connection' })[0]!)

    await waitFor(() => {
      expect(screen.getByRole('option', { name: 'HD-1080p' })).toBeTruthy()
    })
    expect(screen.getByRole('option', { name: '/tv' })).toBeTruthy()
    expect(screen.getAllByText(/Loaded from the last successful connection test/).length).toBeGreaterThan(0)
    expect(postArrTest).toHaveBeenCalledWith({
      kind: 'sonarr',
      url: 'http://sonarr.local',
      apiKey: 'secret-key',
    })
    expect(fetchArrOptions).not.toHaveBeenCalled()
  })

  it('keeps recommendation enable on connections and leaves taste signals to the profile', async () => {
    wrap(<SettingsPage />)
    expect(await screen.findByRole('heading', { name: 'Recommendations' })).toBeTruthy()
    expect(screen.getByLabelText(/Enable recommendations and ratings/i)).toBeChecked()
    expect(screen.queryByLabelText(/Use My list and library as temporary taste signals/i)).toBeNull()
  })

  it('keeps the lists empty when the connection test fails', async () => {
    const user = userEvent.setup()
    vi.mocked(postArrTest).mockResolvedValue({
      ok: false,
      error: 'unauthorized',
      qualityProfiles: [],
      rootFolders: [],
    })

    wrap(<SettingsPage />)
    await screen.findByRole('heading', { name: 'Radarr' })

    const urlInputs = screen.getAllByRole('textbox')
    await user.type(urlInputs[1]!, 'http://radarr.local')
    const keys = document.querySelectorAll('input[type="password"]')
    await user.type(keys[1] as HTMLInputElement, 'bad-key')
    await user.click(screen.getAllByRole('button', { name: 'Test connection' })[1]!)

    expect(await screen.findByText(/Connection failed: unauthorized/)).toBeTruthy()
    expect(screen.queryByRole('option', { name: 'HD-1080p' })).toBeNull()
  })
})
