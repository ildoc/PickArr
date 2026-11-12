import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchTitle } from '../../api/catalog'
import { setRating } from '../../api/ratings'
import { fetchSettings } from '../../api/settings'
import type { TitleDetail } from '../../api/types'
import { addWatched } from '../../api/watched'
import i18n from '../../i18n'
import { TitleDialog } from './TitleDialog'

vi.mock('../../api/catalog', () => ({
  fetchTitle: vi.fn(),
}))

vi.mock('../../api/ratings', () => ({
  setRating: vi.fn(async () => undefined),
  clearRating: vi.fn(async () => undefined),
}))

vi.mock('../../api/watched', () => ({
  addWatched: vi.fn(async () => undefined),
  removeWatched: vi.fn(async () => undefined),
}))

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
}))

vi.mock('../reco/SimilarRow', () => ({
  SimilarRow: () => null,
}))

function renderDialog() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <MemoryRouter initialEntries={['/catalog?title=42']}>
          <Routes>
            <Route path="/catalog" element={<TitleDialog titleId={42} sourceId={1} />} />
          </Routes>
        </MemoryRouter>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('TitleDialog ratings', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    HTMLDialogElement.prototype.showModal = vi.fn(function showModal(this: HTMLDialogElement) {
      this.open = true
    })
    HTMLDialogElement.prototype.close = vi.fn(function close(this: HTMLDialogElement) {
      this.open = false
      this.dispatchEvent(new Event('close'))
    })
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: { enabled: true },
    } as Awaited<ReturnType<typeof fetchSettings>>)
    vi.mocked(fetchTitle).mockResolvedValue({
      id: 42,
      name: 'Dialog Movie',
      originalTitle: 'Dialog Movie',
      year: 2021,
      category: 'movie',
      overview: null,
      runtimeMinutes: null,
      seasonCount: null,
      episodeCount: null,
      genres: [],
      posterUrl: null,
      backdropUrl: null,
      ratings: { tmdb: null, tmdbVotes: null, imdb: null },
      links: { tmdb: null, imdb: null, tvdb: null },
      isFavorite: false,
      isWatched: false,
      rating: null,
      metadataStatus: 'ready',
      availability: [],
      arr: {
        kind: 'radarr',
        arrId: null,
        state: 'notInLibrary',
        canAdd: false,
        canDelete: false,
        addBlockedReason: 'notConfigured',
        monitored: null,
        progress: null,
        sizeOnDisk: null,
        path: null,
        fileCount: null,
        syncedAt: null,
        seasons: null,
        externalUrl: null,
      },
    } as TitleDetail)
  })

  it('hides the library section when Arr is not configured', async () => {
    renderDialog()

    expect(await screen.findByRole('heading', { name: 'Dialog Movie' })).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Library' })).toBeNull()
    expect(screen.queryByText(/Arr instance not configured/i)).toBeNull()
  })

  it('lets the user like a title from the detail dialog', async () => {
    const user = userEvent.setup()
    renderDialog()

    expect(await screen.findByRole('heading', { name: 'Dialog Movie' })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Like' }))

    await waitFor(() => expect(setRating).toHaveBeenCalledWith(42, 'like'))
  })

  it('lets the user mark a title as watched from the detail dialog', async () => {
    const user = userEvent.setup()
    renderDialog()

    expect(await screen.findByRole('button', { name: 'Hide' })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Hide' }))

    await waitFor(() => expect(addWatched).toHaveBeenCalledWith(42))
  })
})
