import { apiDelete, apiGet, apiPatch, apiPost } from './client'

export type Profile = {
  id: number
  name: string
  color: string
  isAdmin: boolean
  useImplicit: boolean
  excludeInLibrary: boolean
  createdAt: string
}

export type ProfileDraft = {
  name?: string
  color?: string
  useImplicit?: boolean
  excludeInLibrary?: boolean
}

export function fetchProfiles(): Promise<Profile[]> {
  return apiGet<Profile[]>('/api/v1/profiles')
}

export function createProfile(body: { name: string; color: string }): Promise<Profile> {
  return apiPost<Profile>('/api/v1/profiles', body, { personal: true }) as Promise<Profile>
}

export function updateProfile(id: number, body: ProfileDraft): Promise<Profile> {
  return apiPatch<Profile>(`/api/v1/profiles/${id}`, body, { personal: true })
}

export function deleteProfile(id: number): Promise<void> {
  return apiDelete(`/api/v1/profiles/${id}`, { personal: true })
}
