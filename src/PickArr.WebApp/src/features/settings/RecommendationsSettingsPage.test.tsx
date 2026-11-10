import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  fetchRecoStatus,
  postRecoModelDetect,
  postRecoRebuild,
  postRecoReset,
} from '../../api/recommendations'
import { fetchSettings, updateSettings } from '../../api/settings'
import i18n from '../../i18n'
import { RecommendationsSettingsPage } from './RecommendationsSettingsPage'

vi.mock('../../api/recommendations', () => ({
  fetchRecoStatus: vi.fn(),
  postRecoRebuild: vi.fn(),
  postRecoEvaluate: vi.fn(),
  postRecoEmbeddingsRebuild: vi.fn(),
  postRecoModelDownload: vi.fn(),
  postRecoModelDetect: vi.fn(),
  postRecoReset: vi.fn(),
}))

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
  updateSettings: vi.fn(),
}))

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

describe('RecommendationsSettingsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.stubGlobal('confirm', vi.fn(() => true))
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: {
        enabled: true,
        useImplicit: true,
        embeddingsEnabled: true,
        exploreRate: 0.1,
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
        eventsEnabled: false,
        mlMinRatings: 100,
      },
    } as Awaited<ReturnType<typeof fetchSettings>>)
    vi.mocked(fetchRecoStatus).mockResolvedValue({
      enabled: true,
      stage: 'warming',
      ratingCount: 12,
      effectiveN: 8,
      targetN: 30,
      personalWeight: 0.2,
      model: { status: 'Ready', id: 'all-MiniLM-L6-v2@q8', sizeBytes: 23000000 },
      embeddings: { coverage: 0.9, count: 100, pending: 0 },
      pool: { titles: 120 },
      lastBuiltAt: null,
      version: 3,
      lastError: null,
    })
    vi.mocked(updateSettings).mockResolvedValue(undefined as never)
    vi.mocked(postRecoRebuild).mockResolvedValue({ hangfireJobId: 'job-1' })
    vi.mocked(postRecoModelDetect).mockResolvedValue({ hangfireJobId: 'job-2' })
    vi.mocked(postRecoReset).mockResolvedValue({ includeRatings: false })
  })

  it('shows model status and queues admin actions', async () => {
    const user = userEvent.setup()
    wrap(<RecommendationsSettingsPage />)

    expect(await screen.findByRole('heading', { name: 'Recommendations' })).toBeTruthy()
    expect(await screen.findByText(/Model: Ready/)).toBeTruthy()
    expect(screen.getByText(/Embedding coverage: 90%/)).toBeTruthy()

    await user.click(screen.getByRole('button', { name: 'Rebuild recommendations' }))
    await waitFor(() => expect(postRecoRebuild).toHaveBeenCalled())
    expect(await screen.findByText(/Queued: rebuild/)).toBeTruthy()

    await user.click(screen.getByRole('button', { name: 'Detect model files' }))
    await waitFor(() => expect(postRecoModelDetect).toHaveBeenCalled())

    await user.click(screen.getByRole('button', { name: 'Reset derived data' }))
    await waitFor(() => expect(postRecoReset).toHaveBeenCalledWith(false))
  })

  it('saves model and explore toggles', async () => {
    const user = userEvent.setup()
    wrap(<RecommendationsSettingsPage />)

    const modelToggle = await screen.findByRole('checkbox', { name: /Use the local embedding model/i })
    const exploreToggle = screen.getByRole('checkbox', { name: /Explore unfamiliar titles/i })
    await user.click(modelToggle)
    await user.click(exploreToggle)
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(updateSettings).toHaveBeenCalledWith(
        expect.objectContaining({
          reco: expect.objectContaining({
            embeddingsEnabled: false,
            exploreRate: 0,
          }),
        }),
      ),
    )
    const saved = vi.mocked(updateSettings).mock.calls[0]?.[0] as { reco: Record<string, unknown> }
    expect(saved.reco).not.toHaveProperty('useImplicit')
    expect(saved.reco).not.toHaveProperty('excludeInLibrary')
  })
})
