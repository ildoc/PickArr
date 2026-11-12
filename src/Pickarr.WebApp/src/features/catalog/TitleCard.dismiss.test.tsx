import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen } from '@testing-library/react'
import { useState, type ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { TitleListItem } from '../../api/types'
import i18n from '../../i18n'
import { RECO_DISMISS_HOLD_MS } from '../ratings/ratingCache'
import { TitleCard } from './TitleCard'

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(async () => ({ reco: { enabled: true } })),
}))

function item(rating: TitleListItem['rating'] = null): TitleListItem {
  return {
    id: 7,
    name: 'Fade Movie',
    originalTitle: null,
    year: 2024,
    category: 'movie',
    posterUrl: null,
    genres: [],
    ratingTmdb: null,
    isFavorite: false,
    rating,
    availability: null,
    arr: null,
    places: [],
  }
}

function Harness() {
  const [title, setTitle] = useState(item(null))
  return (
    <div>
      <button type="button" onClick={() => setTitle(item('like'))}>
        rate
      </button>
      <TitleCard title={title} dismissOnRate />
    </div>
  )
}

function renderCard() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <MemoryRouter>
          <Harness />
        </MemoryRouter>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('TitleCard dismissOnRate', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('keeps the card visible briefly after a vote then starts fading', () => {
    const { container } = renderCard()
    const card = () => container.querySelector('a')!

    expect(card().className).toContain('opacity-100')

    act(() => {
      screen.getByRole('button', { name: 'rate' }).click()
    })
    expect(card().className).toContain('opacity-100')
    expect(card().getAttribute('aria-hidden')).toBeNull()

    act(() => {
      vi.advanceTimersByTime(RECO_DISMISS_HOLD_MS)
    })

    expect(card().className).toContain('opacity-0')
    expect(card().getAttribute('aria-hidden')).toBe('true')
  })
})

