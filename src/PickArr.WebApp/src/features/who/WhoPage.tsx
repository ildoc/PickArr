import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { createProfile, deleteProfile, updateProfile, type Profile } from '../../api/profiles'
import { profileInitial, profilePalette, profileSwatch } from './colors'
import { useProfiles } from './profileContext'

export function WhoPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { profiles, active, ready, choose } = useProfiles()
  const [editingId, setEditingId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['profiles'] })
  }

  const enter = (id: number) => {
    choose(id)
    navigate('/catalog')
  }

  const remove = useMutation({
    mutationFn: (id: number) => deleteProfile(id),
    onSuccess: async () => {
      setError(null)
      await refresh()
    },
    onError: () => setError(t('who.saveFailed')),
  })

  if (!ready) {
    return <p className="text-[var(--muted)]">{t('catalog.loading')}</p>
  }

  const canManage = active?.isAdmin === true

  return (
    <div className="mx-auto max-w-3xl space-y-8">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">{t('who.heading')}</h1>
        <p className="mt-2 text-sm text-[var(--muted)]">{t('who.lead')}</p>
      </div>

      {error && <p className="text-sm text-red-300">{error}</p>}

      <ul className="grid gap-4 sm:grid-cols-2">
        {profiles.map((profile) => {
          const canEdit = active != null && (active.isAdmin || active.id === profile.id)
          return (
            <li key={profile.id} className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-4">
              <button type="button" className="flex w-full items-center gap-3 text-left" onClick={() => enter(profile.id)}>
                <Avatar profile={profile} />
                <span>
                  <span className="block font-medium">{profile.name}</span>
                  {profile.isAdmin && <span className="block text-xs text-[var(--muted)]">{t('who.admin')}</span>}
                </span>
              </button>
              {canEdit && (
                <button
                  type="button"
                  className="text-sm text-[var(--muted)] hover:text-[var(--text)]"
                  onClick={() => setEditingId((current) => (current === profile.id ? null : profile.id))}
                >
                  {t('who.edit', { name: profile.name })}
                </button>
              )}
              {editingId === profile.id && canEdit && (
                <ProfileEditor
                  profile={profile}
                  onSaved={async () => {
                    setEditingId(null)
                    setError(null)
                    await refresh()
                  }}
                  onError={() => setError(t('who.saveFailed'))}
                />
              )}
              {canManage && !profile.isAdmin && (
                <button
                  type="button"
                  className="text-sm text-red-300 hover:text-red-200"
                  onClick={() => {
                    if (window.confirm(t('who.deleteConfirm', { name: profile.name }))) {
                      remove.mutate(profile.id)
                    }
                  }}
                >
                  {t('who.delete', { name: profile.name })}
                </button>
              )}
            </li>
          )
        })}
      </ul>

      {canManage && profiles.length < 10 && <CreateProfile onError={() => setError(t('who.saveFailed'))} onCreated={() => setError(null)} />}
    </div>
  )
}

function Avatar({ profile }: { profile: Profile }) {
  return (
    <span
      aria-hidden
      className="grid h-12 w-12 shrink-0 place-items-center rounded-full text-lg font-semibold text-black"
      style={{ background: profileSwatch(profile.color) }}
    >
      {profileInitial(profile.name)}
    </span>
  )
}

function CreateProfile({ onCreated, onError }: { onCreated: () => void; onError: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [color, setColor] = useState<string>('teal')

  const create = useMutation({
    mutationFn: () => createProfile({ name: name.trim(), color }),
    onSuccess: async () => {
      setName('')
      onCreated()
      await queryClient.invalidateQueries({ queryKey: ['profiles'] })
    },
    onError,
  })

  return (
    <form
      className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-4"
      onSubmit={(event) => {
        event.preventDefault()
        create.mutate()
      }}
    >
      <h2 className="text-lg font-medium">{t('who.add')}</h2>
      <label className="block space-y-1 text-sm">
        <span className="text-[var(--muted)]">{t('who.name')}</span>
        <input
          value={name}
          maxLength={40}
          required
          onChange={(event) => setName(event.target.value)}
          className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2"
        />
      </label>
      <ColorPicker color={color} onChange={setColor} />
      <button type="submit" className="rounded-full bg-white/10 px-4 py-1.5 text-sm" disabled={create.isPending}>
        {t('who.add')}
      </button>
    </form>
  )
}

function ProfileEditor({
  profile,
  onSaved,
  onError,
}: {
  profile: Profile
  onSaved: () => Promise<void>
  onError: () => void
}) {
  const { t } = useTranslation()
  const [name, setName] = useState(profile.name)
  const [color, setColor] = useState(profile.color)
  const [useImplicit, setUseImplicit] = useState(profile.useImplicit)
  const [excludeInLibrary, setExcludeInLibrary] = useState(profile.excludeInLibrary)

  const save = useMutation({
    mutationFn: () =>
      updateProfile(profile.id, {
        name: name.trim(),
        color,
        useImplicit,
        excludeInLibrary,
      }),
    onSuccess: onSaved,
    onError,
  })

  return (
    <form
      className="space-y-3"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate()
      }}
    >
      <label className="block space-y-1 text-sm">
        <span className="text-[var(--muted)]">{t('who.name')}</span>
        <input
          value={name}
          maxLength={40}
          required
          onChange={(event) => setName(event.target.value)}
          className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2"
        />
      </label>
      <ColorPicker color={color} onChange={setColor} />
      <label className="flex items-start gap-3 text-sm">
        <input
          type="checkbox"
          className="mt-1"
          checked={useImplicit}
          onChange={(event) => setUseImplicit(event.target.checked)}
        />
        <span>{t('settings.recoUseImplicit')}</span>
      </label>
      <label className="flex items-start gap-3 text-sm">
        <input
          type="checkbox"
          className="mt-1"
          checked={excludeInLibrary}
          onChange={(event) => setExcludeInLibrary(event.target.checked)}
        />
        <span>{t('settings.recoExcludeInLibrary')}</span>
      </label>
      <button type="submit" className="rounded-full bg-white/10 px-4 py-1.5 text-sm" disabled={save.isPending}>
        {t('settings.save')}
      </button>
    </form>
  )
}

function ColorPicker({ color, onChange }: { color: string; onChange: (color: string) => void }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-wrap gap-2" role="radiogroup" aria-label={t('who.color')}>
      {profilePalette.map((swatch) => (
        <button
          key={swatch}
          type="button"
          role="radio"
          aria-checked={color === swatch}
          aria-label={t('who.colorSwatch', { color: swatch })}
          className={`h-7 w-7 rounded-full ${color === swatch ? 'ring-2 ring-white' : ''}`}
          style={{ background: profileSwatch(swatch) }}
          onClick={() => onChange(swatch)}
        />
      ))}
    </div>
  )
}
