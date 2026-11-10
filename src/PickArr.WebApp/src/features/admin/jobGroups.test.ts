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
  it('groups catalog jobs by kind then provider/country and keeps system separate', () => {
    const grouped = groupAdminJobs([
      job({
        jobKey: 'catalog-full-2',
        displayName: 'Full catalog import (Netflix US)',
        kind: 'CatalogFull',
        providerName: 'Netflix',
        countryCode: 'US',
        sourceId: 2,
      }),
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
        jobKey: 'catalog-incr-3',
        displayName: 'Import recent titles (Apple TV+ US)',
        kind: 'CatalogIncremental',
        providerName: 'Apple TV+',
        countryCode: 'US',
        sourceId: 3,
      }),
      job({
        jobKey: 'catalog-leaving-IT',
        displayName: 'Titles leaving soon (IT)',
        kind: 'Leaving',
        countryCode: 'IT',
      }),
      job({
        jobKey: 'catalog-leaving-US',
        displayName: 'Titles leaving soon (US)',
        kind: 'Leaving',
        countryCode: 'US',
      }),
      job({
        jobKey: 'reco-build',
        displayName: 'Recommendations rebuild',
        kind: 'RecoBuild',
      }),
      job({
        jobKey: 'reco-embedding',
        displayName: 'Title embeddings',
        kind: 'RecoEmbedding',
      }),
      job({
        jobKey: 'metadata-enrichment',
        displayName: 'Metadata enrichment',
        kind: 'Enrichment',
      }),
    ])

    expect(grouped.kinds.map((g) => g.kind)).toEqual([
      'CatalogIncremental',
      'CatalogFull',
      'Leaving',
    ])
    expect(grouped.kinds[0]?.jobs.map((j) => j.jobKey)).toEqual([
      'catalog-incr-3',
      'catalog-incr-1',
      'catalog-incr-2',
    ])
    expect(grouped.kinds[1]?.jobs.map((j) => j.jobKey)).toEqual(['catalog-full-2'])
    expect(grouped.kinds[2]?.jobs.map((j) => j.jobKey)).toEqual([
      'catalog-leaving-IT',
      'catalog-leaving-US',
    ])
    expect(grouped.recommendations.map((j) => j.jobKey)).toEqual([
      'reco-embedding',
      'reco-build',
    ])
    expect(grouped.system.map((j) => j.jobKey)).toEqual(['metadata-enrichment'])
  })

  it('labels catalog rows as provider + country under kind groups', () => {
    expect(
      shortCatalogJobName(
        job({
          jobKey: 'catalog-incr-1',
          displayName: 'Import recent titles (Netflix IT)',
          kind: 'CatalogIncremental',
          providerName: 'Netflix',
          countryCode: 'IT',
        }),
      ),
    ).toBe('Netflix IT')
    expect(
      shortCatalogJobName(
        job({
          jobKey: 'catalog-leaving-US',
          displayName: 'Titles leaving soon (US)',
          kind: 'Leaving',
          countryCode: 'US',
        }),
      ),
    ).toBe('US')
  })
})
