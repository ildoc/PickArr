import { useQuery, useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useState, type ReactNode } from 'react'
import { clearProfileIdCache, profileStorageKey } from '../../api/client'
import { fetchProfiles, type Profile } from '../../api/profiles'

type ProfileContextValue = {
  profiles: Profile[]
  active: Profile | null
  ready: boolean
  choose: (id: number) => void
}

const ProfileContext = createContext<ProfileContextValue | null>(null)

function readStoredId(): number | null {
  try {
    const raw = localStorage.getItem(profileStorageKey)
    if (!raw || !/^[1-9]\d*$/.test(raw)) return null
    return Number(raw)
  } catch {
    return null
  }
}

function persistProfile(id: number) {
  const next = String(id)
  try {
    if (localStorage.getItem(profileStorageKey) !== next) {
      localStorage.setItem(profileStorageKey, next)
      clearProfileIdCache()
    }
  } catch {
    // Personal calls fall back to the admin id when storage is unavailable.
  }
}

function resolveActiveProfile(profiles: Profile[], selectedId: number | null): Profile | null {
  if (profiles.length === 0) return null
  if (profiles.length === 1) return profiles[0]!
  if (selectedId == null) return null
  return profiles.find((profile) => profile.id === selectedId) ?? null
}

export function ProfileProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const profilesQuery = useQuery({ queryKey: ['profiles'], queryFn: fetchProfiles })
  const [overrideId, setOverrideId] = useState<number | null>(null)
  const profiles = profilesQuery.data ?? []
  const active = profilesQuery.data
    ? resolveActiveProfile(profilesQuery.data, overrideId ?? readStoredId())
    : null

  if (active) {
    persistProfile(active.id)
  }

  const choose = (id: number) => {
    persistProfile(id)
    clearProfileIdCache()
    setOverrideId(id)
    void queryClient.invalidateQueries()
  }

  return (
    <ProfileContext.Provider
      value={{
        profiles,
        active,
        ready: profilesQuery.isSuccess || profilesQuery.isError,
        choose,
      }}
    >
      {children}
    </ProfileContext.Provider>
  )
}

export function useProfiles(): ProfileContextValue {
  const value = useContext(ProfileContext)
  if (!value) {
    throw new Error('useProfiles must be used within ProfileProvider')
  }
  return value
}
