export class ApiError extends Error {
  status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export const profileStorageKey = 'pickarr.profileId'
export const profileHeaderName = 'X-Pickarr-Profile'

export type ApiRequestOptions = { personal?: boolean }

type ProfileSummary = { id: number; isAdmin: boolean }

let cachedAdminProfileId: number | null = null

export function clearProfileIdCache() {
  cachedAdminProfileId = null
}

export async function resolveProfileId(): Promise<number> {
  try {
    const stored = localStorage.getItem(profileStorageKey)
    if (stored && /^[1-9]\d*$/.test(stored)) {
      return Number(stored)
    }
  } catch {
    // localStorage can be unavailable; fall through to the admin profile.
  }

  if (cachedAdminProfileId != null) {
    return cachedAdminProfileId
  }

  const profiles = await apiGet<ProfileSummary[]>('/api/v1/profiles')
  const admin = profiles.find((profile) => profile.isAdmin)
  if (!admin) {
    throw new ApiError('GET /api/v1/profiles failed', 404)
  }

  cachedAdminProfileId = admin.id
  return admin.id
}

async function requestHeaders(body: unknown, personal: boolean | undefined): Promise<Headers | undefined> {
  if (body === undefined && !personal) {
    return undefined
  }

  const headers = new Headers()
  if (body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }
  if (personal) {
    headers.set(profileHeaderName, String(await resolveProfileId()))
  }
  return headers
}

export async function apiGet<T>(path: string, options?: ApiRequestOptions): Promise<T> {
  const response = await fetch(path, { headers: await requestHeaders(undefined, options?.personal) })
  if (!response.ok) {
    throw new ApiError(`GET ${path} failed`, response.status)
  }
  return (await response.json()) as T
}

export async function apiPut<T>(path: string, body?: unknown, options?: ApiRequestOptions): Promise<T | void> {
  const response = await fetch(path, {
    method: 'PUT',
    headers: await requestHeaders(body, options?.personal),
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    throw new ApiError(`PUT ${path} failed`, response.status)
  }
  if (response.status === 204) {
    return
  }
  return (await response.json()) as T
}

export async function apiDelete(path: string, options?: ApiRequestOptions): Promise<void> {
  const response = await fetch(path, {
    method: 'DELETE',
    headers: await requestHeaders(undefined, options?.personal),
  })
  if (!response.ok) {
    throw new ApiError(`DELETE ${path} failed`, response.status)
  }
}

export async function apiPatch<T>(path: string, body?: unknown, options?: ApiRequestOptions): Promise<T> {
  const response = await fetch(path, {
    method: 'PATCH',
    headers: await requestHeaders(body, options?.personal),
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    throw new ApiError(`PATCH ${path} failed`, response.status)
  }
  return (await response.json()) as T
}

export async function apiPost<T>(path: string, body?: unknown, options?: ApiRequestOptions): Promise<T | void> {
  const response = await fetch(path, {
    method: 'POST',
    headers: await requestHeaders(body, options?.personal),
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    throw new ApiError(`POST ${path} failed`, response.status)
  }
  if (response.status === 204) {
    return
  }
  return (await response.json()) as T
}

export function buildQuery(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === '' || value === false) continue
    search.set(key, String(value))
  }
  const qs = search.toString()
  return qs ? `?${qs}` : ''
}
