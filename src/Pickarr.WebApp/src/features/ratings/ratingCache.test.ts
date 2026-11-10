import { QueryClient } from '@tanstack/react-query'
import { describe, expect, it } from 'vitest'
import type { PagedTitles, RecommendationsPage, TitleDetail, TitleListItem } from '../../api/types'
import { applyOptimisticRating, removeRatedRecommendation } from './ratingCache'

function listItem(id: number, rating: TitleListItem['rating'] = null): TitleListItem {
  return {
    id,
    name: `Title ${id}`,
    originalTitle: null,
    year: 2020,
    category: 'movie',
    posterUrl: null,
    genres: [],
    ratingTmdb: null,
    ratingImdb: null,
    isFavorite: false,
    rating,
    availability: null,
    arr: null,
    places: [],
  }
}

describe('applyOptimisticRating', () => {
  it('patches catalog titles without crashing on my-list filter metadata queries', () => {
    const client = new QueryClient()
    client.setQueryData(['my-list', 'types'], [{ type: 'movie', count: 3 }])
    client.setQueryData(['my-list', 'genres'], [{ id: 1, name: 'Drama', count: 2 }])
    client.setQueryData(
      ['titles', 7, '', 'all', '', '', 'added', 'desc'],
      {
        pages: [{ page: 1, pageSize: 48, total: 1, items: [listItem(42)] }],
        pageParams: [1],
      } satisfies { pages: PagedTitles[]; pageParams: number[] },
    )
    client.setQueryData(
      ['my-list', 'titles', '', '', '', 'added', 'desc'],
      {
        pages: [{ page: 1, pageSize: 48, total: 1, items: [listItem(42)] }],
        pageParams: [1],
      } satisfies { pages: PagedTitles[]; pageParams: number[] },
    )
    client.setQueryData(['title', 42, 7], {
      id: 42,
      name: 'Title 42',
      rating: null,
    } as TitleDetail)

    expect(() => applyOptimisticRating(client, 42, 'like')).not.toThrow()

    const titles = client.getQueryData<{ pages: PagedTitles[] }>([
      'titles',
      7,
      '',
      'all',
      '',
      '',
      'added',
      'desc',
    ])
    expect(titles?.pages[0]?.items[0]?.rating).toBe('like')

    const myList = client.getQueryData<{ pages: PagedTitles[] }>([
      'my-list',
      'titles',
      '',
      '',
      '',
      'added',
      'desc',
    ])
    expect(myList?.pages[0]?.items[0]?.rating).toBe('like')

    const detail = client.getQueryData<TitleDetail>(['title', 42, 7])
    expect(detail?.rating).toBe('like')

    expect(client.getQueryData(['my-list', 'types'])).toEqual([{ type: 'movie', count: 3 }])
    expect(client.getQueryData(['my-list', 'genres'])).toEqual([{ id: 1, name: 'Drama', count: 2 }])
  })

  it('patches flat catalog For you row recommendations without throwing', () => {
    const client = new QueryClient()
    client.setQueryData(
      ['recommendations', 'source', 7, 'catalog-row'],
      {
        version: 1,
        stage: 'cold',
        page: 1,
        pageSize: 25,
        total: 2,
        items: [listItem(42), listItem(99)],
      } satisfies RecommendationsPage,
    )
    client.setQueryData(
      ['titles', 7],
      {
        pages: [{ page: 1, pageSize: 48, total: 1, items: [listItem(42)] }],
        pageParams: [1],
      } satisfies { pages: PagedTitles[]; pageParams: number[] },
    )

    expect(() => applyOptimisticRating(client, 42, 'like')).not.toThrow()

    const row = client.getQueryData<RecommendationsPage>([
      'recommendations',
      'source',
      7,
      'catalog-row',
    ])
    expect(row?.items.map((item) => item.id)).toEqual([42, 99])
    expect(row?.items[0]?.rating).toBe('like')
    expect(row?.total).toBe(2)

    const titles = client.getQueryData<{ pages: PagedTitles[] }>(['titles', 7])
    expect(titles?.pages[0]?.items[0]?.rating).toBe('like')
  })

  it('removes a rated recommendation after the dismiss window', () => {
    const client = new QueryClient()
    client.setQueryData(
      ['recommendations', 'source', 7, 'catalog-row'],
      {
        version: 1,
        stage: 'cold',
        page: 1,
        pageSize: 25,
        total: 2,
        items: [listItem(42, 'like'), listItem(99)],
      } satisfies RecommendationsPage,
    )

    removeRatedRecommendation(client, 42)

    const row = client.getQueryData<RecommendationsPage>([
      'recommendations',
      'source',
      7,
      'catalog-row',
    ])
    expect(row?.items.map((item) => item.id)).toEqual([99])
    expect(row?.total).toBe(1)
  })
})
