import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { describe, expect, it } from 'vitest'
import i18n from '../../i18n'
import { ArrStatusBadge } from './ArrStatusBadge'

function wrap(ui: ReactNode) {
  return render(<I18nextProvider i18n={i18n}>{ui}</I18nextProvider>)
}

describe('ArrStatusBadge', () => {
  it('renders translated library state', () => {
    wrap(<ArrStatusBadge state="downloaded" />)
    expect(screen.getByText('Downloaded')).toBeTruthy()
  })

  it('shows download progress', () => {
    wrap(<ArrStatusBadge state="downloading" progress={42.5} />)
    expect(screen.getByText(/Downloading/)).toBeTruthy()
    expect(screen.getByText(/43%/)).toBeTruthy()
  })
})
