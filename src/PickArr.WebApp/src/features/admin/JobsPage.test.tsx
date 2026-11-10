import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchAdminJobs, fetchAdminQuota, fetchAdminRuns, triggerAdminJob } from '../../api/admin'
import i18n from '../../i18n'
import { JobsPage } from './JobsPage'

vi.mock('../../api/admin', () => ({
  fetchAdminJobs: vi.fn(),
  fetchAdminRuns: vi.fn(),
  fetchAdminQuota: vi.fn(),
  triggerAdminJob: vi.fn(),
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

describe('JobsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchAdminRuns).mockResolvedValue({ page: 1, pageSize: 20, total: 1, items: [] })
    vi.mocked(fetchAdminQuota).mockResolvedValue({
      service: 'watchmode',
      quota: 2500,
      used: 1,
      reserve: 250,
      spendable: 2000,
      projectedMonthEnd: null,
      lastUpdatedAt: null,
      periodStart: '2026-11-01T00:00:00Z',
      periodEnd: '2026-12-01T00:00:00Z',
    })
  })

  it('shows queued, running, and failure logs with plain job names', async () => {
    const user = userEvent.setup()
    vi.mocked(fetchAdminJobs).mockResolvedValue([
      {
        jobKey: 'catalog-incr-1',
        displayName: 'Import recent titles (Netflix IT)',
        description: 'Downloads the newest pages of the Netflix catalog for Italy.',
        kind: 'CatalogIncremental',
        sourceId: 1,
        countryCode: 'IT',
        providerName: 'Netflix',
        cron: '0 4 * * *',
        lastExecution: null,
        nextExecution: null,
        lastOutcome: null,
        lastRunId: null,
        canTrigger: true,
        executionState: 'Running',
        lastError: null,
      },
      {
        jobKey: 'catalog-bootstrap',
        displayName: 'Initial catalog import',
        description: 'Turns on the Italy and United States catalogs and queues their first import.',
        kind: 'CatalogBootstrap',
        sourceId: null,
        countryCode: null,
        providerName: null,
        cron: null,
        lastExecution: null,
        nextExecution: null,
        lastOutcome: null,
        lastRunId: null,
        canTrigger: true,
        executionState: 'Queued',
        lastError: null,
      },
      {
        jobKey: 'catalog-resume',
        displayName: 'Resume paused imports',
        description:
          'Re-queues catalog imports that stopped because the Watchmode credit budget ran out. It does not download titles by itself.',
        kind: 'CatalogResume',
        sourceId: null,
        countryCode: null,
        providerName: null,
        cron: '0 0 * * *',
        lastExecution: null,
        nextExecution: null,
        lastOutcome: null,
        lastRunId: null,
        canTrigger: true,
        executionState: 'Failed',
        lastError: 'Watchmode monthly quota exceeded.',
      },
    ])
    vi.mocked(fetchAdminRuns).mockResolvedValue({
      page: 1,
      pageSize: 20,
      total: 1,
      items: [
        {
          id: 'run-1',
          kind: 'CatalogIncremental',
          sourceId: 1,
          countryCode: 'IT',
          triggeredBy: 'Manual',
          status: 'Failed',
          startedAt: '2026-11-05T10:00:00Z',
          finishedAt: '2026-11-05T10:01:00Z',
          creditsUsed: 1,
          pagesFetched: 1,
          titlesAdded: 0,
          titlesUpdated: 0,
          titlesRemoved: 0,
          errorMessage: 'provider timeout',
        },
      ],
    })

    wrap(<JobsPage />)

    expect(await screen.findByText('1 running · 1 queued · 1 failed')).toBeTruthy()
    expect(screen.getByRole('heading', { level: 3, name: 'Import recent titles' })).toBeTruthy()
    expect(screen.getByText('Downloads the newest pages of the catalogs.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Netflix IT' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 3, name: 'System' })).toBeTruthy()
    expect(screen.getAllByText('Initial catalog import').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Resume paused imports').length).toBeGreaterThan(0)
    expect(screen.getByText(/does not download titles by itself/)).toBeTruthy()
    expect(screen.getAllByText('Running').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Queued').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Failed').length).toBeGreaterThan(0)
    expect(screen.getByText('Watchmode monthly quota exceeded.')).toBeTruthy()
    expect(await screen.findByText('Recent titles import')).toBeTruthy()
    expect(screen.getByText('provider timeout')).toBeTruthy()
    await waitFor(() => {
      expect(fetchAdminRuns).toHaveBeenCalledWith(
        expect.objectContaining({ jobKey: 'catalog-incr-1' }),
      )
    })

    vi.mocked(triggerAdminJob).mockResolvedValue({ hangfireJobId: 'hf-1', runId: 'run-9' })
    const runButtons = screen.getAllByRole('button', { name: 'Run now' })
    await user.click(runButtons[0]!)

    await waitFor(() => {
      expect(triggerAdminJob).toHaveBeenCalledWith('catalog-incr-1', {
        sourceId: 1,
        mode: 'incremental',
      })
    })
    expect(await screen.findByText(/Job started — run run-9/)).toBeTruthy()
  })
})


