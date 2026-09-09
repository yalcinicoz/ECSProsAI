import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { PermissionGuard, ReadOnlyBadge } from '@/components/ui/PermissionGuard'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'
import { cn } from '@/lib/utils'
import { getWarehouseName } from './warehouseHelpers'

const PERM = 'inventory.manage'

const WAREHOUSE_TYPES = [
  { value: 'physical',     label: 'Fiziksel' },
  { value: 'virtual',      label: 'Sanal' },
  { value: 'dropship',     label: 'Dropship' },
  { value: 'consignment',  label: 'Konsinyasyon' },
]

export interface Warehouse {
  id: string
  code: string
  nameI18n: Record<string, string>
  warehouseType: string
  address: string | null
  isSellableOnline: boolean
  isActive: boolean
  sortOrder: number
}

/** DataGrid satırı — /inventory/warehouses/grid (kısım sayısı ve ERP kodu da gelir). */
export interface WarehouseRow extends Warehouse {
  reservePriority: number
  isCentral: boolean
  erpCode: string | null
  sectionCount: number
  createdAt: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

type FormState = {
  code: string
  nameI18n: Record<string, string>
  warehouseType: string
  address: string
  isSellableOnline: boolean
  reservePriority: number
  sortOrder: number
  isActive: boolean
}

const emptyForm = (): FormState => ({
  code: '', nameI18n: {}, warehouseType: 'physical', address: '',
  isSellableOnline: false, reservePriority: 0, sortOrder: 0, isActive: true,
})

export function WarehousesPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (WarehouseGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /inventory/warehouses TÜM depoları döner ve yedi ekranın dropdown kaynağıdır; liste
  // ekranı sayfalı /warehouses/grid ucunu kullanır (bkz. WarehouseGrid açıklaması).
  const [sp] = useSearchParams()
  const activeOnly = sp.get('activeOnly') === 'true'
  const grid = useGridState('warehouses', { defaultPageSize: 20, defaultSort: 'sortOrder', defaultDir: 'asc' })
  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<WarehouseRow | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm())

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<WarehouseRow>>({
    queryKey: ['warehouses-grid', activeOnly, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/inventory/warehouses/grid?${grid.toParams({ activeOnly: activeOnly ? 'true' : undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const warehouses = data?.items ?? []

  const createMutation = useMutation({
    mutationFn: async () => {
      await api.post('/inventory/warehouses', {
        code: form.code,
        nameI18n: form.nameI18n,
        warehouseType: form.warehouseType,
        address: form.address || null,
        isSellableOnline: form.isSellableOnline,
        reservePriority: form.reservePriority,
        sortOrder: form.sortOrder,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['warehouses'] }); setCreateOpen(false) },
  })

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!editTarget) return
      await api.put(`/inventory/warehouses/${editTarget.id}`, {
        nameI18n: form.nameI18n,
        warehouseType: form.warehouseType,
        address: form.address || null,
        isSellableOnline: form.isSellableOnline,
        reservePriority: form.reservePriority,
        isActive: form.isActive,
        sortOrder: form.sortOrder,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['warehouses'] }); setEditTarget(null) },
  })

  const sourceLang = languages.find(l => l.isDefault)?.code ?? 'tr'
  const i18nValues = useMemo(() => buildI18nValues(form.nameI18n, languages), [form.nameI18n, languages])
  const i18nFields = useMemo(() => [{ key: 'name', labels: FL.name, required: true }], [])

  function openCreate() {
    setForm(emptyForm())
    setCreateOpen(true)
  }

  function openEdit(w: WarehouseRow, e: React.MouseEvent) {
    e.stopPropagation()
    setEditTarget(w)
    setForm({
      code: w.code,
      nameI18n: { ...w.nameI18n },
      warehouseType: w.warehouseType,
      address: w.address ?? '',
      isSellableOnline: w.isSellableOnline,
      reservePriority: 0,
      sortOrder: w.sortOrder,
      isActive: w.isActive,
    })
  }

  if (langsLoading) return <PageSpinner />   // liste yüklemesi DataGrid'in kendi göstergesinde

  const formFields = (isEdit: boolean) => (
    <div className="space-y-4">
      {!isEdit && (
        <div>
          <label className="flbl">Kod <span className="text-red-500">*</span></label>
          <input className="inp" value={form.code}
            onChange={e => setForm(f => ({ ...f, code: e.target.value.toUpperCase() }))}
            placeholder="Örn: WH-001" />
        </div>
      )}
      <div>
        <label className="flbl">Tip</label>
        <select className="inp" value={form.warehouseType}
          onChange={e => setForm(f => ({ ...f, warehouseType: e.target.value }))}>
          {WAREHOUSE_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
        </select>
      </div>
      <div>
        <label className="flbl">Adres <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
        <textarea className="ta" rows={2} value={form.address}
          onChange={e => setForm(f => ({ ...f, address: e.target.value }))}
          placeholder="Depo adresi" />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="flbl">Sıra</label>
          <IntegerInput value={form.sortOrder} onChange={v => setForm(f => ({ ...f, sortOrder: v ?? 0 }))} />
        </div>
        <div>
          <label className="flbl">Rezervasyon Önceliği</label>
          <IntegerInput value={form.reservePriority} onChange={v => setForm(f => ({ ...f, reservePriority: v ?? 0 }))} />
        </div>
      </div>
      <div className="flex items-center gap-4">
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={form.isSellableOnline}
            onChange={e => setForm(f => ({ ...f, isSellableOnline: e.target.checked }))} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Online satışa açık</span>
        </label>
        {isEdit && (
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
              checked={form.isActive}
              onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))} />
            <span className="text-sm" style={{ color: 'var(--text)' }}>Aktif</span>
          </label>
        )}
      </div>
      <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
        <I18nField sourceLang={sourceLang} languages={languages} fields={i18nFields}
          values={i18nValues}
          onChange={(lang, _key, val) => setForm(f => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: val } }))} />
      </div>
    </div>
  )

  const columns: GridColumn<WarehouseRow>[] = [
    { key: 'name', header: 'AD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 200,
      filter: { type: 'text', label: 'Ad' },
      cell: w => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getWarehouseName(w)}</span> },
    { key: 'code', header: 'KOD', priority: 1, sortable: true, filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      filters: [{ field: 'erpCode', label: 'ERP kodu', type: 'text' }],
      cell: w => <code className="text-xs px-2 py-0.5 rounded-md font-mono"
        style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{w.code}</code> },
    { key: 'warehouseType', header: 'TİP', priority: 2, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Tip', options: WAREHOUSE_TYPES.map(t => ({ value: t.value, label: t.label })) },
      filters: [{ field: 'isCentral', label: 'Merkez depo', type: 'boolean' }],
      cell: w => <span className="text-sm" style={{ color: 'var(--text-m)' }}>
        {WAREHOUSE_TYPES.find(t => t.value === w.warehouseType)?.label ?? w.warehouseType}</span> },
    { key: 'isSellableOnline', header: 'ONLİNE', priority: 2, align: 'center', sortable: true,
      filter: { type: 'boolean', label: 'Online satış' },
      cell: w => <span className="text-sm">{w.isSellableOnline ? '✓' : '—'}</span> },
    { key: 'sectionCount', header: 'KISIM', priority: 2, align: 'center', sortable: true, filter: { type: 'number', label: 'Kısım sayısı' },
      cell: w => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{w.sectionCount}</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, align: 'center', sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: w => <Badge variant={w.isActive ? 'success' : 'neutral'}>{w.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'address', header: 'ADRES', priority: 3, sortable: true, filter: { type: 'text', label: 'Adres' },
      cell: w => <span className="text-sm truncate block" style={{ color: 'var(--text-s)', maxWidth: 200 }}>{w.address ?? '—'}</span> },
    { key: 'sortOrder', header: 'SIRA', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Sıra' },
      filters: [{ field: 'reservePriority', label: 'Rezerv önceliği', type: 'number' }],
      cell: w => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{w.sortOrder}</span> },
    { key: 'actions', header: '', priority: 3, align: 'right', exportable: false, stopRowClick: true,
      cell: w => <PermissionGuard permission={PERM}>
        <button className="text-xs px-2 py-1 rounded-lg transition-colors"
          style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
          onClick={e => openEdit(w, e)}>Düzenle</button>
      </PermissionGuard> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Depolar</h1>
            <PermissionGuard permission={PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {[false, true].map(v => (
              <button key={String(v)}
                onClick={() => grid.mutate(n => { if (v) n.set('activeOnly', 'true'); else n.delete('activeOnly') })}
                className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all',
                  activeOnly === v ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
                style={activeOnly === v ? { color: 'var(--text)' } : {}}>
                {v ? 'Aktif' : 'Tümü'}
              </button>
            ))}
          </div>
          <PermissionGuard permission={PERM}>
            <Button size="sm" onClick={openCreate}>+ Yeni Depo</Button>
          </PermissionGuard>
        </div>
      </div>

      <DataGrid<WarehouseRow>
        gridId="warehouses"
        views
        grid={grid}
        columns={columns}
        rows={warehouses}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={w => navigate(`/inventory/warehouses/${w.id}`)}
        empty="Depo bulunamadı."
        search={{ placeholder: 'Depo adı veya koduyla ara…' }}
        minWidth={900}
        export={{ endpoint: '/inventory/warehouses/export', named: () => ({ activeOnly: activeOnly ? 'true' : undefined }), fallbackFileName: 'depolar.xlsx' }}
        compact={{
          title: w => getWarehouseName(w),
          subtitle: w => `${w.code} · ${WAREHOUSE_TYPES.find(t => t.value === w.warehouseType)?.label ?? w.warehouseType}`,
          right: w => `${w.sectionCount} kısım`,
          badge: w => <Badge variant={w.isActive ? 'success' : 'neutral'}>{w.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {/* Create */}
      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Depo">
        {formFields(false)}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
          <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending}
            disabled={!form.code || !form.nameI18n[sourceLang]}>
            Oluştur
          </Button>
        </div>
      </Modal>

      {/* Edit */}
      <Modal open={!!editTarget} onClose={() => setEditTarget(null)} title="Depo Düzenle">
        {formFields(true)}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setEditTarget(null)}>İptal</Button>
          <Button onClick={() => updateMutation.mutate()} loading={updateMutation.isPending}>Kaydet</Button>
        </div>
      </Modal>
    </div>
  )
}
