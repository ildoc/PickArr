import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchCatalogSetup } from '../../api/catalogSetup'
import i18n from '../../i18n'
import { CatalogSourcesPage } from './CatalogSourcesPage'

vi.mock('../../api/catalogSetup', () => ({
  fetchCatalogSetup: vi.fn(),
  updateCatalogSetup: vi.fn(),
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

describe('CatalogSourcesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows a load error when the catalog list cannot be fetched', async () => {
    vi.mocked(fetchCatalogSetup).mockRejectedValue(new Error('network'))
    wrap(<CatalogSourcesPage />)
    expect(await screen.findByText('Could not load the catalog selection.')).toBeTruthy()
  })
})
