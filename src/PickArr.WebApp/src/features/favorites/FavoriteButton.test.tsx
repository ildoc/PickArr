import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '../../i18n'
import { FavoriteButton } from './FavoriteButton'

vi.mock('../../api/favorites', () => ({
  addFavorite: vi.fn(async () => undefined),
  removeFavorite: vi.fn(async () => undefined),
}))

import { addFavorite, removeFavorite } from '../../api/favorites'

function renderButton(isFavorite: boolean) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <FavoriteButton titleId={7} isFavorite={isFavorite} />
      </I18nextProvider>
    </QueryClientProvider>,
  )
}

describe('FavoriteButton', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('adds favorite when inactive', async () => {
    const user = userEvent.setup()
    renderButton(false)
    await user.click(screen.getByRole('button', { name: /add to my list/i }))
    expect(addFavorite).toHaveBeenCalledWith(7)
  })

  it('removes favorite when active', async () => {
    const user = userEvent.setup()
    renderButton(true)
    await user.click(screen.getByRole('button', { name: /remove from my list/i }))
    expect(removeFavorite).toHaveBeenCalledWith(7)
  })
})
