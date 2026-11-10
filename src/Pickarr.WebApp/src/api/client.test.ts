import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchTitle, fetchTitles } from './catalog'
import { addFavorite } from './favorites'
import {
  ApiError,
  apiDelete,
  apiGet,
  apiPost,
  apiPut,
  buildQuery,
  clearProfileIdCache,
  profileHeaderName,
  profileStorageKey,
} from './client'

const profileStore = new Map<string, string>()

const memoryStorage = {
  getItem: (key: string) => profileStore.get(key) ?? null,
  setItem: (key: string, value: string) => {
    profileStore.set(key, value)
  },
  removeItem: (key: string) => {
    profileStore.delete(key)
  },
  clear: () => {
    profileStore.clear()
  },
}

beforeEach(() => {
  profileStore.clear()
  vi.stubGlobal('localStorage', memoryStorage)
})

afterEach(() => {
  profileStore.clear()
  clearProfileIdCache()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('api client', () => {
  it('throws ApiError with status when GET is not ok', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('nope', { status: 503 })),
    )

    await expect(apiGet('/api/v1/x')).rejects.toMatchObject({
      name: 'ApiError',
      status: 503,
      message: 'GET /api/v1/x failed',
    })
  })

  it('returns void for PUT 204 without parsing JSON', async () => {
    const json = vi.fn()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        ok: true,
        status: 204,
        json,
      })),
    )

    await expect(apiPut('/api/v1/favorites/1')).resolves.toBeUndefined()
    expect(json).not.toHaveBeenCalled()
  })

  it('returns JSON for successful POST', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        new Response(JSON.stringify({ id: 7 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    )

    await expect(apiPost<{ id: number }>('/api/v1/x', { a: 1 })).resolves.toEqual({ id: 7 })
  })

  it('sends the stored profile on a personal favorite write', async () => {
    memoryStorage.setItem(profileStorageKey, '4')
    const fetchMock = vi.fn(async () => new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await addFavorite(9)

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const init = (fetchMock.mock.calls as unknown as Array<[string, RequestInit?]>)[0]?.[1]
    expect(new Headers(init?.headers).get(profileHeaderName)).toBe('4')
  })

  it('uses the admin id from GET /profiles when no profile is stored', async () => {
    const fetchMock = vi.fn(async (path: string) => {
      if (path === '/api/v1/profiles') {
        return new Response(JSON.stringify([{ id: 3, isAdmin: true, name: 'Admin' }]), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        })
      }
      return new Response(JSON.stringify({ items: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    })
    vi.stubGlobal('fetch', fetchMock)

    await apiGet('/api/v1/favorites', { personal: true })

    expect(fetchMock).toHaveBeenCalledTimes(2)
    const calls = fetchMock.mock.calls as unknown as Array<[string, RequestInit?]>
    expect(new Headers(calls[0]?.[1]?.headers).get(profileHeaderName)).toBeNull()
    expect(new Headers(calls[1]?.[1]?.headers).get(profileHeaderName)).toBe('3')
  })

  it('sends the stored profile on catalog and title reads', async () => {
    memoryStorage.setItem(profileStorageKey, '4')
    const fetchMock = vi.fn(async () =>
      new Response(JSON.stringify({ items: [], id: 9 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    await fetchTitles(2, { view: 'all' })
    await fetchTitle(9, 2)

    expect(fetchMock).toHaveBeenCalledTimes(2)
    for (const call of fetchMock.mock.calls) {
      const init = (call as unknown[])[1] as RequestInit
      expect(new Headers(init.headers).get(profileHeaderName)).toBe('4')
    }
  })

  it('throws ApiError for failed DELETE', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('no', { status: 404 })),
    )

    await expect(apiDelete('/api/v1/x')).rejects.toBeInstanceOf(ApiError)
  })
})

describe('buildQuery', () => {
  it('omits undefined, empty string, and false', () => {
    expect(
      buildQuery({
        q: 'matrix',
        view: 'all',
        favorite: false,
        empty: '',
        missing: undefined,
        page: 2,
        includeRemoved: true,
      }),
    ).toBe('?q=matrix&view=all&page=2&includeRemoved=true')
  })

  it('returns empty string when nothing remains', () => {
    expect(buildQuery({ favorite: false, q: '' })).toBe('')
  })
})
