import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchSettings } from '../../api/settings'
import { fetchSystemStatus } from '../../api/system'
import i18n from '../../i18n'
import { Header } from './Header'

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
}))

vi.mock('../../api/system', () => ({
  fetchSystemStatus: vi.fn(),
}))

function wrap(ui: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <MemoryRouter>{ui}</MemoryRouter>
      </I18nextProvider>
    </QueryClientProvider>,
  )
}

describe('Header', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: { enabled: true },
    } as Awaited<ReturnType<typeof fetchSettings>>)
  })

  it('shows the Docker/image version from system status with GitHub and Ko-fi links', async () => {
    vi.mocked(fetchSystemStatus).mockResolvedValue({
      version: '1.2.3.4',
      utcNow: '2026-11-09T00:00:00Z',
    })

    wrap(<Header />)

    const version = await screen.findByRole('link', { name: '1.2.3.4' })
    expect(version).toHaveAttribute('href', 'https://github.com/ildoc/PickArr')
    expect(fetchSystemStatus).toHaveBeenCalled()

    const kofi = screen.getByRole('link', { name: 'Support on Ko-fi' })
    expect(kofi).toHaveAttribute('href', 'https://ko-fi.com/pheeleeppo')
  })
})
