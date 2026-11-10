import { describe, expect, it } from 'vitest'
import type { AdminJob } from '../../api/admin'
import { groupAdminJobs, shortCatalogJobName } from './jobGroups'

function job(partial: Partial<AdminJob> & Pick<AdminJob, 'jobKey' | 'displayName' | 'kind'>): AdminJob {
  return {
    sourceId: null,
    countryCode: null,
    providerName: null,
    cron: null,
    lastExecution: null,
    nextExecution: null,
    lastOutcome: null,
    lastRunId: null,
    canTrigger: true,
    executionState: 'Idle',
    lastError: null,
    description: '',
    ...partial,
  }
}

describe('groupAdminJobs', () => {
  it('groups catalog jobs by provider then country and keeps system separate', () => {
    const grouped = groupAdminJobs([
      job({
        jobKey: 'catalog-incr-2',
        displayName: 'Import recent titles (Netflix US)',
        kind: 'CatalogIncremental',
        providerName: 'Netflix',
        countryCode: 'US',
        sourceId: 2,
        executionState: 'Idle',
      }),
      job({
        jobKey: 'catalog-incr-1',
        displayName: 'Import recent titles (Netflix IT)',
        kind: 'CatalogIncremental',
        providerName: 'Netflix',
        countryCode: 'IT',
        sourceId: 1,
        executionState: 'Running',
      }),
      job({
        jobKey: 'catalog-leaving-IT',
        displayName: 'Titles leaving soon (IT)',
        kind: 'Leaving',
        countryCode: 'IT',
      }),
      job({
        jobKey: 'metadata-enrichment',
        displayName: 'Metadata enrichment',
        kind: 'Enrichment',
      }),
    ])

    expect(grouped.providers).toHaveLength(1)
    expect(grouped.providers[0]?.providerName).toBe('Netflix')
    expect(grouped.providers[0]?.countries.map((c) => c.countryCode)).toEqual(['IT', 'US'])
    expect(grouped.providers[0]?.countries[0]?.jobs[0]?.jobKey).toBe('catalog-incr-1')
    expect(grouped.leaving.map((j) => j.jobKey)).toEqual(['catalog-leaving-IT'])
    expect(grouped.system.map((j) => j.jobKey)).toEqual(['metadata-enrichment'])
  })

  it('shortens catalog job names under provider groups', () => {
    expect(
      shortCatalogJobName(
        job({
          jobKey: 'catalog-incr-1',
          displayName: 'Import recent titles (Netflix IT)',
          kind: 'CatalogIncremental',
        }),
      ),
    ).toBe('Import recent titles')
    expect(
      shortCatalogJobName(
        job({
          jobKey: 'catalog-full-1',
          displayName: 'Full catalog import (Netflix IT)',
          kind: 'CatalogFull',
        }),
      ),
    ).toBe('Full catalog import')
  })
})
