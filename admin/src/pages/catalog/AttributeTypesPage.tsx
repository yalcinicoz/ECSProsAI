import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { cn, toSnakeCase } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { PermissionGuard, ReadOnlyBadge } from '@/components/ui/PermissionGuard'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

const PLATFORM_PERM = 'catalog.platform.manage'

// ── Types ────────────────────────────────────────────────────────────────────

/** DataGrid satırı — /catalog/attribute-types/grid (değer listesi yerine SAYILARI gelir). */
interface AttributeTypeRow {
  id: string
  code: string
  nameI18n: Record<string, string>
  dataType: string
  isActive: boolean
  useInFilter: boolean
  sortOrder: number
  valueCount: number
  groupCount: number
  createdAt: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

// ── Constants ─────────────────────────────────────────────────────────────────

const DATA_TYPE_OPTIONS = [
  { value: 'select',       label: 'Seçim Listesi' },
  { value: 'multi_select', label: 'Çoklu Seçim' },
  { value: 'text',         label: 'Metin' },
  { value: 'number',       label: 'Sayı' },
  { value: 'boolean',      label: 'Evet/Hayır' },
]

const DATA_TYPE_LABELS: Record<string, string> = {
  select:       'Seçim Listesi',
  multi_select: 'Çoklu Seçim',
  text:         'Metin',
  number:       'Sayı',
  boolean:      'Evet/Hayır',
}

function getName(at: { nameI18n: Record<string, string>; code: string }): string {
  return at.nameI18n['tr'] ?? at.nameI18n[Object.keys(at.nameI18n)[0]] ?? at.code
}

// ── Component ─────────────────────────────────────────────────────────────────

export function AttributeTypesPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (AttributeTypeGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /catalog/attribute-types TÜM tipleri DEĞERLERİYLE döner (4 ekranın kaynağı);
  // liste ekranı yalın+sayfalı /attribute-types/grid kullanır.
  const [sp] = useSearchParams()
  const activeOnly = sp.get('activeOnly') === 'true'
  const grid = useGridState('attribute-types', { defaultPageSize: 20, defaultSort: 'sortOrder', defaultDir: 'asc' })
  const [createOpen, setCreateOpen] = useState(false)
  const [form, setForm] = useState<{
    nameI18n: Record<string, string>
    dataType: string
    sortOrder: number
    useInFilter: boolean
  }>({ nameI18n: {}, dataType: 'select', sortOrder: 0, useInFilter: false })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<AttributeTypeRow>>({
    queryKey: ['attribute-types-grid', activeOnly, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/catalog/attribute-types/grid?${grid.toParams({ activeOnly: activeOnly ? 'true' : undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const attrTypes = data?.items ?? []

  const mutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/catalog/attribute-types', {
        nameI18n: form.nameI18n,
        dataType: form.dataType,
        sortOrder: form.sortOrder,
        useInFilter: form.useInFilter,
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
    setForm({ nameI18n: {}, dataType: 'select', sortOrder: 0, useInFilter: false })
    setCreateOpen(true)
  }

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const previewCode = toSnakeCase(form.nameI18n['tr'] ?? form.nameI18n[sourceLang] ?? '')
  const canSubmit = !!previewCode && !!form.dataType

  if (langsLoading) return <PageSpinner />   // liste yüklemesi DataGrid'in kendi göstergesinde

  const columns: GridColumn<AttributeTypeRow>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 140,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      cell: at => <code className="text-xs px-2 py-0.5 rounded-md font-mono"
        style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{at.code}</code> },
    { key: 'name', header: 'AD', priority: 1, frozen: true, sortable: true, minWidth: 200,
      filter: { type: 'text', label: 'Ad' },
      cell: at => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getName(at)}</span> },
    { key: 'dataType', header: 'VERİ TİPİ', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Veri tipi', options: DATA_TYPE_OPTIONS.map(o => ({ value: o.value, label: o.label })) },
      cell: at => <Badge variant="info">{DATA_TYPE_LABELS[at.dataType] ?? at.dataType}</Badge> },
    { key: 'valueCount', header: 'DEĞER SAYISI', priority: 1, align: 'center', sortable: true,
      filter: { type: 'number', label: 'Değer sayısı' },
      filters: [{ field: 'hasValues', label: 'Değeri olan', type: 'boolean' }],
      cell: at => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{at.valueCount}</span> },
    { key: 'groupCount', header: 'KULLANAN GRUP', priority: 2, align: 'center', sortable: true,
      filter: { type: 'number', label: 'Kullanan grup sayısı' },
      cell: at => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{at.groupCount}</span> },
    { key: 'sortOrder', header: 'SIRA', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Sıra' },
      cell: at => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{at.sortOrder}</span> },
    { key: 'useInFilter', header: 'FİLTREDE', priority: 2, align: 'center', sortable: true,
      filter: { type: 'boolean', label: 'Vitrin filtresinde' },
      cell: at => <Badge variant={at.useInFilter ? 'success' : 'neutral'}>{at.useInFilter ? 'Evet' : 'Hayır'}</Badge> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, align: 'center', sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: at => <Badge variant={at.isActive ? 'success' : 'neutral'}>{at.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false,
      cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      {/* Page header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Özellik Tipleri</h1>
            <PermissionGuard permission={PLATFORM_PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>

        <div className="flex items-center gap-3">
          {/* Active filter toggle */}
          <div
            className="flex items-center gap-1 rounded-xl p-1"
            style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}
          >
            <button
              onClick={() => grid.mutate(n => n.delete('activeOnly'))}
              className={cn(
                'px-3 py-1 rounded-lg text-sm font-medium transition-all',
                !activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]',
              )}
              style={!activeOnly ? { color: 'var(--text)' } : {}}
            >
              Tümü
            </button>
            <button
              onClick={() => grid.mutate(n => n.set('activeOnly', 'true'))}
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

      <DataGrid<AttributeTypeRow>
        gridId="attribute-types"
        views
        grid={grid}
        columns={columns}
        rows={attrTypes}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={at => navigate(`/catalog/attribute-types/${at.id}`)}
        empty="Özellik tipi bulunamadı."
        search={{ placeholder: 'Özellik kodu veya adıyla ara…' }}
        minWidth={940}
        export={{ endpoint: '/catalog/attribute-types/export', named: () => ({ activeOnly: activeOnly ? 'true' : undefined }), fallbackFileName: 'ozellik-tipleri.xlsx' }}
        compact={{
          title: at => getName(at),
          subtitle: at => `${at.code} · ${DATA_TYPE_LABELS[at.dataType] ?? at.dataType}`,
          right: at => `${at.valueCount} değer`,
          badge: at => <Badge variant={at.isActive ? 'success' : 'neutral'}>{at.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

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
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Filtre alanındaki gösterim sırası (küçük değer üstte).
            </p>
          </div>

          <div>
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                className="w-4 h-4 rounded accent-[var(--brand)]"
                checked={form.useInFilter}
                onChange={(e) => setForm((f) => ({ ...f, useInFilter: e.target.checked }))}
              />
              <span className="text-sm" style={{ color: 'var(--text-m)' }}>Filtrede kullanılsın</span>
            </label>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              İşaretliyse bu özellik mağaza ürün listesi filtrelerinde gösterilir.
            </p>
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
