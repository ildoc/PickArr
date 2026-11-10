import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchSeasonEpisodes, fetchSeriesSeasons } from '../../api/catalog'
import i18n from '../../i18n'
import { SeriesSeasons } from './SeriesSeasons'

vi.mock('../../api/catalog', () => ({
  fetchSeriesSeasons: vi.fn(),
  fetchSeasonEpisodes: vi.fn(),
}))

function wrap(ui: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>{ui}</I18nextProvider>
    </QueryClientProvider>,
  )
}

describe('SeriesSeasons', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchSeriesSeasons).mockResolvedValue([
      { number: 2, name: 'Season 2', episodeCount: 2, airDate: '2024-01-01', overview: null },
      { number: 1, name: 'Season 1', episodeCount: 2, airDate: '2020-01-01', overview: null },
    ])
    vi.mocked(fetchSeasonEpisodes).mockResolvedValue([
      { number: 1, name: 'Earlier', airDate: null, overview: null, runtimeMinutes: null },
      { number: 3, name: 'Later', airDate: null, overview: null, runtimeMinutes: null },
    ])
  })

  it('renders seasons newest first and loads episodes oldest first', async () => {
    const user = userEvent.setup()
    wrap(<SeriesSeasons titleId={9} sourceId={1} />)

    const season2 = await screen.findByRole('button', { name: /Season 2/i })
    const season1 = screen.getByRole('button', { name: /Season 1/i })
    expect(season2.compareDocumentPosition(season1) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()

    await user.click(season2)
    const earlier = await screen.findByText('Earlier')
    const later = screen.getByText('Later')
    expect(earlier.compareDocumentPosition(later) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    await waitFor(() =>
      expect(fetchSeasonEpisodes).toHaveBeenCalledWith(9, 2, 1),
    )
  })
})
