import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { describe, expect, it, vi } from 'vitest'
import i18n from '../../i18n'
import type { TitleDetail } from '../../api/types'
import { DeleteFromArrDialog } from './DeleteFromArrDialog'

const title = {
  id: 1,
  name: 'Example',
  arr: {
    kind: 'sonarr',
    arrId: 55,
    state: 'downloaded',
    monitored: true,
    progress: null,
    sizeOnDisk: 2048,
    path: '/tv/Example',
    fileCount: 8,
    syncedAt: null,
    seasons: null,
    canAdd: false,
    canDelete: true,
    addBlockedReason: 'alreadyInLibrary',
    externalUrl: null,
  },
} as TitleDetail

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

describe('DeleteFromArrDialog', () => {
  it('warns when delete files is selected', async () => {
    const user = userEvent.setup()
    wrap(
      <DeleteFromArrDialog title={title} open onClose={vi.fn()} onDeleted={vi.fn()} />,
    )

    expect(screen.getByText('/tv/Example')).toBeTruthy()
    expect(screen.queryByText(/permanently deletes/i)).toBeNull()

    await user.click(screen.getByLabelText(/Also delete downloaded files/i))
    expect(screen.getByText(/permanently deletes/i)).toBeTruthy()
    expect(screen.getByRole('button', { name: /Delete permanently/i })).toBeTruthy()
  })
})
