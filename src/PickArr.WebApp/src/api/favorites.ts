import { apiDelete, apiGet, apiPut, buildQuery } from './client'
import type { CatalogTypeCount, GenreCount, PagedTitles, TitleQueryParams } from './types'

export function fetchMyList(params: TitleQueryParams) {
  return apiGet<PagedTitles>(
    `/api/v1/favorites${buildQuery({
      q: params.q,
      type: params.type,
      genre: params.genre,
      sort: params.sort,
      dir: params.dir,
      page: params.page,
      pageSize: params.pageSize ?? 48,
    })}`,
  )
}

export function fetchMyListTypes() {
  return apiGet<CatalogTypeCount[]>('/api/v1/favorites/types')
}

export function fetchMyListGenres() {
  return apiGet<GenreCount[]>('/api/v1/favorites/genres')
}

export function addFavorite(titleId: number) {
  return apiPut(`/api/v1/favorites/${titleId}`)
}

export function removeFavorite(titleId: number) {
  return apiDelete(`/api/v1/favorites/${titleId}`)
}
