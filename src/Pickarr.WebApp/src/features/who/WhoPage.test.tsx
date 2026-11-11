import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchProfiles, type Profile } from '../../api/profiles'
import i18n from '../../i18n'
import { ProfileProvider } from './profileContext'
import { WhoPage } from './WhoPage'

vi.mock('../../api/profiles', () => ({
  fetchProfiles: vi.fn(),
  createProfile: vi.fn(),
  updateProfile: vi.fn(),
  deleteProfile: vi.fn(),
}))

function profile(id: number, name: string, isAdmin: boolean): Profile {
  return {
    id,
    name,
    color: 'blue',
    isAdmin,
    useImplicit: true,
    excludeInLibrary: true,
    createdAt: '2026-11-10T00:00:00Z',
  }
}

function renderWho(mode: 'picker' | 'management' = 'picker') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const ui: ReactNode = (
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <ProfileProvider>
          <MemoryRouter>
            <WhoPage mode={mode} />
          </MemoryRouter>
        </ProfileProvider>
      </I18nextProvider>
    </QueryClientProvider>
  )
  return render(ui)
}

describe('WhoPage', () => {
  beforeEach(() => {
    const store = new Map<string, string>()
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => {
        store.set(key, value)
      },
      removeItem: (key: string) => {
        store.delete(key)
      },
      clear: () => {
        store.clear()
      },
    })
    vi.clearAllMocks()
  })

  it('lets the admin manage everyone from the Users view', async () => {
    const user = userEvent.setup()
    vi.mocked(fetchProfiles).mockResolvedValue([profile(1, 'Admin', true), profile(2, 'Kid', false)])
    localStorage.setItem('pickarr.profileId', '1')

    renderWho('management')

    expect(await screen.findByRole('button', { name: 'Add profile' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Edit Kid' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Delete Kid' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Delete Admin' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Switch to profile, Kid' })).toBeNull()
    expect(screen.queryByRole('button', { name: /transfer/i })).toBeNull()

    await user.click(screen.getByRole('button', { name: 'Edit Kid' }))
    expect(screen.getByRole('checkbox', { name: /Use My list and library/i })).toBeTruthy()
    expect(screen.getByRole('checkbox', { name: /Hide library titles/i })).toBeTruthy()
  })

  it('marks the active profile and lets a non-admin edit only their own profile', async () => {
    vi.mocked(fetchProfiles).mockResolvedValue([profile(1, 'Admin', true), profile(2, 'Kid', false)])
    localStorage.setItem('pickarr.profileId', '2')

    renderWho()

    expect(await screen.findByRole('button', { name: 'Edit Kid' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Current profile, Kid' })).toHaveAttribute('aria-current', 'true')
    expect(screen.getByText('Current profile')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Switch to profile, Admin' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Edit Admin' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Add profile' })).toBeNull()
    expect(screen.queryByRole('button', { name: /Delete/ })).toBeNull()
  })

  it('hides add when the household already has 10 profiles', async () => {
    vi.mocked(fetchProfiles).mockResolvedValue([
      profile(1, 'Admin', true),
      ...Array.from({ length: 9 }, (_, index) => profile(index + 2, `P${index + 2}`, false)),
    ])
    localStorage.setItem('pickarr.profileId', '1')

    renderWho('management')

    expect(await screen.findByRole('button', { name: 'Edit Admin' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Add profile' })).toBeNull()
  })
})
