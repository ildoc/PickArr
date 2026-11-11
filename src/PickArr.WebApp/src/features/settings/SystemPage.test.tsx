import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchSystemLogs, fetchSystemStats, fetchSystemStatus } from '../../api/system'
import i18n from '../../i18n'
import { SystemPage } from './SystemPage'

vi.mock('../../api/system', () => ({
  fetchSystemStatus: vi.fn(),
  fetchSystemStats: vi.fn(),
  fetchSystemLogs: vi.fn(),
  clearSystemLogs: vi.fn(),
  downloadSystemLog: vi.fn(),
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

describe('SystemPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchSystemStatus).mockResolvedValue({
      version: '1.2.3',
      utcNow: '2026-11-09T15:00:00Z',
    })
    vi.mocked(fetchSystemLogs).mockResolvedValue({
      items: [{ fileName: 'pickarr-20261111.log', bytes: 128, lastWriteUtc: '2026-11-11T10:00:00Z' }],
    })
  })

  it('shows title totals, provider/country breakdown, and storage', async () => {
    vi.mocked(fetchSystemStats).mockResolvedValue({
      totalTitles: 42,
      byProvider: [{ name: 'Netflix', titleCount: 40 }],
      byCountry: [{ name: 'IT', titleCount: 25 }],
      bySource: [{ providerName: 'Netflix', countryCode: 'IT', titleCount: 25 }],
      storage: {
        totalBytes: 2048,
        entries: [
          { key: 'database', relativePath: 'Pickarr.db', bytes: 1536 },
          { key: 'logs', relativePath: 'logs', bytes: 512 },
        ],
      },
    })

    wrap(<SystemPage />)

    expect(await screen.findByText('42 titles in the database')).toBeTruthy()
    expect(screen.getAllByText('Netflix').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Italy').length).toBeGreaterThan(0)
    expect(screen.getByText('Catalog database')).toBeTruthy()
    expect(screen.getByText('Pickarr.db')).toBeTruthy()
    expect(screen.getByText('1.2.3')).toBeTruthy()
    expect(await screen.findByText('pickarr-20261111.log')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Clear all logs' })).toBeTruthy()
  })
})
