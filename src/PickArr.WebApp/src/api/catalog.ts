import { apiGet, buildQuery } from './client'
import type {
  CatalogSource,
  CatalogTypeCount,
  GenreCount,
  PagedTitles,
  TitleDetail,
  TitleQueryParams,
} from './types'

export function fetchSources() {
  return apiGet<CatalogSource[]>('/api/v1/catalog/sources')
}

export function fetchTitles(sourceId: number, params: TitleQueryParams) {
  return apiGet<PagedTitles>(
    `/api/v1/catalog/sources/${sourceId}/titles${buildQuery({
      q: params.q,
      view: params.view,
      type: params.type,
      includeRemoved: params.includeRemoved,
      sort: params.sort,
      dir: params.dir,
      genre: params.genre,
      page: params.page,
      pageSize: params.pageSize ?? 48,
    })}`,
  )
}

export function fetchCatalogTypes(sourceId: number) {
  return apiGet<CatalogTypeCount[]>(`/api/v1/catalog/sources/${sourceId}/types`)
}

export function fetchGenres(sourceId: number) {
  return apiGet<GenreCount[]>(`/api/v1/catalog/sources/${sourceId}/genres`)
}

export function fetchTitle(id: number, sourceId?: number) {
  return apiGet<TitleDetail>(
    `/api/v1/titles/${id}${buildQuery({ sourceId })}`,
  )
}

export type SeriesSeason = {
  number: number
  name: string
  episodeCount: number
  airDate: string | null
  overview: string | null
}

export type SeriesEpisode = {
  number: number
  name: string
  airDate: string | null
  overview: string | null
  runtimeMinutes: number | null
}

export function fetchSeriesSeasons(titleId: number, sourceId?: number) {
  return apiGet<SeriesSeason[]>(
    `/api/v1/titles/${titleId}/seasons${buildQuery({ sourceId })}`,
  )
}

export function fetchSeasonEpisodes(titleId: number, seasonNumber: number, sourceId?: number) {
  return apiGet<SeriesEpisode[]>(
    `/api/v1/titles/${titleId}/seasons/${seasonNumber}/episodes${buildQuery({ sourceId })}`,
  )
}
