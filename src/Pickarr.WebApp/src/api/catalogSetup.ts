import { ApiError } from './client'

export type CatalogCountrySetup = {
  code: string
  enabled: boolean
  pages: number
  pagesMeasured: boolean
  incrementalPages: number
  fullyImported: boolean
}

export type CatalogProviderSetup = {
  id: number
  key: string
  name: string
  enabled: boolean
  countries: CatalogCountrySetup[]
}

export type CatalogSetup = {
  quota: number
  used: number
  plan: string
  freeCountryLimit: number
  creditsPerCall: number
  fullSyncsPerMonth: number
  dailyRunsPerMonth: number
  defaultPages: number
  providers: CatalogProviderSetup[]
}

export type UpdateCatalogSetupRequest = {
  providers: Array<{ id: number; enabled: boolean; countries: string[] }>
}

export function fetchCatalogSetup() {
  return readCatalog('/api/v1/settings/catalogs')
}

export function updateCatalogSetup(body: UpdateCatalogSetupRequest) {
  return readCatalog('/api/v1/settings/catalogs', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

async function readCatalog(path: string, init?: RequestInit): Promise<CatalogSetup> {
  const response = await fetch(path, init)
  if (!response.ok) {
    const payload = (await response.json().catch(() => null)) as { detail?: string } | null
    throw new ApiError(payload?.detail ?? `${init?.method ?? 'GET'} ${path} failed`, response.status)
  }
  return (await response.json()) as CatalogSetup
}
