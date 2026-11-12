import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchRecommendations } from '../../api/recommendations'
import { fetchSettings } from '../../api/settings'
import i18n from '../../i18n'
import { CatalogForYouRow } from './CatalogForYouRow'

vi.mock('../../api/recommendations', () => ({
  fetchRecommendations: vi.fn(),
}))

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
}))

function renderRow() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <MemoryRouter>
          <CatalogForYouRow sourceId={7} />
        </MemoryRouter>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('CatalogForYouRow', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: { enabled: true },
    } as Awaited<ReturnType<typeof fetchSettings>>)
  })

  it('loads up to 25 recommendations for the catalog source', async () => {
    vi.mocked(fetchRecommendations).mockResolvedValue({
      version: 1,
      stage: 'cold',
      page: 1,
      pageSize: 25,
      total: 1,
      items: [
        {
          id: 11,
          name: 'Suggested Title',
          originalTitle: null,
          year: 2024,
          category: 'movie',
          posterUrl: null,
          genres: [],
          ratingTmdb: null,
          isFavorite: false,
          rating: null,
          availability: null,
          arr: null,
        },
      ],
    })

    renderRow()

    await waitFor(() =>
      expect(fetchRecommendations).toHaveBeenCalledWith(
        expect.objectContaining({
          scope: 'source',
          sourceId: 7,
          page: 1,
          pageSize: 25,
        }),
      ),
    )
    expect(await screen.findByRole('heading', { name: 'For you' })).toBeTruthy()
    expect(screen.getAllByText('Suggested Title').length).toBeGreaterThan(0)
    expect(screen.getByRole('link', { name: 'All sources' })).toHaveAttribute('href', '/for-you')
  })

  it('renders nothing when recommendations are disabled', async () => {
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: { enabled: false },
    } as Awaited<ReturnType<typeof fetchSettings>>)

    const { container } = renderRow()
    await waitFor(() => expect(fetchSettings).toHaveBeenCalled())
    expect(fetchRecommendations).not.toHaveBeenCalled()
    expect(container).toBeEmptyDOMElement()
  })
})
