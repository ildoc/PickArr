import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { deleteTitleArr } from '../../api/arr'
import { ApiError } from '../../api/client'
import type { TitleDetail } from '../../api/types'
import { formatBytes } from '../../lib/format'

type Props = {
  title: TitleDetail
  open: boolean
  onClose: () => void
  onDeleted: () => void
}

export function DeleteFromArrDialog({ title, open, onClose, onDeleted }: Props) {
  const { t, i18n } = useTranslation()
  const [deleteFiles, setDeleteFiles] = useState(false)
  const [addImportExclusion, setAddImportExclusion] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const remove = useMutation({
    mutationFn: () => deleteTitleArr(title.id, deleteFiles, addImportExclusion),
    onSuccess: () => {
      setError(null)
      onDeleted()
      onClose()
    },
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        setError(err.message)
        return
      }
      setError(t('arr.deleteFailed'))
    },
  })

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div
        role="dialog"
        aria-modal="true"
        className="w-[min(480px,92vw)] rounded-xl border border-white/10 bg-[var(--surface)] p-5 text-[var(--text)] shadow-2xl"
      >
      <h3 className="text-lg font-semibold">{t('arr.canDelete')}</h3>
      <p className="mt-1 text-sm text-[var(--muted)]">{title.name}</p>

      <dl className="mt-4 space-y-2 text-sm">
        <div>
          <dt className="text-[var(--muted)]">{t('arr.path')}</dt>
          <dd className="font-mono text-xs break-all">{title.arr.path ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-[var(--muted)]">{t('arr.sizeOnDisk')}</dt>
          <dd>{formatBytes(title.arr.sizeOnDisk, i18n.language)}</dd>
        </div>
        <div>
          <dt className="text-[var(--muted)]">{t('arr.fileCount')}</dt>
          <dd>{title.arr.fileCount ?? '—'}</dd>
        </div>
      </dl>

      <div className="mt-4 space-y-2 text-sm">
        <label className="flex items-center gap-2">
          <input
            type="checkbox"
            checked={deleteFiles}
            onChange={(e) => setDeleteFiles(e.target.checked)}
          />
          {t('arr.deleteFiles')}
        </label>
        <label className="flex items-center gap-2">
          <input
            type="checkbox"
            checked={addImportExclusion}
            onChange={(e) => setAddImportExclusion(e.target.checked)}
          />
          {t('arr.addImportExclusion')}
        </label>
      </div>

      {deleteFiles && (
        <p className="mt-3 text-sm text-amber-300">{t('arr.deleteFilesWarning')}</p>
      )}
      {error && <p className="mt-3 text-sm text-red-300">{error}</p>}

      <div className="mt-5 flex justify-end gap-2">
        <button
          type="button"
          onClick={onClose}
          className="rounded-md px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-white/5"
        >
          {t('title.close')}
        </button>
        <button
          type="button"
          disabled={remove.isPending}
          onClick={() => remove.mutate()}
          className={
            deleteFiles
              ? 'rounded-md bg-red-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-600 disabled:opacity-50'
              : 'rounded-md bg-white/10 px-3 py-1.5 text-sm font-medium hover:bg-white/15 disabled:opacity-50'
          }
        >
          {deleteFiles ? t('arr.confirmDeleteFiles') : t('arr.confirmDelete')}
        </button>
      </div>
      </div>
    </div>
  )
}
