import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, ChevronRight, FolderOpen } from 'lucide-react'
import { cn, toSnakeCase } from '@/lib/utils'
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

const PLATFORM_PERM = 'catalog.platform.manage'

// ── Types ────────────────────────────────────────────────────────────────────

export interface ProductGroupAttribute {
  id: string
  attributeTypeId: string
  attributeTypeCode: string
  attributeTypeNameI18n: Record<string, string>
  isVariant: boolean
  isRequired: boolean
  isPrimaryAxis: boolean
  sortOrder: number
}

export interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
  isActive: boolean
  sortOrder: number
  hasProducts: boolean
  attributes: ProductGroupAttribute[]
}

function getName(pg: ProductGroup): string {
  return pg.nameI18n['tr'] ?? pg.nameI18n[Object.keys(pg.nameI18n)[0]] ?? '—'
}

// ── Component ─────────────────────────────────────────────────────────────────

export function ProductGroupsPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const [activeOnly, setActiveOnly] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [form, setForm] = useState<{
    nameI18n: Record<string, string>
    sortOrder: number
  }>({ nameI18n: {}, sortOrder: 0 })

  const { data: groups = [], isLoading } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups', activeOnly],
    queryFn: async () => {
      const { data } = await api.get(`/catalog/product-groups?activeOnly=${activeOnly}`)
      return data.data
    },
  })

  const mutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/catalog/product-groups', {
        nameI18n: form.nameI18n,
        sortOrder: form.sortOrder,
      })
      return data.data.id as string
    },
    onSuccess: (id) => {
      queryClient.invalidateQueries({ queryKey: ['product-groups'] })
      setCreateOpen(false)
      navigate(`/catalog/product-groups/${id}`)
    },
  })

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const previewCode = toSnakeCase(form.nameI18n['tr'] ?? form.nameI18n[sourceLang] ?? '')

  const i18nValues = useMemo(
    () => buildI18nValues(form.nameI18n, languages),
    [languages, form.nameI18n],
  )

  const i18nFields = useMemo(
    () => [{ key: 'name', labels: FL.name, required: true }],
    [],
  )

  function openCreate() {
    setForm({ nameI18n: {}, sortOrder: 0 })
    setCreateOpen(true)
  }

  const sorted = useMemo(
    () => [...groups].sort((a, b) => a.sortOrder - b.sortOrder || getName(a).localeCompare(getName(b), 'tr')),
    [groups],
  )

  if (isLoading || langsLoading) return <PageSpinner />

  return (
    <div className="p-6">
      {/* Page header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürün Grupları</h1>
            <PermissionGuard permission={PLATFORM_PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{groups.length} kayıt</p>
        </div>

        <div className="flex items-center gap-3">
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
              <Plus size={14} /> Yeni Grup
            </Button>
          </PermissionGuard>
        </div>
      </div>

      {/* Table */}
      <div className="card overflow-hidden p-0">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ad</th>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Kod</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Özellik</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Varyant</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sıra</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Durum</th>
              <th className="w-8 px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {sorted.length === 0 && (
              <tr>
                <td colSpan={7} className="text-center py-12 text-sm" style={{ color: 'var(--text-s)' }}>
                  Ürün grubu bulunamadı
                </td>
              </tr>
            )}
            {sorted.map((g) => {
              const variantCount = g.attributes.filter((a) => a.isVariant).length
              return (
                <tr
                  key={g.id}
                  onClick={() => navigate(`/catalog/product-groups/${g.id}`)}
                  className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                  style={{ borderBottom: '1px solid var(--border)' }}
                >
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <FolderOpen size={14} style={{ color: 'var(--brand)', flexShrink: 0 }} />
                      <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getName(g)}</span>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <code
                      className="text-xs px-2 py-0.5 rounded-md font-mono"
                      style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}
                    >
                      {g.code}
                    </code>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-sm" style={{ color: 'var(--text-m)' }}>{g.attributes.length}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    {variantCount > 0 ? (
                      <Badge variant="default">{variantCount} eksen</Badge>
                    ) : (
                      <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>{g.sortOrder}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge variant={g.isActive ? 'success' : 'neutral'}>
                      {g.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </td>
                  <td className="px-4 py-3">
                    <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Create Modal */}
      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title="Yeni Ürün Grubu"
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button
              onClick={() => mutation.mutate()}
              loading={mutation.isPending}
            >
              Kaydet
            </Button>
          </>
        }
      >
        <div className="space-y-5">
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
                  onChange={(lang, _key, val) =>
                    setForm((f) => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: val } }))
                  }
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

          {mutation.isError && (
            <p className="text-sm" style={{ color: '#ef4444' }}>
              Hata oluştu. Lütfen tekrar deneyin.
            </p>
          )}
        </div>
      </Modal>
    </div>
  )
}
