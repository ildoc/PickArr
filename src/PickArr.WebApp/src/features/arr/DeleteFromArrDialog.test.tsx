import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import i18n from '../../i18n'
import type { TitleDetail } from '../../api/types'
import { DeleteFromArrDialog } from './DeleteFromArrDialog'

vi.mock('../../api/arr', () => ({
  deleteTitleArr: vi.fn(),
}))

import { deleteTitleArr } from '../../api/arr'

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
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(deleteTitleArr).mockResolvedValue(title.arr)
  })

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

  it('submits delete with the selected flags and notifies the parent', async () => {
    const user = userEvent.setup()
    const onClose = vi.fn()
    const onDeleted = vi.fn()
    wrap(<DeleteFromArrDialog title={title} open onClose={onClose} onDeleted={onDeleted} />)

    await user.click(screen.getByLabelText(/Also delete downloaded files/i))
    await user.click(screen.getByLabelText(/Add to import exclusion list/i))
    await user.click(screen.getByRole('button', { name: /Delete permanently/i }))

    await waitFor(() => {
      expect(deleteTitleArr).toHaveBeenCalledWith(1, true, true)
    })
    expect(onDeleted).toHaveBeenCalled()
    expect(onClose).toHaveBeenCalled()
  })

  it('shows the API error when delete fails', async () => {
    const user = userEvent.setup()
    vi.mocked(deleteTitleArr).mockRejectedValue(new ApiError('ARR_GONE', 404))
    wrap(<DeleteFromArrDialog title={title} open onClose={vi.fn()} onDeleted={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: /^Remove$/i }))

    expect(await screen.findByText('ARR_GONE')).toBeTruthy()
  })
})

