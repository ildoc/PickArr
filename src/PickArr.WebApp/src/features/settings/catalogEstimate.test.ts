import { describe, expect, it } from 'vitest'
import { estimateCatalog } from './catalogEstimate'

const base = {
  used: 0,
  quota: 2500,
  freePlan: true,
  freeCountryLimit: 3,
  creditsPerCall: 1,
  fullSyncsPerMonth: 4,
  dailyRunsPerMonth: 30,
}

describe('estimateCatalog', () => {
  it('counts a fill and a month for each new country', () => {
    const result = estimateCatalog({
      ...base,
      countries: [{ code: 'US', selected: true, pages: 32, incrementalPages: 3, fullyImported: false }],
    })

    expect(result.countries).toBe(1)
    expect(result.catalogs).toBe(1)
    expect(result.fill).toBe(32)
    expect(result.monthly).toBe(3 * 30 + 32 * 4 + 30)
    expect(result.overQuota).toBe(false)
    expect(result.overCountryLimit).toBe(false)
  })

  it('ignores countries that are not selected and flags a fourth country', () => {
    const country = (code: string, selected = true) => ({
      code,
      selected,
      pages: 32,
      incrementalPages: 3,
      fullyImported: true,
    })
    const result = estimateCatalog({
      ...base,
      countries: [country('US'), country('IT'), country('GB'), country('DE'), country('FR', false)],
    })

    expect(result.countries).toBe(4)
    expect(result.catalogs).toBe(4)
    expect(result.fill).toBe(0)
    expect(result.overCountryLimit).toBe(true)
  })

  it('counts several providers in one country as a single country', () => {
    const catalog = { code: 'US', selected: true, pages: 32, incrementalPages: 3, fullyImported: false }
    const result = estimateCatalog({
      ...base,
      countries: [catalog, catalog, catalog, catalog],
    })

    expect(result.countries).toBe(1)
    expect(result.catalogs).toBe(4)
    expect(result.fill).toBe(128)
    expect(result.overCountryLimit).toBe(false)
  })

  it('flags a selection that would pass the monthly quota', () => {
    const result = estimateCatalog({
      ...base,
      used: 2400,
      countries: [{ code: 'US', selected: true, pages: 400, incrementalPages: 3, fullyImported: false }],
    })

    expect(result.overQuota).toBe(true)
  })
})
