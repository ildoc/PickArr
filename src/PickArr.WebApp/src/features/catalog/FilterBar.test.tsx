import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nextProvider } from 'react-i18next'
import { describe, expect, it, vi } from 'vitest'
import type { CatalogSource } from '../../api/types'
import i18n from '../../i18n'
import { FilterBar } from './FilterBar'

const sources: CatalogSource[] = [
  {
    id: 1,
    providerKey: 'netflix',
    providerName: 'Netflix',
    countryCode: 'IT',
    metadataLanguage: 'it-IT',
    baselineCompleted: false,
    lastFullSyncAt: null,
    dataStale: false,
  },
  {
    id: 2,
    providerKey: 'netflix',
    providerName: 'Netflix',
    countryCode: 'US',
    metadataLanguage: 'en-US',
    baselineCompleted: false,
    lastFullSyncAt: null,
    dataStale: false,
  },
]

describe('FilterBar', () => {
  it('filters the catalog by country', async () => {
    const user = userEvent.setup()
    const onCountryChange = vi.fn()
    render(
      <I18nextProvider i18n={i18n}>
        <FilterBar
          q=""
          onQChange={() => undefined}
          view="all"
          onViewChange={() => undefined}
          sort="added"
          onSortChange={() => undefined}
          total={0}
          sources={sources}
          countrySourceId={1}
          onCountryChange={onCountryChange}
          type=""
          onTypeChange={() => undefined}
          types={[
            { type: 'movie', count: 1 },
            { type: 'series', count: 1 },
            { type: 'standup', count: 1 },
          ]}
          genre=""
          genres={[{ id: 4, name: 'Comedy', count: 2 }]}
          onGenreChange={() => undefined}
          dir="desc"
          onDirChange={() => undefined}
        />
      </I18nextProvider>,
    )

    expect(screen.getByLabelText('Provider')).toBeTruthy()
    expect(screen.getByRole('option', { name: 'Netflix' })).toBeTruthy()
    const country = screen.getByLabelText('Country')
    expect(screen.getByRole('option', { name: 'Italy' })).toBeTruthy()
    expect(screen.getByRole('option', { name: 'United States' })).toBeTruthy()
    expect(screen.getByRole('option', { name: 'Stand-up' })).toBeTruthy()
    expect(screen.getByRole('option', { name: 'Comedy' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Sort direction' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'All' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'My list' })).toBeNull()
    await user.selectOptions(country, '2')
    expect(onCountryChange).toHaveBeenCalledWith(2)
  })

  it('offers All provider and country when allowAllSources is set', async () => {
    const user = userEvent.setup()
    const onCountryChange = vi.fn()
    render(
      <I18nextProvider i18n={i18n}>
        <FilterBar
          q=""
          onQChange={() => undefined}
          view="all"
          onViewChange={() => undefined}
          sort="added"
          onSortChange={() => undefined}
          total={0}
          sources={sources}
          countrySourceId={null}
          onCountryChange={onCountryChange}
          type=""
          onTypeChange={() => undefined}
          types={[]}
          genre=""
          genres={[]}
          onGenreChange={() => undefined}
          dir="desc"
          onDirChange={() => undefined}
          hideViews
          hideSearch
          hideSort
          allowAllSources
        />
      </I18nextProvider>,
    )

    const provider = screen.getByLabelText('Provider')
    expect(provider).toHaveValue('')
    expect(screen.getByLabelText('Country')).toBeDisabled()
    await user.selectOptions(provider, 'netflix')
    expect(onCountryChange).toHaveBeenCalledWith(1)
  })
})
