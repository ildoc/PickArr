import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { afterEach, describe, expect, it, vi } from 'vitest'
import i18n from '../i18n'
import { BackToTop } from './BackToTop'

function wrap(ui: ReactNode) {
  return render(<I18nextProvider i18n={i18n}>{ui}</I18nextProvider>)
}

describe('BackToTop', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    Object.defineProperty(window, 'scrollY', { value: 0, configurable: true })
  })

  it('stays hidden near the top and scrolls up when shown', async () => {
    const user = userEvent.setup()
    const scrollTo = vi.fn()
    vi.stubGlobal('scrollTo', scrollTo)

    Object.defineProperty(window, 'scrollY', { value: 0, configurable: true })
    wrap(<BackToTop />)
    expect(screen.queryByRole('button', { name: 'Top' })).toBeNull()

    Object.defineProperty(window, 'scrollY', { value: 600, configurable: true })
    await act(async () => {
      window.dispatchEvent(new Event('scroll'))
    })

    const button = await screen.findByRole('button', { name: 'Top' })
    await user.click(button)
    expect(scrollTo).toHaveBeenCalledWith({ top: 0, behavior: 'smooth' })
  })
})
