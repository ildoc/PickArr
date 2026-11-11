import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchSettings } from '../../api/settings'
import { fetchSystemStatus } from '../../api/system'
import { fetchProfiles } from '../../api/profiles'
import i18n from '../../i18n'
import { ProfileProvider } from '../../features/who/profileContext'
import { Header } from './Header'

vi.mock('../../api/settings', () => ({
  fetchSettings: vi.fn(),
}))

vi.mock('../../api/system', () => ({
  fetchSystemStatus: vi.fn(),
}))

vi.mock('../../api/profiles', () => ({
  fetchProfiles: vi.fn(),
}))

function wrap(ui: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={client}>
      <I18nextProvider i18n={i18n}>
        <ProfileProvider>
          <MemoryRouter>{ui}</MemoryRouter>
        </ProfileProvider>
      </I18nextProvider>
    </QueryClientProvider>,
  )
}

describe('Header', () => {
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
    vi.mocked(fetchSettings).mockResolvedValue({
      reco: { enabled: true },
    } as Awaited<ReturnType<typeof fetchSettings>>)
    vi.mocked(fetchProfiles).mockResolvedValue([
      {
        id: 1,
        name: 'Admin',
        color: 'blue',
        isAdmin: true,
        useImplicit: true,
        excludeInLibrary: true,
        createdAt: '2026-11-10T00:00:00Z',
      },
    ])
  })

  it('shows the Docker/image version from system status with GitHub and Ko-fi links', async () => {
    vi.mocked(fetchSystemStatus).mockResolvedValue({
      version: '1.2.3.4',
      utcNow: '2026-11-09T00:00:00Z',
    })

    wrap(<Header />)

    const version = await screen.findByRole('link', { name: '1.2.3.4' })
    expect(version).toHaveAttribute('href', 'https://github.com/ildoc/Pickarr')
    expect(fetchSystemStatus).toHaveBeenCalled()

    const kofi = screen.getByRole('link', { name: 'Support on Ko-fi' })
    expect(kofi).toHaveAttribute('href', 'https://ko-fi.com/pheeleeppo')
    expect(await screen.findByRole('link', { name: 'Switch profile, Admin' })).toHaveAttribute('href', '/who')
    expect(screen.getByText('Admin')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Settings' })).toHaveAttribute('href', '/settings/system')
  })

  it('hides settings for a profile that is not admin', async () => {
    vi.mocked(fetchProfiles).mockResolvedValue([
      {
        id: 1,
        name: 'Admin',
        color: 'blue',
        isAdmin: true,
        useImplicit: true,
        excludeInLibrary: true,
        createdAt: '2026-11-10T00:00:00Z',
      },
      {
        id: 2,
        name: 'Kid',
        color: 'teal',
        isAdmin: false,
        useImplicit: true,
        excludeInLibrary: true,
        createdAt: '2026-11-10T00:00:00Z',
      },
    ])
    localStorage.setItem('pickarr.profileId', '2')

    wrap(<Header />)

    expect(await screen.findByRole('link', { name: 'Switch profile, Kid' })).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Settings' })).toBeNull()
  })
})
