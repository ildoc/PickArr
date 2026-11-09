export type CatalogSource = {
  id: number
  providerKey: string
  providerName: string
  countryCode: string
  metadataLanguage: string
  baselineCompleted: boolean
  lastFullSyncAt: string | null
  dataStale: boolean
}

export type GenreRef = { id: number; name: string }

export type AvailabilitySummary = {
  addedAt: string | null
  removedAt: string | null
  leavingDate: string | null
  isNew: boolean
}

export type ArrSummary = {
  kind: string
  state: string
  monitored?: boolean | null
  progress?: number | null
}

export type RecoReason = {
  kind: string
  titleId?: number | null
  name?: string | null
  similarity?: number | null
  terms?: string[] | null
}

export type RecoItemScore = {
  score: number
  personalized: boolean
  reasons: RecoReason[]
  similarity?: number | null
}

export type SimilarTitlesPage = {
  items: TitleListItem[]
}

export type TitleListItem = {
  id: number
  name: string
  originalTitle: string | null
  year: number | null
  category: string
  posterUrl: string | null
  genres: GenreRef[]
  ratingTmdb: number | null
  ratingImdb: number | null
  isFavorite: boolean
  rating?: 'like' | 'dislike' | null
  availability: AvailabilitySummary | null
  arr: ArrSummary | null
  places?: Array<{ providerName: string; countryCode: string; removedAt?: string | null }>
  reco?: RecoItemScore
  /** Client-only badge for My ratings vs selected catalog source. */
  sourceStatus?: 'present' | 'removed' | 'absent'
}

export type RecommendationsPage = {
  version: number
  stage: string
  page: number
  pageSize: number
  total: number
  items: TitleListItem[]
}

export type RecoStatus = {
  enabled: boolean
  stage: string
  ratingCount: number
  effectiveN: number
  targetN: number
  personalWeight: number
  model: { status: string; id: string | null; sizeBytes: number | null }
  embeddings: { coverage: number; count: number; pending: number }
  pool: { titles: number }
  lastBuiltAt: string | null
  version: number
  lastError: string | null
}

export type ArrFilterInfo = {
  sonarrConfigured: boolean
  radarrConfigured: boolean
  allowedMediaTypes: string[]
}

export type PagedTitles = {
  page: number
  pageSize: number
  total: number
  items: TitleListItem[]
  emptyReason?: string | null
  arrFilter?: ArrFilterInfo | null
}

export type TitleDetail = {
  id: number
  name: string
  originalTitle: string | null
  year: number | null
  category: string
  overview: string | null
  runtimeMinutes: number | null
  seasonCount: number | null
  episodeCount: number | null
  genres: GenreRef[]
  posterUrl: string | null
  backdropUrl: string | null
  ratings: { tmdb: number | null; tmdbVotes: number | null; imdb: number | null }
  links: { tmdb: string | null; imdb: string | null; tvdb: string | null }
  isFavorite: boolean
  rating?: 'like' | 'dislike' | null
  metadataStatus: string
  availability: Array<{
    sourceId: number
    countryCode: string
    addedAt: string | null
    removedAt: string | null
    leavingDate: string | null
  }>
  cast?: string[]
  arr: {
    kind: string | null
    arrId: number | null
    state: string
    monitored: boolean | null
    progress: number | null
    sizeOnDisk: number | null
    path: string | null
    fileCount: number | null
    syncedAt: string | null
    seasons: Array<{
      number: number
      monitored: boolean
      state: string
      episodeFileCount: number
      episodeCount: number
      totalEpisodeCount: number
    }> | null
    canAdd: boolean
    canDelete: boolean
    addBlockedReason: string | null
    externalUrl: string | null
  }
}

export type SecretStatus = { isSet: boolean }

export type SettingsResponse = {
  watchmode: {
    apiKey: SecretStatus
    plan: string
    quota: number
    reservePercent: number
    maxCreditsPerRun: number
  }
  tmdb: { apiKey: SecretStatus }
  omdb: { apiKey: SecretStatus }
  sonarr: {
    url: string | null
    apiKey: SecretStatus
    syncIntervalMinutes: number
    defaults?: { qualityProfileId: number | null; rootFolderPath: string | null } | null
  }
  radarr: {
    url: string | null
    apiKey: SecretStatus
    syncIntervalMinutes: number
    defaults?: { qualityProfileId: number | null; rootFolderPath: string | null } | null
  }
  webhooks: { token: SecretStatus }
  ui: { language: string }
  reco: {
    enabled: boolean
    useImplicit: boolean
    embeddingsEnabled: boolean
    excludeInLibrary: boolean
    n0: number
    weightLike: number
    weightDislike: number
    weightFavorite: number
    weightArrAdd: number
    weightDownloaded: number
    mmrLambda: number
    exploreRate: number
    mlMinRatings: number
    eventsEnabled: boolean
  }
}

export type RecoEvalLevelMetrics = {
  auc: number
  hitAt10: number
  folds: number
}

export type RecoEvalReport = {
  evaluatedAt: string
  ratingCount: number
  likeCount: number
  dislikeCount: number
  poolSize: number
  lowSampleWarning: boolean
  prior: RecoEvalLevelMetrics | null
  level1: RecoEvalLevelMetrics | null
  level2: RecoEvalLevelMetrics | null
  combined: RecoEvalLevelMetrics | null
  level3: RecoEvalLevelMetrics | null
  likeVsDislikeAucCombined: number | null
  level3Active: boolean
  level3SkipReason: string | null
}

export type TitleQueryParams = {
  q?: string
  view?: string
  type?: string
  includeRemoved?: boolean
  sort?: string
  dir?: string
  genre?: string
  page?: number
  pageSize?: number
}

export type CatalogTypeCount = { type: string; count: number }

export type GenreCount = { id: number; name: string; count: number }
