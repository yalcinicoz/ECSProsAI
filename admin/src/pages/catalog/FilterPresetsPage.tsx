import { useState, useMemo, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil, Trash2, Filter, Link } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'
import { FilterBuilder, type FilterDef } from '@/components/catalog/FilterBuilder'

// ── Types ─────────────────────────────────────────────────────────────────────

interface FilterPreset {
  id: string
  code: string
  nameI18n: Record<string, string>
  description: string | null
  filterDef: FilterDef
  isActive: boolean
  sortOrder: number
  usedInCategories: number
}

interface PresetForm {
  code: string
  nameI18n: Record<string, string>
  description: string
  filterDef: FilterDef
  sortOrder: number
  isActive: boolean
}

function getName(p: FilterPreset): string {
  return p.nameI18n['tr'] ?? p.nameI18n[Object.keys(p.nameI18n)[0]] ?? p.code
}

function toSlug(s: string) {
  return s.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')
}

// ── Component ─────────────────────────────────────────────────────────────────

export function FilterPresetsPage() {
  const qc = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const [modal, setModal] = useState<null | 'create' | FilterPreset>(null)
  const [deleteTarget, setDeleteTarget] = useState<FilterPreset | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)

  const sourceLang = languages.find(l => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const nameFields = useMemo(() => [{ key: 'name', labels: FL.filterName, required: true }], [])

  const blankForm = useCallback((): PresetForm => ({
    code: '',
    nameI18n: languages.reduce((a, l) => ({ ...a, [l.code]: '' }), {}),
    description: '',
    filterDef: {},
    sortOrder: 0,
    isActive: true,
  }), [languages])

  const [form, setForm] = useState<PresetForm>(blankForm)

  // ── Fetch ──────────────────────────────────────────────────────────────────

  const { data: presets = [], isLoading } = useQuery<FilterPreset[]>({
    queryKey: ['filter-presets'],
    queryFn: async () => { const { data } = await api.get('/catalog/filter-presets'); return data.data },
  })

  // ── Open modal ─────────────────────────────────────────────────────────────

  function openCreate() {
    setForm(blankForm())
    setSaveError(null)
    setModal('create')
  }

  function openEdit(p: FilterPreset) {
    setForm({
      code: p.code,
      nameI18n: { ...p.nameI18n },
      description: p.description ?? '',
      filterDef: p.filterDef ?? {},
      sortOrder: p.sortOrder,
      isActive: p.isActive,
    })
    setSaveError(null)
    setModal(p)
  }

  // ── FilterBuilder callback ─────────────────────────────────────────────────

  const handleFilterChange = useCallback((def: FilterDef, desc: string) => {
    setForm(f => ({
      ...f,
      filterDef: def,
      // Sadece description boşsa otomatik doldur; kullanıcı üzerine yazdıysa koru
      description: f.description && f.description !== 'Tüm ürünler'
        ? f.description
        : desc,
    }))
  }, [])

  // ── Save mutation ──────────────────────────────────────────────────────────

  const saveMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        code: form.code.trim() || toSlug(form.nameI18n['tr'] ?? form.nameI18n[Object.keys(form.nameI18n)[0]] ?? 'filtre'),
        nameI18n: form.nameI18n,
        description: form.description.trim() || null,
        filterDef: form.filterDef,
        sortOrder: form.sortOrder,
        isActive: form.isActive,
      }
      if (modal === 'create') await api.post('/catalog/filter-presets', payload)
      else await api.put(`/catalog/filter-presets/${(modal as FilterPreset).id}`, payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['filter-presets'] }); setModal(null) },
    onError: (err: unknown) => {
      setSaveError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Bir hata oluştu.',
      )
    },
  })

  // ── Delete mutation ────────────────────────────────────────────────────────

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => api.delete(`/catalog/filter-presets/${id}`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['filter-presets'] }); setDeleteTarget(null) },
    onError: (err: unknown) => {
      alert((err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Silinemedi.')
    },
  })

  if (isLoading || langsLoading) return <PageSpinner />

  const isEditing = modal !== null && modal !== 'create'

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Filtreler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {presets.length} filtre — kategorilerde ve diğer yerlerde kullanılabilir
          </p>
        </div>
        <Button onClick={openCreate}><Plus size={14} /> Yeni Filtre</Button>
      </div>

      {/* Empty state */}
      {presets.length === 0 ? (
        <div className="card flex flex-col items-center justify-center py-16 gap-3" style={{ color: 'var(--text-s)' }}>
          <Filter size={32} />
          <p className="text-sm">Henüz filtre tanımlanmamış.</p>
          <Button onClick={openCreate}><Plus size={14} /> İlk Filtreyi Oluştur</Button>
        </div>
      ) : (
        <div className="card overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                <th className="text-left px-4 py-3 font-semibold" style={{ color: 'var(--text-m)' }}>Filtre</th>
                <th className="text-left px-4 py-3 font-semibold" style={{ color: 'var(--text-m)' }}>Otomatik Açıklama</th>
                <th className="text-center px-4 py-3 font-semibold" style={{ color: 'var(--text-m)' }}>Kullanım</th>
                <th className="text-center px-4 py-3 font-semibold" style={{ color: 'var(--text-m)' }}>Durum</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {presets.map((p, i) => (
                <tr
                  key={p.id}
                  className="cursor-pointer transition-colors hover:bg-[var(--surface2)]"
                  style={{ borderTop: i > 0 ? '1px solid var(--border)' : undefined }}
                  onClick={() => openEdit(p)}
                >
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <Filter size={14} style={{ color: 'var(--brand)', flexShrink: 0 }} />
                      <div>
                        <div className="font-medium" style={{ color: 'var(--text)' }}>{getName(p)}</div>
                        <code className="text-xs" style={{ color: 'var(--text-s)' }}>{p.code}</code>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3 max-w-xs">
                    <span className="text-sm line-clamp-2" style={{ color: 'var(--text-m)' }}>
                      {p.description || <span style={{ color: 'var(--text-s)', fontStyle: 'italic' }}>—</span>}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    {p.usedInCategories > 0 ? (
                      <span className="inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full"
                        style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}>
                        <Link size={10} /> {p.usedInCategories} kategori
                      </span>
                    ) : (
                      <span style={{ color: 'var(--text-s)', fontSize: '12px' }}>—</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge variant={p.isActive ? 'success' : 'neutral'}>{p.isActive ? 'Aktif' : 'Pasif'}</Badge>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1 justify-end">
                      <button
                        className="w-7 h-7 flex items-center justify-center rounded-lg hover:bg-[var(--surface2)] transition-colors"
                        style={{ color: 'var(--text-m)' }}
                        onClick={e => { e.stopPropagation(); openEdit(p) }}
                      ><Pencil size={13} /></button>
                      <button
                        className="w-7 h-7 flex items-center justify-center rounded-lg transition-colors"
                        style={{ color: p.usedInCategories > 0 ? 'var(--text-s)' : '#ef4444' }}
                        onClick={e => { e.stopPropagation(); if (p.usedInCategories === 0) setDeleteTarget(p) }}
                        disabled={p.usedInCategories > 0}
                        title={p.usedInCategories > 0 ? 'Kategorilerde kullanılıyor' : 'Sil'}
                      ><Trash2 size={13} /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* ── Create / Edit Modal ─────────────────────────────────────────────── */}
      {modal !== null && (
        <Modal
          open
          title={isEditing ? `Düzenle: ${getName(modal as FilterPreset)}` : 'Yeni Filtre'}
          onClose={() => setModal(null)}
          footer={
            <div className="flex items-center justify-between w-full">
              <div>
                {saveError && <p className="text-xs" style={{ color: '#ef4444' }}>{saveError}</p>}
              </div>
              <div className="flex gap-2">
                <Button variant="secondary" onClick={() => setModal(null)}>İptal</Button>
                <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending}>
                  {isEditing ? 'Kaydet' : 'Oluştur'}
                </Button>
              </div>
            </div>
          }
        >
          <div className="space-y-5">
            {/* Filtre adı */}
            {languages.length > 0 && (
              <div>
                <label className="flbl mb-2">Filtre Adı</label>
                <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                  <I18nField
                    sourceLang={sourceLang}
                    languages={languages}
                    fields={nameFields}
                    values={buildI18nValues(form.nameI18n, languages)}
                    onChange={(lang, _key, value) =>
                      setForm(f => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: value } }))}
                  />
                </div>
              </div>
            )}

            {/* Kod — sadece oluşturma */}
            {!isEditing && (
              <div>
                <label className="flbl">Kod <span className="text-xs font-normal" style={{ color: 'var(--text-s)' }}>(opsiyonel — boş bırakılırsa otomatik)</span></label>
                <input
                  className="inp"
                  value={form.code}
                  onChange={e => setForm(f => ({ ...f, code: toSlug(e.target.value) }))}
                  placeholder="erkek_gomlek_filtresi"
                />
              </div>
            )}

            {/* Ayraç */}
            <div style={{ borderTop: '2px solid var(--border)', margin: '0 -4px' }} />

            {/* Görsel filtre oluşturucu */}
            <FilterBuilder
              value={form.filterDef}
              onChange={handleFilterChange}
            />

            {/* Açıklama — düzenlenebilir override */}
            <div>
              <label className="flbl">
                Açıklama
                <span className="text-xs font-normal ml-1" style={{ color: 'var(--text-s)' }}>
                  (otomatik üretilir, istersen değiştirebilirsin)
                </span>
              </label>
              <textarea
                className="inp resize-none"
                rows={2}
                value={form.description}
                onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                placeholder="Erkek gömlek gruplarından, 150-500₺ arası ürünler"
              />
            </div>

            {/* Sıra + Aktif */}
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="flbl">Sıra</label>
                <IntegerInput value={form.sortOrder} onChange={v => setForm(f => ({ ...f, sortOrder: v ?? 0 }))} />
              </div>
              <div className="flex items-end pb-1">
                <label className="flex items-center gap-2 cursor-pointer text-sm" style={{ color: 'var(--text-m)' }}>
                  <input type="checkbox" checked={form.isActive}
                    onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
                    className="w-4 h-4 rounded" />
                  Aktif
                </label>
              </div>
            </div>
          </div>
        </Modal>
      )}

      {/* ── Delete Confirm ─────────────────────────────────────────────────── */}
      {deleteTarget && (
        <Modal
          open
          title="Filtreyi Sil"
          onClose={() => setDeleteTarget(null)}
          footer={
            <div className="flex gap-2 justify-end">
              <Button variant="secondary" onClick={() => setDeleteTarget(null)}>İptal</Button>
              <Button variant="danger" onClick={() => deleteMutation.mutate(deleteTarget.id)} loading={deleteMutation.isPending}>
                Sil
              </Button>
            </div>
          }
        >
          <p className="text-sm" style={{ color: 'var(--text-m)' }}>
            <strong style={{ color: 'var(--text)' }}>{getName(deleteTarget)}</strong> filtresini silmek istediğinize emin misiniz?
          </p>
        </Modal>
      )}
    </div>
  )
}
