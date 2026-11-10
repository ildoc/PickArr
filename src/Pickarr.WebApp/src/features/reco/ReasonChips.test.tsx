import { render, screen } from '@testing-library/react'
import { I18nextProvider } from 'react-i18next'
import { describe, expect, it } from 'vitest'
import i18n from '../../i18n'
import { ReasonChips } from './ReasonChips'

describe('ReasonChips', () => {
  it('renders similarTo, terms, and popular labels', () => {
    render(
      <I18nextProvider i18n={i18n}>
        <ReasonChips
          reasons={[
            { kind: 'similarTo', titleId: 1, name: 'Dark', similarity: 0.8 },
            { kind: 'terms', terms: ['Crime', 'Mystery'] },
            { kind: 'popular' },
          ]}
        />
      </I18nextProvider>,
    )

    expect(screen.getByText('Like Dark')).toBeTruthy()
    expect(screen.getByText('Crime, Mystery')).toBeTruthy()
    expect(screen.getByText('Popular')).toBeTruthy()
  })
})
