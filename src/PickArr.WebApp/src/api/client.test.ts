import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiDelete, apiGet, apiPost, apiPut, buildQuery } from './client'

afterEach(() => {
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
