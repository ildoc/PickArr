import type { AdminRun } from '../../api/admin'

/** Kind-aware one-line summary; empty string means hide the stats cell content. */
export function formatRunStats(
  run: Pick<AdminRun, 'kind' | 'creditsUsed' | 'pagesFetched' | 'titlesAdded' | 'titlesUpdated' | 'titlesRemoved'>,
  t: (key: string, options?: Record<string, unknown>) => string,
): string {
  switch (run.kind) {
    case 'CatalogIncremental':
    case 'CatalogFull':
      return t('admin.runStats.catalog', {
        credits: run.creditsUsed,
        pages: run.pagesFetched,
        added: run.titlesAdded,
        updated: run.titlesUpdated,
        removed: run.titlesRemoved,
      })
    case 'Leaving':
      return t('admin.runStats.leaving', {
        pages: run.pagesFetched,
        updated: run.titlesUpdated,
      })
    case 'Enrichment':
    case 'MetadataRefresh':
      return t('admin.runStats.metadata', {
        processed: run.pagesFetched,
        ready: run.titlesUpdated,
      })
    case 'RecoFeature':
      return t('admin.runStats.recoFeature', {
        processed: run.titlesAdded,
        ready: run.titlesUpdated,
      })
    case 'RecoEmbedding':
      return t('admin.runStats.recoEmbedding', { count: run.titlesUpdated })
    case 'RecoBuild':
      return t('admin.runStats.recoBuild', { version: run.titlesUpdated })
    case 'RecoEval':
      return t('admin.runStats.recoEval', { ratings: run.titlesUpdated })
    case 'ArrSync':
    case 'RecoModelDownload':
      return ''
    default:
      if (
        run.creditsUsed === 0
        && run.pagesFetched === 0
        && run.titlesAdded === 0
        && run.titlesUpdated === 0
        && run.titlesRemoved === 0
      ) {
        return ''
      }
      return t('admin.runStats.generic', {
        credits: run.creditsUsed,
        pages: run.pagesFetched,
        added: run.titlesAdded,
        updated: run.titlesUpdated,
      })
  }
}
