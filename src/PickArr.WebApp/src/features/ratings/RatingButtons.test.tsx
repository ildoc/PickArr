import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '../../i18n'
import { RatingButtons } from './RatingButtons'

vi.mock('../../api/ratings', () => ({
  setRating: vi.fn(async () => undefined),
  clearRating: vi.fn(async () => undefined),
}))

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(async () => ({
    reco: { enabled: true },
  })),
}))

import { clearRating, setRating } from '../../api/ratings'
import { fetchSettings } from '../../api/settings'

function renderButtons(rating: 'love' | 'like' | 'dislike' | null) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <RatingButtons titleId={7} rating={rating} />
      </I18nextProvider>
    </QueryClientProvider>,
  )
}

describe('RatingButtons', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: { enabled: true },
    } as Awaited<ReturnType<typeof fetchSettings>>)
  })

  it('sets love when inactive', async () => {
    const user = userEvent.setup()
    renderButtons(null)
    await user.click(await screen.findByRole('button', { name: 'Love' }))
    expect(setRating).toHaveBeenCalledWith(7, 'love')
  })

  it('sets like when inactive', async () => {
    const user = userEvent.setup()
    renderButtons(null)
    await user.click(await screen.findByRole('button', { name: 'Like' }))
    expect(setRating).toHaveBeenCalledWith(7, 'like')
  })

  it('clears like on second click', async () => {
    const user = userEvent.setup()
    renderButtons('like')
    await user.click(await screen.findByRole('button', { name: 'Like' }))
    expect(clearRating).toHaveBeenCalledWith(7)
  })

  it('switches from like to dislike', async () => {
    const user = userEvent.setup()
    renderButtons('like')
    await user.click(await screen.findByRole('button', { name: 'Dislike' }))
    expect(setRating).toHaveBeenCalledWith(7, 'dislike')
  })

  it('hides when recommendations are disabled', async () => {
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: { enabled: false },
    } as Awaited<ReturnType<typeof fetchSettings>>)
    renderButtons(null)
    await waitFor(() => {
      expect(screen.queryByRole('button', { name: 'Like' })).toBeNull()
    })
  })
})
