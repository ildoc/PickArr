import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import type { RecoStatus } from '../../api/types'
import i18n from '../../i18n'
import { LearningProgress } from './LearningProgress'

const status: RecoStatus = {
  enabled: true,
  stage: 'cold',
  ratingCount: 4,
  effectiveN: 6,
  targetN: 30,
  personalWeight: 0,
  model: { status: 'absent', id: null, sizeBytes: null },
  embeddings: { coverage: 0, count: 0, pending: 0 },
  pool: { titles: 0 },
  lastBuiltAt: null,
  version: 1,
  lastError: null,
}

function wrap(ui: ReactNode) {
  return render(
    <I18nextProvider i18n={i18n}>
      <MemoryRouter>{ui}</MemoryRouter>
    </I18nextProvider>,
  )
}

describe('LearningProgress', () => {
  it('shows stage, progress, taste link, and bar width', () => {
    const { container } = wrap(<LearningProgress status={status} />)

    expect(screen.getByText('Cold start')).toBeTruthy()
    expect(screen.getByText('6 / 30 effective ratings')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Teach your taste' })).toHaveAttribute('href', '/taste')

    const bar = container.querySelector('[style*="width"]') as HTMLElement | null
    expect(bar?.style.width).toBe('20%')
  })

  it('hides teach your taste once effective ratings reach the target', () => {
    wrap(
      <LearningProgress
        status={{
          ...status,
          stage: 'personalized',
          effectiveN: 30,
          ratingCount: 28,
        }}
      />,
    )

    expect(screen.queryByRole('link', { name: 'Teach your taste' })).toBeNull()
    expect(screen.getByText('30 / 30 effective ratings')).toBeTruthy()
  })
})
