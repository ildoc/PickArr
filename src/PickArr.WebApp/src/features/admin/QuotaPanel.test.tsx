import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '../../i18n'
import { QuotaPanel } from './QuotaPanel'

vi.mock('../../api/admin', () => ({
  fetchAdminQuota: vi.fn(async () => ({
    service: 'watchmode',
    quota: 2500,
    used: 412,
    reserve: 250,
    spendable: 1838,
    projectedMonthEnd: 1980,
    lastUpdatedAt: '2026-11-04T04:05:00Z',
    periodStart: '2026-11-01T00:00:00Z',
    periodEnd: '2026-12-01T00:00:00Z',
  })),
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

describe('QuotaPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders watchmode quota figures', async () => {
    wrap(<QuotaPanel />)
    await waitFor(() => {
      expect(screen.getByText(/412 \/ 2500/)).toBeTruthy()
    })
    expect(screen.getByText('1838')).toBeTruthy()
    expect(screen.getByText('1980')).toBeTruthy()
  })
})
