import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { ChevronRight } from 'lucide-react'
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
import { cn } from '@/lib/utils'

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

export function getWarehouseName(w: Warehouse): string {
  return w.nameI18n['tr'] ?? w.nameI18n[Object.keys(w.nameI18n)[0]] ?? w.code
}

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

  const [activeOnly, setActiveOnly] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<Warehouse | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm())

  const { data: warehouses = [], isLoading } = useQuery<Warehouse[]>({
    queryKey: ['warehouses', activeOnly],
    queryFn: async () => {
      const { data } = await api.get(`/inventory/warehouses?activeOnly=${activeOnly}`)
      return data.data
    },
  })

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

  function openEdit(w: Warehouse, e: React.MouseEvent) {
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

  if (isLoading || langsLoading) return <PageSpinner />

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

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Depolar</h1>
            <PermissionGuard permission={PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{warehouses.length} kayıt</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {[false, true].map(v => (
              <button key={String(v)}
                onClick={() => setActiveOnly(v)}
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

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['AD', 'KOD', 'TİP', 'ONLİNE', 'DURUM', 'ADRES', ''].map(h => (
                <th key={h} className={cn('px-4 py-3 text-xs font-semibold', h === '' ? 'w-24' : 'text-left')}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {warehouses.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Depo bulunamadı.
              </td></tr>
            )}
            {warehouses.map(w => (
              <tr key={w.id}
                onClick={() => navigate(`/inventory/warehouses/${w.id}`)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getWarehouseName(w)}</span>
                </td>
                <td className="px-4 py-3">
                  <code className="text-xs px-2 py-0.5 rounded-md font-mono"
                    style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                    {w.code}
                  </code>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>
                    {WAREHOUSE_TYPES.find(t => t.value === w.warehouseType)?.label ?? w.warehouseType}
                  </span>
                </td>
                <td className="px-4 py-3 text-center">
                  <span className="text-sm">{w.isSellableOnline ? '✓' : '—'}</span>
                </td>
                <td className="px-4 py-3 text-center">
                  <Badge variant={w.isActive ? 'success' : 'neutral'}>{w.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 max-w-[200px]">
                  <span className="text-sm truncate block" style={{ color: 'var(--text-s)' }}>{w.address ?? '—'}</span>
                </td>
                <td className="px-4 py-3 text-right">
                  <div className="flex items-center justify-end gap-2">
                    <PermissionGuard permission={PERM}>
                      <button
                        className="text-xs px-2 py-1 rounded-lg transition-colors"
                        style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                        onClick={e => openEdit(w, e)}>
                        Düzenle
                      </button>
                    </PermissionGuard>
                    <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

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
