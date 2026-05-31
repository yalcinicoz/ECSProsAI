import { useState, useMemo, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil, Trash2 } from 'lucide-react'
import { toSnakeCase } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard, ReadOnlyBadge } from '@/components/ui/PermissionGuard'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

const PERM = 'catalog.platform.manage'

interface FilterColor {
  id: string
  code: string
  nameI18n: Record<string, string>
  hexCode: string | null
  sortOrder: number
  isActive: boolean
}

function getName(c: FilterColor): string {
  return c.nameI18n['tr'] ?? c.nameI18n[Object.keys(c.nameI18n)[0]] ?? c.code
}

function ColorSwatch({ hex }: { hex: string | null }) {
  if (!hex) return <span className="text-[var(--text-s)] text-xs">—</span>
  return (
    <span className="inline-flex items-center gap-1.5">
      <span
        className="inline-block w-5 h-5 rounded border border-[var(--border)]"
        style={{ backgroundColor: hex }}
      />
      <span className="text-xs text-[var(--text-m)] font-mono">{hex}</span>
    </span>
  )
}

interface FormState {
  code: string
  nameI18n: Record<string, string>
  hexCode: string
  sortOrder: number
  isActive: boolean
}

function emptyForm(): FormState {
  return { code: '', nameI18n: {}, hexCode: '', sortOrder: 0, isActive: true }
}

function colorToForm(c: FilterColor): FormState {
  return {
    code: c.code,
    nameI18n: c.nameI18n,
    hexCode: c.hexCode ?? '',
    sortOrder: c.sortOrder,
    isActive: c.isActive,
  }
}

export function FilterColorsPage() {
  const qc = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<FilterColor | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm())
  const [deleteTarget, setDeleteTarget] = useState<FilterColor | null>(null)

  const sourceLang = languages.find(l => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'

  const { data, isLoading } = useQuery({
    queryKey: ['filter-colors'],
    queryFn: () => api.get('/catalog/filter-colors').then(r => r.data.data as FilterColor[]),
  })

  const createMutation = useMutation({
    mutationFn: (body: object) => api.post('/catalog/filter-colors', body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['filter-colors'] }); closeModal() },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, body }: { id: string; body: object }) => api.put(`/catalog/filter-colors/${id}`, body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['filter-colors'] }); closeModal() },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.delete(`/catalog/filter-colors/${id}`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['filter-colors'] }); setDeleteTarget(null) },
  })

  function openCreate() {
    setEditing(null)
    setForm(emptyForm())
    createMutation.reset()
    setModalOpen(true)
  }

  function openEdit(c: FilterColor) {
    setEditing(c)
    setForm(colorToForm(c))
    updateMutation.reset()
    setModalOpen(true)
  }

  function closeModal() {
    setModalOpen(false)
    setEditing(null)
  }

  const handleNameChange = useCallback((lang: string, _key: string, val: string) => {
    setForm(f => {
      const next = { ...f, nameI18n: { ...f.nameI18n, [lang]: val } }
      if (!editing && lang === 'tr') {
        next.code = toSnakeCase(val)
      }
      return next
    })
  }, [editing])

  const i18nValues = useMemo(() => buildI18nValues(form.nameI18n, languages), [form.nameI18n, languages])
  const i18nFields = useMemo(() => [{ key: 'name', labels: FL.name, required: true }], [])

  function submit() {
    const body = {
      code: form.code,
      nameI18n: form.nameI18n,
      hexCode: form.hexCode || null,
      sortOrder: form.sortOrder,
      isActive: form.isActive,
    }
    if (editing) {
      updateMutation.mutate({ id: editing.id, body })
    } else {
      createMutation.mutate(body)
    }
  }

  const isPending = createMutation.isPending || updateMutation.isPending
  const apiError = (createMutation.error as any)?.response?.data?.error
    ?? (updateMutation.error as any)?.response?.data?.error

  if (isLoading || langsLoading) return <PageSpinner />

  const colors = data ?? []

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-semibold">Filtre Renkleri</h1>
          <p className="text-sm text-[var(--text-m)] mt-0.5">
            Web sitesi filtre alanında kullanılan temel renk paleti. Özellik değerleri bu renklerle eşleştirilir.
          </p>
        </div>
        <PermissionGuard permission={PERM} fallback={<ReadOnlyBadge />}>
          <Button onClick={openCreate} size="sm">
            <Plus className="w-4 h-4 mr-1" />
            Yeni Renk
          </Button>
        </PermissionGuard>
      </div>

      <div className="bg-[var(--surface)] rounded-lg border border-[var(--border)] overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-[var(--border)] text-[var(--text-m)] text-xs uppercase tracking-wide">
              <th className="px-4 py-3 text-left font-medium">Renk</th>
              <th className="px-4 py-3 text-left font-medium">Kod</th>
              <th className="px-4 py-3 text-left font-medium">Hex</th>
              <th className="px-4 py-3 text-left font-medium">Sıra</th>
              <th className="px-4 py-3 text-left font-medium">Durum</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {colors.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-[var(--text-m)]">
                  Henüz filtre rengi tanımlanmamış.
                </td>
              </tr>
            )}
            {colors.map(c => (
              <tr
                key={c.id}
                className="border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface2)] transition-colors"
              >
                <td className="px-4 py-3 font-medium">{getName(c)}</td>
                <td className="px-4 py-3 font-mono text-xs text-[var(--text-m)]">{c.code}</td>
                <td className="px-4 py-3"><ColorSwatch hex={c.hexCode} /></td>
                <td className="px-4 py-3 text-[var(--text-m)]">{c.sortOrder}</td>
                <td className="px-4 py-3">
                  <Badge variant={c.isActive ? 'success' : 'default'}>
                    {c.isActive ? 'Aktif' : 'Pasif'}
                  </Badge>
                </td>
                <td className="px-4 py-3">
                  <PermissionGuard permission={PERM}>
                    <div className="flex items-center gap-2 justify-end">
                      <button
                        onClick={() => openEdit(c)}
                        className="p-1.5 rounded hover:bg-[var(--border)] text-[var(--text-m)] hover:text-[var(--text)]"
                      >
                        <Pencil className="w-3.5 h-3.5" />
                      </button>
                      <button
                        onClick={() => { deleteMutation.reset(); setDeleteTarget(c) }}
                        className="p-1.5 rounded hover:bg-[var(--border)] text-[var(--text-m)] hover:text-red-500"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </PermissionGuard>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create / Edit Modal */}
      <Modal
        open={modalOpen}
        onClose={closeModal}
        title={editing ? 'Filtre Rengi Düzenle' : 'Yeni Filtre Rengi'}
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={closeModal}>İptal</Button>
            <Button onClick={submit} loading={isPending}>Kaydet</Button>
          </>
        }
      >
        <div className="space-y-5">
          {languages.length > 0 && (
            <div>
              <label className="flbl mb-2">Ad</label>
              <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                <I18nField
                  sourceLang={sourceLang}
                  languages={languages}
                  fields={i18nFields}
                  values={i18nValues}
                  onChange={handleNameChange}
                  uppercase
                />
              </div>
            </div>
          )}

          <div>
            <label className="flbl">Kod</label>
            <input
              className="w-full px-3 py-2 rounded border border-[var(--border)] bg-[var(--surface)] text-sm font-mono mt-1"
              value={form.code}
              onChange={e => setForm(f => ({ ...f, code: e.target.value }))}
              placeholder="ornek_renk"
            />
          </div>

          <div>
            <label className="flbl">
              Hex Kodu
              <span className="ml-1 font-normal text-[var(--text-s)]">(opsiyonel, örn. #FF5733)</span>
            </label>
            <div className="flex items-center gap-2 mt-1">
              {form.hexCode && (
                <span
                  className="w-8 h-8 rounded border border-[var(--border)] flex-shrink-0"
                  style={{ backgroundColor: form.hexCode }}
                />
              )}
              <input
                className="flex-1 px-3 py-2 rounded border border-[var(--border)] bg-[var(--surface)] text-sm font-mono"
                value={form.hexCode}
                onChange={e => setForm(f => ({ ...f, hexCode: e.target.value }))}
                placeholder="#RRGGBB"
              />
            </div>
          </div>

          <div>
            <label className="flbl">Sıra</label>
            <IntegerInput
              value={form.sortOrder}
              onChange={v => setForm(f => ({ ...f, sortOrder: v ?? 0 }))}
            />
          </div>

          {editing && (
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
                className="w-4 h-4 rounded accent-[var(--brand)]"
              />
              <span className="text-sm">Aktif</span>
            </label>
          )}

          {apiError && (
            <p className="text-sm" style={{ color: '#ef4444' }}>{apiError}</p>
          )}
        </div>
      </Modal>

      {/* Delete Confirm Modal */}
      {deleteTarget && (
        <Modal
          open={!!deleteTarget}
          onClose={() => setDeleteTarget(null)}
          title="Filtre Rengi Sil"
          size="sm"
          footer={
            <>
              <Button variant="secondary" onClick={() => setDeleteTarget(null)}>İptal</Button>
              <Button
                variant="danger"
                loading={deleteMutation.isPending}
                onClick={() => deleteMutation.mutate(deleteTarget.id)}
              >
                Sil
              </Button>
            </>
          }
        >
          <p className="text-sm" style={{ color: 'var(--text-m)' }}>
            <strong style={{ color: 'var(--text)' }}>{getName(deleteTarget)}</strong> filtre rengi silinecek.
            Bu işlem geri alınamaz.
          </p>
          {(deleteMutation.error as any)?.response?.data?.error && (
            <p className="text-sm mt-3" style={{ color: '#ef4444' }}>
              {(deleteMutation.error as any).response.data.error}
            </p>
          )}
        </Modal>
      )}
    </div>
  )
}
