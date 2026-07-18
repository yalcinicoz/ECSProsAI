import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, Plus, ChevronDown, ChevronRight, Pencil } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard } from '@/components/ui/PermissionGuard'
import type { Warehouse } from './WarehousesPage'
import { getWarehouseName } from './WarehousesPage'

const PERM = 'inventory.manage'

const WAREHOUSE_TYPES: Record<string, string> = {
  physical: 'Fiziksel', virtual: 'Sanal', dropship: 'Dropship', consignment: 'Konsinyasyon',
}

// Üçlü depo yapısı (Depo → Kısım → Birim, 2026-07-14 cutover) — eski Lokasyonlar UI'ı kaldırıldı (B-06)

interface WarehouseBin {
  id: string
  code: string
  barcode: string
  name: string | null
  pickingOrder: number
  isActive: boolean
  stockRowCount: number
  totalQuantity: number
}

interface WarehouseSection {
  id: string
  code: string
  name: string
  isSellableOnline: boolean
  pickingOrder: number
  isActive: boolean
  sortOrder: number
  stockRowCount: number
  totalQuantity: number
  bins: WarehouseBin[]
}

type SectionForm = { code: string; name: string; isSellableOnline: boolean; pickingOrder: number; isActive: boolean }
type BinForm = { code: string; barcode: string; name: string; isActive: boolean }

const EMPTY_SECTION: SectionForm = { code: '', name: '', isSellableOnline: true, pickingOrder: 0, isActive: true }
const EMPTY_BIN: BinForm = { code: '', barcode: '', name: '', isActive: true }

export function WarehouseDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const [sectionModal, setSectionModal] = useState<{ mode: 'create' } | { mode: 'edit'; section: WarehouseSection } | null>(null)
  const [sectionForm, setSectionForm] = useState<SectionForm>(EMPTY_SECTION)
  const [binModal, setBinModal] = useState<{ mode: 'create'; sectionId: string } | { mode: 'edit'; sectionId: string; bin: WarehouseBin } | null>(null)
  const [binForm, setBinForm] = useState<BinForm>(EMPTY_BIN)
  const [formError, setFormError] = useState('')

  const { data: warehouse, isLoading: wLoading } = useQuery<Warehouse>({
    queryKey: ['warehouse', id],
    queryFn: async () => {
      const { data } = await api.get('/inventory/warehouses?activeOnly=false')
      const found = (data.data as Warehouse[]).find(w => w.id === id)
      if (!found) throw new Error('Depo bulunamadı')
      return found
    },
    enabled: !!id,
  })

  const { data: sections = [], isLoading: sLoading } = useQuery<WarehouseSection[]>({
    queryKey: ['warehouse-sections', id],
    queryFn: async () => (await api.get(`/inventory/warehouses/${id}/sections`)).data.data,
    enabled: !!id,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['warehouse-sections', id] })
  const onErr = (e: unknown) => {
    const err = e as { response?: { data?: { error?: string } } }
    setFormError(err.response?.data?.error ?? 'Kaydedilemedi.')
  }

  const saveSection = useMutation({
    mutationFn: async () => {
      setFormError('')
      if (sectionModal?.mode === 'edit') {
        await api.put(`/inventory/sections/${sectionModal.section.id}`, {
          name: sectionForm.name, isSellableOnline: sectionForm.isSellableOnline,
          isActive: sectionForm.isActive, pickingOrder: sectionForm.pickingOrder,
        })
      } else {
        await api.post(`/inventory/warehouses/${id}/sections`, {
          code: sectionForm.code, name: sectionForm.name,
          isSellableOnline: sectionForm.isSellableOnline, pickingOrder: sectionForm.pickingOrder,
        })
      }
    },
    onSuccess: () => { setSectionModal(null); invalidate() },
    onError: onErr,
  })

  // Satışa açıklık toggle — satır üzerinden tek tıkla (site stok görünürlüğünün yönetim noktası)
  const toggleSellable = useMutation({
    mutationFn: async (s: WarehouseSection) => {
      await api.put(`/inventory/sections/${s.id}`, {
        name: s.name, isSellableOnline: !s.isSellableOnline, isActive: s.isActive, pickingOrder: s.pickingOrder,
      })
    },
    onSuccess: invalidate,
  })

  const saveBin = useMutation({
    mutationFn: async () => {
      setFormError('')
      if (binModal?.mode === 'edit') {
        await api.put(`/inventory/bins/${binModal.bin.id}`, {
          name: binForm.name || null, barcode: binForm.barcode, isActive: binForm.isActive,
        })
      } else if (binModal) {
        await api.post(`/inventory/sections/${binModal.sectionId}/bins`, {
          code: binForm.code, barcode: binForm.barcode, name: binForm.name || null,
        })
      }
    },
    onSuccess: () => { setBinModal(null); invalidate() },
    onError: onErr,
  })

  if (wLoading || !warehouse) return <PageSpinner />

  const toggleExpand = (sid: string) => setExpanded(prev => {
    const next = new Set(prev)
    next.has(sid) ? next.delete(sid) : next.add(sid)
    return next
  })

  return (
    <div className="p-6 max-w-5xl">
      {/* Başlık */}
      <div className="flex flex-wrap items-center gap-3 mb-1">
        <button onClick={() => navigate('/inventory/warehouses')} className="text-sm" style={{ color: 'var(--text-s)' }}>
          <ChevronLeft size={18} />
        </button>
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{getWarehouseName(warehouse)}</h1>
        <code className="text-xs px-2 py-0.5 rounded-md font-mono" style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
          {warehouse.code}
        </code>
        <Badge variant="neutral">{WAREHOUSE_TYPES[warehouse.warehouseType] ?? warehouse.warehouseType}</Badge>
        <Badge variant={warehouse.isActive ? 'success' : 'neutral'}>{warehouse.isActive ? 'Aktif' : 'Pasif'}</Badge>
      </div>
      <p className="text-sm mb-5" style={{ color: 'var(--text-s)' }}>
        Depo → Kısım → Birim yapısı. İnternete satılabilirlik <b>kısım</b> seviyesinde yönetilir —
        satışa kapalı kısımlardaki stok (iade/defo/bağış) sitede "stokta" sayılmaz.
      </p>

      {/* Kısımlar */}
      <div className="card overflow-hidden">
        <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Kısımlar ({sections.length})</h2>
          <PermissionGuard permission={PERM}>
            <Button size="sm" onClick={() => { setSectionForm(EMPTY_SECTION); setFormError(''); setSectionModal({ mode: 'create' }) }}>
              <Plus size={14} /> Yeni Kısım
            </Button>
          </PermissionGuard>
        </div>
        {sLoading && <p className="p-4 text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</p>}
        {!sLoading && sections.length === 0 && (
          <p className="p-4 text-sm" style={{ color: 'var(--text-s)' }}>
            Bu depoda kısım tanımlı değil. Reyon/mağaza depolarında tek kısım yeterlidir — "Yeni Kısım" ile ekleyin.
          </p>
        )}
        <div className="overflow-x-auto">
          {sections.length > 0 && (
            <table className="w-full">
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)' }}>
                  {['', 'Kod', 'Ad', 'İnternete Satış', 'Toplama Sırası', 'Birim', 'Stok (satır / adet)', 'Durum', ''].map((h, i) => (
                    <th key={i} className="px-3 py-2 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {sections.map(s => (
                  <SectionRows key={s.id} s={s} expanded={expanded.has(s.id)}
                    onToggle={() => toggleExpand(s.id)}
                    onToggleSellable={() => toggleSellable.mutate(s)}
                    onEdit={() => {
                      setSectionForm({ code: s.code, name: s.name, isSellableOnline: s.isSellableOnline, pickingOrder: s.pickingOrder, isActive: s.isActive })
                      setFormError(''); setSectionModal({ mode: 'edit', section: s })
                    }}
                    onAddBin={() => { setBinForm(EMPTY_BIN); setFormError(''); setBinModal({ mode: 'create', sectionId: s.id }) }}
                    onEditBin={(b) => {
                      setBinForm({ code: b.code, barcode: b.barcode, name: b.name ?? '', isActive: b.isActive })
                      setFormError(''); setBinModal({ mode: 'edit', sectionId: s.id, bin: b })
                    }}
                  />
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Kısım modal */}
      <Modal open={!!sectionModal} onClose={() => setSectionModal(null)}
        title={sectionModal?.mode === 'edit' ? `Kısım Düzenle — ${sectionModal.section.code}` : 'Yeni Kısım'}>
        <div className="space-y-3">
          {sectionModal?.mode !== 'edit' && (
            <div>
              <label className="flbl">Kod *</label>
              <input className="inp" value={sectionForm.code} placeholder="örn: SATIS-KATI, IADE"
                onChange={e => setSectionForm(f => ({ ...f, code: e.target.value.toUpperCase() }))} />
            </div>
          )}
          <div>
            <label className="flbl">Ad *</label>
            <input className="inp" value={sectionForm.name} placeholder="örn: Satış Katı"
              onChange={e => setSectionForm(f => ({ ...f, name: e.target.value }))} />
          </div>
          <div>
            <label className="flbl">Toplama Sırası</label>
            <IntegerInput className="inp" value={sectionForm.pickingOrder}
              onChange={v => setSectionForm(f => ({ ...f, pickingOrder: v ?? 0 }))} />
          </div>
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={sectionForm.isSellableOnline}
              onChange={e => setSectionForm(f => ({ ...f, isSellableOnline: e.target.checked }))} />
            İnternete satışa açık (bu kısımdaki serbest stok sitede "stokta" sayılır)
          </label>
          {sectionModal?.mode === 'edit' && (
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={sectionForm.isActive}
                onChange={e => setSectionForm(f => ({ ...f, isActive: e.target.checked }))} />
              Aktif
            </label>
          )}
          {formError && <p className="text-sm text-red-500">{formError}</p>}
        </div>
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setSectionModal(null)}>İptal</Button>
          <Button onClick={() => saveSection.mutate()} loading={saveSection.isPending}
            disabled={!sectionForm.name.trim() || (sectionModal?.mode !== 'edit' && !sectionForm.code.trim())}>
            Kaydet
          </Button>
        </div>
      </Modal>

      {/* Birim modal */}
      <Modal open={!!binModal} onClose={() => setBinModal(null)}
        title={binModal?.mode === 'edit' ? `Birim Düzenle — ${binModal.bin.code}` : 'Yeni Birim/Raf'}>
        <div className="space-y-3">
          {binModal?.mode !== 'edit' && (
            <div>
              <label className="flbl">Kod *</label>
              <input className="inp" value={binForm.code} placeholder="örn: A1-01"
                onChange={e => setBinForm(f => ({ ...f, code: e.target.value.toUpperCase() }))} />
            </div>
          )}
          <div>
            <label className="flbl">Barkod *</label>
            <input className="inp" value={binForm.barcode} placeholder="raf barkodu"
              onChange={e => setBinForm(f => ({ ...f, barcode: e.target.value }))} />
          </div>
          <div>
            <label className="flbl">Ad</label>
            <input className="inp" value={binForm.name}
              onChange={e => setBinForm(f => ({ ...f, name: e.target.value }))} />
          </div>
          {binModal?.mode === 'edit' && (
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={binForm.isActive}
                onChange={e => setBinForm(f => ({ ...f, isActive: e.target.checked }))} />
              Aktif
            </label>
          )}
          {formError && <p className="text-sm text-red-500">{formError}</p>}
        </div>
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setBinModal(null)}>İptal</Button>
          <Button onClick={() => saveBin.mutate()} loading={saveBin.isPending}
            disabled={!binForm.barcode.trim() || (binModal?.mode !== 'edit' && !binForm.code.trim())}>
            Kaydet
          </Button>
        </div>
      </Modal>
    </div>
  )
}

function SectionRows({ s, expanded, onToggle, onToggleSellable, onEdit, onAddBin, onEditBin }: {
  s: WarehouseSection
  expanded: boolean
  onToggle: () => void
  onToggleSellable: () => void
  onEdit: () => void
  onAddBin: () => void
  onEditBin: (b: WarehouseBin) => void
}) {
  return (
    <>
      <tr className="cursor-pointer hover:bg-[var(--surface2)]" style={{ borderBottom: '1px solid var(--border)' }} onClick={onToggle}>
        <td className="px-3 py-2" style={{ color: 'var(--text-s)' }}>
          {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
        </td>
        <td className="px-3 py-2"><code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{s.code}</code></td>
        <td className="px-3 py-2 text-sm font-medium" style={{ color: 'var(--text)' }}>{s.name}</td>
        <td className="px-3 py-2" onClick={e => e.stopPropagation()}>
          <button onClick={onToggleSellable} title="Tıklayarak değiştir"
            className="text-xs font-semibold px-2.5 py-1 rounded-full"
            style={s.isSellableOnline
              ? { background: 'var(--brand)18', color: 'var(--brand)', border: '1px solid var(--brand)' }
              : { background: '#ef444418', color: '#ef4444', border: '1px solid #ef444440' }}>
            {s.isSellableOnline ? '✓ Satışa açık' : '✗ Satışa kapalı'}
          </button>
        </td>
        <td className="px-3 py-2 text-sm" style={{ color: 'var(--text-m)' }}>{s.pickingOrder}</td>
        <td className="px-3 py-2 text-sm" style={{ color: 'var(--text-m)' }}>{s.bins.length}</td>
        <td className="px-3 py-2 text-sm font-mono" style={{ color: 'var(--text-m)' }}>
          {s.stockRowCount.toLocaleString('tr-TR')} / {s.totalQuantity.toLocaleString('tr-TR')}
        </td>
        <td className="px-3 py-2"><Badge variant={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Aktif' : 'Pasif'}</Badge></td>
        <td className="px-3 py-2" onClick={e => e.stopPropagation()}>
          <button onClick={onEdit} className="text-xs" style={{ color: 'var(--brand)' }}><Pencil size={13} /></button>
        </td>
      </tr>
      {expanded && (
        <tr style={{ borderBottom: '1px solid var(--border)' }}>
          <td></td>
          <td colSpan={8} className="px-3 py-3" style={{ background: 'var(--surface2)' }}>
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>
                Birimler / Raflar ({s.bins.length})
              </span>
              <Button size="sm" variant="secondary" onClick={onAddBin}><Plus size={12} /> Yeni Birim</Button>
            </div>
            {s.bins.length === 0 && (
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>
                Birim yok. Raf takibi yapılmayan kısımlarda tek "dummy" birim yeterlidir.
              </p>
            )}
            {s.bins.length > 0 && (
              <table className="w-full">
                <thead>
                  <tr>
                    {['Kod', 'Barkod', 'Ad', 'Stok (satır / adet)', 'Durum', ''].map((h, i) => (
                      <th key={i} className="px-2 py-1 text-left text-[11px] font-semibold uppercase" style={{ color: 'var(--text-s)' }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {s.bins.map(b => (
                    <tr key={b.id} style={{ borderTop: '1px solid var(--border)' }}>
                      <td className="px-2 py-1.5"><code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{b.code}</code></td>
                      <td className="px-2 py-1.5 text-xs font-mono" style={{ color: 'var(--text-m)' }}>{b.barcode}</td>
                      <td className="px-2 py-1.5 text-xs" style={{ color: 'var(--text-m)' }}>{b.name ?? '—'}</td>
                      <td className="px-2 py-1.5 text-xs font-mono" style={{ color: 'var(--text-m)' }}>
                        {b.stockRowCount.toLocaleString('tr-TR')} / {b.totalQuantity.toLocaleString('tr-TR')}
                      </td>
                      <td className="px-2 py-1.5"><Badge variant={b.isActive ? 'success' : 'neutral'}>{b.isActive ? 'Aktif' : 'Pasif'}</Badge></td>
                      <td className="px-2 py-1.5">
                        <button onClick={() => onEditBin(b)} className="text-xs" style={{ color: 'var(--brand)' }}><Pencil size={12} /></button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </td>
        </tr>
      )}
    </>
  )
}
