import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchOnboarding, postOnboardingRatings } from '../../api/recommendations'
import { fetchSettings } from '../../api/settings'
import i18n from '../../i18n'
import { TastePage } from './TastePage'

vi.mock('../../api/recommendations', () => ({
  fetchOnboarding: vi.fn(),
  postOnboardingRatings: vi.fn(),
}))

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
}))

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <MemoryRouter initialEntries={['/taste']}>
          <Routes>
            <Route path="/taste" element={<TastePage />} />
            <Route path="/for-you" element={<p>For you</p>} />
          </Routes>
        </MemoryRouter>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('TastePage', () => {
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
    vi.mocked(fetchOnboarding).mockResolvedValue({
      version: 0,
      stage: 'cold',
      page: 1,
      pageSize: 24,
      total: 1,
      items: [
        {
          id: 42,
          name: 'Onboarding Movie',
          originalTitle: 'Onboarding Movie',
          year: 2020,
          category: 'movie',
          posterUrl: null,
          genres: [],
          ratingTmdb: null,
          ratingImdb: null,
          isFavorite: false,
          rating: null,
          availability: null,
          arr: null,
          places: [],
          reco: { score: 0, personalized: false, reasons: [], similarity: null },
        },
      ],
    })
    vi.mocked(postOnboardingRatings).mockResolvedValue(undefined)
  })

  it('loads onboarding titles and submits selected votes', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByRole('heading', { name: 'Teach your taste' })).toBeTruthy()
    expect(await screen.findByText('Onboarding Movie')).toBeTruthy()

    await user.click(screen.getByRole('button', { name: 'Like' }))
    await user.click(screen.getByRole('button', { name: 'Save ratings' }))

    await waitFor(() =>
      expect(postOnboardingRatings).toHaveBeenCalledWith([{ titleId: 42, value: 'like' }]),
    )
    expect(await screen.findByText('For you')).toBeTruthy()
  })
})
