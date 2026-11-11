import { describe, expect, it } from 'vitest'
import { formatRunStats } from './runStats'

const t = (key: string, options?: Record<string, unknown>) =>
  `${key}:${JSON.stringify(options ?? {})}`

describe('formatRunStats', () => {
  it('uses catalog wording for catalog syncs', () => {
    const text = formatRunStats(
      {
        kind: 'CatalogFull',
        creditsUsed: 3,
        pagesFetched: 2,
        titlesAdded: 1,
        titlesUpdated: 4,
        titlesRemoved: 5,
      },
      t,
    )
    expect(text).toContain('admin.runStats.catalog')
    expect(text).toContain('"removed":5')
  })

  it('hides stats for Arr sync', () => {
    expect(
      formatRunStats(
        {
          kind: 'ArrSync',
          creditsUsed: 0,
          pagesFetched: 0,
          titlesAdded: 0,
          titlesUpdated: 0,
          titlesRemoved: 0,
        },
        t,
      ),
    ).toBe('')
  })

  it('describes enrichment with processed/ready', () => {
    const text = formatRunStats(
      {
        kind: 'Enrichment',
        creditsUsed: 0,
        pagesFetched: 10,
        titlesAdded: 0,
        titlesUpdated: 8,
        titlesRemoved: 0,
      },
      t,
    )
    expect(text).toContain('admin.runStats.metadata')
    expect(text).toContain('"processed":10')
  })
})
