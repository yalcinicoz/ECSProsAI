import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, ChevronRight } from 'lucide-react'
import { cn, toSnakeCase } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard, ReadOnlyBadge } from '@/components/ui/PermissionGuard'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

const PLATFORM_PERM = 'catalog.platform.manage'

// ── Types ────────────────────────────────────────────────────────────────────

interface AttributeValue {
  id: string
  code: string
  nameI18n: Record<string, string>
  isActive: boolean
  sortOrder: number
}

interface AttributeType {
  id: string
  code: string
  nameI18n: Record<string, string>
  dataType: string
  isActive: boolean
  sortOrder: number
  values: AttributeValue[]
}

// ── Constants ─────────────────────────────────────────────────────────────────

const DATA_TYPE_OPTIONS = [
  { value: 'select',       label: 'Seçim Listesi' },
  { value: 'multi_select', label: 'Çoklu Seçim' },
  { value: 'text',         label: 'Metin' },
  { value: 'number',       label: 'Sayı' },
  { value: 'boolean',      label: 'Evet/Hayır' },
]

export const DATA_TYPE_LABELS: Record<string, string> = {
  select:       'Seçim Listesi',
  multi_select: 'Çoklu Seçim',
  text:         'Metin',
  number:       'Sayı',
  boolean:      'Evet/Hayır',
}

function getName(at: AttributeType): string {
  return at.nameI18n['tr'] ?? at.nameI18n[Object.keys(at.nameI18n)[0]] ?? at.code
}

// ── Component ─────────────────────────────────────────────────────────────────

export function AttributeTypesPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const [activeOnly, setActiveOnly] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [form, setForm] = useState<{
    nameI18n: Record<string, string>
    dataType: string
    sortOrder: number
    requiresFilterColor: boolean
  }>({ nameI18n: {}, dataType: 'select', sortOrder: 0, requiresFilterColor: false })

  const { data: attrTypes = [], isLoading } = useQuery<AttributeType[]>({
    queryKey: ['attribute-types', activeOnly],
    queryFn: async () => {
      const { data } = await api.get(`/catalog/attribute-types?activeOnly=${activeOnly}`)
      return data.data
    },
  })

  const mutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/catalog/attribute-types', {
        nameI18n: form.nameI18n,
        dataType: form.dataType,
        sortOrder: form.sortOrder,
        requiresFilterColor: form.requiresFilterColor,
      })
      return data.data.id as string
    },
    onSuccess: (id) => {
      queryClient.invalidateQueries({ queryKey: ['attribute-types'] })
      setCreateOpen(false)
      navigate(`/catalog/attribute-types/${id}`)
    },
  })

  const i18nValues = useMemo(
    () => buildI18nValues(form.nameI18n, languages),
    [languages, form.nameI18n],
  )

  const i18nFields = useMemo(
    () => [{ key: 'name', labels: FL.name, required: true }],
    [],
  )

  function handleNameChange(lang: string, _key: string, value: string) {
    setForm((f) => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: value } }))
  }

  function openCreate() {
    setForm({ nameI18n: {}, dataType: 'select', sortOrder: 0, requiresFilterColor: false })
    setCreateOpen(true)
  }

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const previewCode = toSnakeCase(form.nameI18n['tr'] ?? form.nameI18n[sourceLang] ?? '')
  const canSubmit = !!previewCode && !!form.dataType

  if (isLoading || langsLoading) return <PageSpinner />

  return (
    <div className="p-6">
      {/* Page header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Özellik Tipleri</h1>
            <PermissionGuard permission={PLATFORM_PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{attrTypes.length} kayıt</p>
        </div>

        <div className="flex items-center gap-3">
          {/* Active filter toggle */}
          <div
            className="flex items-center gap-1 rounded-xl p-1"
            style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}
          >
            <button
              onClick={() => setActiveOnly(false)}
              className={cn(
                'px-3 py-1 rounded-lg text-sm font-medium transition-all',
                !activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]',
              )}
              style={!activeOnly ? { color: 'var(--text)' } : {}}
            >
              Tümü
            </button>
            <button
              onClick={() => setActiveOnly(true)}
              className={cn(
                'px-3 py-1 rounded-lg text-sm font-medium transition-all',
                activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]',
              )}
              style={activeOnly ? { color: 'var(--text)' } : {}}
            >
              Aktif
            </button>
          </div>

          <PermissionGuard permission={PLATFORM_PERM}>
            <Button onClick={openCreate}>
              <Plus size={14} /> Yeni Özellik Tipi
            </Button>
          </PermissionGuard>
        </div>
      </div>

      {/* Table */}
      <div className="card overflow-hidden p-0">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Kod</th>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ad</th>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Veri Tipi</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Değer Sayısı</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sıra</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Durum</th>
              <th className="w-8 px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {attrTypes.length === 0 && (
              <tr>
                <td colSpan={7} className="text-center py-12 text-sm" style={{ color: 'var(--text-s)' }}>
                  Özellik tipi bulunamadı
                </td>
              </tr>
            )}
            {attrTypes.map((at) => (
              <tr
                key={at.id}
                onClick={() => navigate(`/catalog/attribute-types/${at.id}`)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}
              >
                <td className="px-4 py-3">
                  <code
                    className="text-xs px-2 py-0.5 rounded-md font-mono"
                    style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}
                  >
                    {at.code}
                  </code>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getName(at)}</span>
                </td>
                <td className="px-4 py-3">
                  <Badge variant="info">{DATA_TYPE_LABELS[at.dataType] ?? at.dataType}</Badge>
                </td>
                <td className="px-4 py-3 text-center">
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>{at.values.length}</span>
                </td>
                <td className="px-4 py-3 text-center">
                  <span className="text-sm" style={{ color: 'var(--text-s)' }}>{at.sortOrder}</span>
                </td>
                <td className="px-4 py-3 text-center">
                  <Badge variant={at.isActive ? 'success' : 'neutral'}>
                    {at.isActive ? 'Aktif' : 'Pasif'}
                  </Badge>
                </td>
                <td className="px-4 py-3">
                  <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create Modal */}
      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title="Yeni Özellik Tipi"
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button
              onClick={() => mutation.mutate()}
              loading={mutation.isPending}
              disabled={!canSubmit}
            >
              Kaydet
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          <div>
            <label className="flbl">Veri Tipi *</label>
            <SearchableSelect
              value={form.dataType}
              onChange={(v) => v && setForm((f) => ({ ...f, dataType: v }))}
              options={DATA_TYPE_OPTIONS}
              hasValue={!!form.dataType}
            />
          </div>

          <div>
            <label className="flbl">Sıra</label>
            <IntegerInput
              value={form.sortOrder}
              onChange={(v) => setForm((f) => ({ ...f, sortOrder: v ?? 0 }))}
            />
          </div>

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
                />
              </div>
            </div>
          )}

          <div>
            <label className="flbl">Otomatik Kod</label>
            <div
              className="flex items-center gap-2 px-3 py-2 rounded-xl"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}
            >
              <code className="text-sm font-mono" style={{ color: previewCode ? 'var(--brand)' : 'var(--text-s)' }}>
                {previewCode || '—'}
              </code>
            </div>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Türkçe addan otomatik üretilir. Kayıt sonrası değiştirilemez.
            </p>
          </div>

          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              className="w-4 h-4 rounded accent-[var(--brand)]"
              checked={form.requiresFilterColor}
              onChange={(e) => setForm((f) => ({ ...f, requiresFilterColor: e.target.checked }))}
            />
            <span className="text-sm" style={{ color: 'var(--text)' }}>Filtre rengi zorunlu</span>
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>(renk tipi özellikler için)</span>
          </label>

          {mutation.isError && (
            <p className="text-sm" style={{ color: 'var(--danger, #ef4444)' }}>
              Hata oluştu. Lütfen tekrar deneyin.
            </p>
          )}
        </div>
      </Modal>
    </div>
  )
}
