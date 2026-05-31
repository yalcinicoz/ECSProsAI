import { useState, useMemo } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, Plus, Trash2 } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard } from '@/components/ui/PermissionGuard'
import { cn } from '@/lib/utils'
import type { Warehouse } from './WarehousesPage'
import { getWarehouseName } from './WarehousesPage'

const PERM = 'inventory.manage'
const PAGE_SIZE = 50

const LOCATION_TYPES = [
  { value: 'zone',  label: 'Bölge' },
  { value: 'aisle', label: 'Koridor' },
  { value: 'shelf', label: 'Raf' },
  { value: 'bin',   label: 'Göz' },
]

const WAREHOUSE_TYPES: Record<string, string> = {
  physical: 'Fiziksel', virtual: 'Sanal', dropship: 'Dropship', consignment: 'Konsinyasyon',
}

interface WarehouseLocation {
  id: string
  warehouseId: string
  code: string
  barcode: string
  name: string | null
  parentId: string | null
  locationType: string
  reservePriority: number
  pickingOrder: number
  isActive: boolean
  sortOrder: number
}

// ── Barcode helpers ────────────────────────────────────────────────────────────

function warehouseBarPrefix(warehouseId: string): string {
  const hex = warehouseId.replace(/-/g, '').slice(0, 6)
  const num = parseInt(hex, 16) % 1_000_000
  return String(num).padStart(6, '0')
}

function ean13Check(base12: string): string {
  let sum = 0
  for (let i = 0; i < 12; i++) sum += parseInt(base12[i]) * (i % 2 === 0 ? 1 : 3)
  return String((10 - (sum % 10)) % 10)
}

function maxExistingSeq(locations: WarehouseLocation[], prefix: string): number {
  let max = 0
  for (const loc of locations) {
    const b = loc.barcode
    if (b && b.length === 13 && b.startsWith(prefix)) {
      const seq = parseInt(b.slice(6, 12))
      if (!isNaN(seq) && seq > max) max = seq
    }
  }
  return max
}

function makeBarcode(prefix: string, startSeq: number, i: number): string {
  const seq = startSeq + i
  const base12 = prefix + String(seq).padStart(6, '0')
  return base12 + ean13Check(base12)
}

// ── Code increment helpers ─────────────────────────────────────────────────────

function parseCode(code: string): { prefix: string; numStr: string } | null {
  const m = code.match(/^(.*?)(\d+)$/)
  if (!m) return null
  return { prefix: m[1], numStr: m[2] }
}

function makeCode(base: string, i: number): string {
  const parsed = parseCode(base)
  if (!parsed) return base + (i > 0 ? String(i + 1) : '')
  const newNum = parseInt(parsed.numStr) + i
  return parsed.prefix + String(newNum).padStart(parsed.numStr.length, '0')
}

// ── Types ──────────────────────────────────────────────────────────────────────

type EditForm = {
  name: string
  locationType: string
  reservePriority: number
  pickingOrder: number
  sortOrder: number
  isActive: boolean
}

type BulkForm = {
  startCode: string
  count: number
  sortStart: number
  locationType: string
  parentId: string
  reservePriority: number
  pickingOrder: number
}

type DeleteForm = {
  startCode: string
  endCode: string
}

interface BlockedLocation { code: string; quantity: number }

interface BulkDeleteResult {
  deletedCount: number
  blocked: boolean
  blockedLocations: BlockedLocation[]
}

// ── Component ──────────────────────────────────────────────────────────────────

export function WarehouseDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [activeOnly, setActiveOnly] = useState(true)
  const [page, setPage] = useState(1)

  const [bulkOpen, setBulkOpen]   = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<WarehouseLocation | null>(null)

  const [editForm, setEditForm] = useState<EditForm>({
    name: '', locationType: 'bin', reservePriority: 0, pickingOrder: 0, sortOrder: 0, isActive: true,
  })
  const [bulkForm, setBulkForm] = useState<BulkForm>({
    startCode: '', count: 10, sortStart: 1, locationType: 'bin',
    parentId: '', reservePriority: 0, pickingOrder: 0,
  })
  const [deleteForm, setDeleteForm] = useState<DeleteForm>({ startCode: '', endCode: '' })
  const [deleteResult, setDeleteResult] = useState<BulkDeleteResult | null>(null)
  const [bulkProgress, setBulkProgress] = useState<{ done: number; total: number } | null>(null)
  const [bulkError, setBulkError] = useState('')

  // ── Queries ──────────────────────────────────────────────────────────────────

  const { data: warehouse, isLoading: wLoading } = useQuery<Warehouse>({
    queryKey: ['warehouse', id],
    queryFn: async () => {
      const { data } = await api.get('/inventory/warehouses?activeOnly=false')
      const list: Warehouse[] = data.data
      const found = list.find(w => w.id === id)
      if (!found) throw new Error('Depo bulunamadı')
      return found
    },
    enabled: !!id,
  })

  const { data: locations = [], isLoading: locLoading } = useQuery<WarehouseLocation[]>({
    queryKey: ['warehouse-locations', id, activeOnly],
    queryFn: async () => {
      const { data } = await api.get(`/inventory/warehouses/${id}/locations?activeOnly=${activeOnly}`)
      return data.data
    },
    enabled: !!id,
  })

  const allLocations = useQuery<WarehouseLocation[]>({
    queryKey: ['warehouse-locations', id, false],
    queryFn: async () => {
      const { data } = await api.get(`/inventory/warehouses/${id}/locations?activeOnly=false`)
      return data.data
    },
    enabled: !!id,
  }).data ?? []

  // ── Pagination ───────────────────────────────────────────────────────────────

  const totalPages = Math.ceil(locations.length / PAGE_SIZE)
  const pagedLocations = useMemo(
    () => locations.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [locations, page],
  )

  // ── Derived defaults ─────────────────────────────────────────────────────────

  const defaultSortStart = useMemo(() => {
    if (allLocations.length === 0) return 1
    return Math.max(...allLocations.map(l => l.sortOrder)) + 1
  }, [allLocations])

  // ── Bulk create preview ───────────────────────────────────────────────────────

  const preview = useMemo(() => {
    if (!bulkForm.startCode || bulkForm.count < 1 || !id) return null
    const prefix = warehouseBarPrefix(id)
    const startSeq = maxExistingSeq(allLocations, prefix) + 1
    const rows: { code: string; barcode: string; sort: number }[] = []
    const n = Math.min(bulkForm.count, 500)
    for (let i = 0; i < n; i++) {
      rows.push({
        code: makeCode(bulkForm.startCode, i),
        barcode: makeBarcode(prefix, startSeq, i),
        sort: bulkForm.sortStart + i,
      })
    }
    return { rows, prefix, startSeq }
  }, [bulkForm.startCode, bulkForm.count, bulkForm.sortStart, allLocations, id])

  // ── Delete preview (client-side filter) ──────────────────────────────────────

  const deletePreview = useMemo(() => {
    if (!deleteForm.startCode || !deleteForm.endCode) return []
    return allLocations
      .filter(l => l.code >= deleteForm.startCode && l.code <= deleteForm.endCode)
      .sort((a, b) => a.code.localeCompare(b.code))
  }, [deleteForm.startCode, deleteForm.endCode, allLocations])

  // ── Mutations ─────────────────────────────────────────────────────────────────

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!editTarget) return
      await api.put(`/inventory/locations/${editTarget.id}`, {
        name: editForm.name || null,
        locationType: editForm.locationType,
        reservePriority: editForm.reservePriority,
        pickingOrder: editForm.pickingOrder,
        sortOrder: editForm.sortOrder,
        isActive: editForm.isActive,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['warehouse-locations', id] })
      setEditTarget(null)
    },
  })

  async function runBulkCreate() {
    if (!preview || !id) return
    setBulkError('')
    setBulkProgress({ done: 0, total: preview.rows.length })
    let done = 0
    for (const row of preview.rows) {
      try {
        await api.post(`/inventory/warehouses/${id}/locations`, {
          code: row.code,
          barcode: row.barcode,
          name: null,
          parentId: bulkForm.parentId || null,
          locationType: bulkForm.locationType,
          reservePriority: bulkForm.reservePriority,
          pickingOrder: bulkForm.pickingOrder,
          sortOrder: row.sort,
        })
        done++
        setBulkProgress({ done, total: preview.rows.length })
      } catch (err: any) {
        const msg = err?.response?.data?.error ?? err?.message ?? 'Bilinmeyen hata'
        setBulkError(`${row.code} oluşturulurken hata: ${msg}`)
        break
      }
    }
    queryClient.invalidateQueries({ queryKey: ['warehouse-locations', id] })
    if (done === preview.rows.length) setBulkOpen(false)
    setBulkProgress(null)
  }

  async function runBulkDelete() {
    if (!id || !deleteForm.startCode || !deleteForm.endCode) return
    setDeleteResult(null)
    try {
      const { data } = await api.delete(`/inventory/warehouses/${id}/locations/bulk`, {
        data: { startCode: deleteForm.startCode, endCode: deleteForm.endCode },
      })
      const result: BulkDeleteResult = data.data
      setDeleteResult(result)
      if (!result.blocked) {
        queryClient.invalidateQueries({ queryKey: ['warehouse-locations', id] })
        setPage(1)
      }
    } catch (err: any) {
      setDeleteResult(null)
      const msg = err?.response?.data?.error ?? err?.message ?? 'Bilinmeyen hata'
      alert(msg)
    }
  }

  // ── Open helpers ──────────────────────────────────────────────────────────────

  function openBulk() {
    setBulkError('')
    setBulkProgress(null)
    setBulkForm(f => ({ ...f, sortStart: defaultSortStart, startCode: '', count: 10 }))
    setBulkOpen(true)
  }

  function openDelete() {
    setDeleteForm({ startCode: '', endCode: '' })
    setDeleteResult(null)
    setDeleteOpen(true)
  }

  function openEdit(loc: WarehouseLocation, e: React.MouseEvent) {
    e.stopPropagation()
    setEditTarget(loc)
    setEditForm({
      name: loc.name ?? '',
      locationType: loc.locationType,
      reservePriority: loc.reservePriority,
      pickingOrder: loc.pickingOrder,
      sortOrder: loc.sortOrder,
      isActive: loc.isActive,
    })
  }

  // ── Tree indent ───────────────────────────────────────────────────────────────

  const locationMap = Object.fromEntries(locations.map(l => [l.id, l]))
  function getDepth(loc: WarehouseLocation): number {
    if (!loc.parentId || !locationMap[loc.parentId]) return 0
    return 1 + getDepth(locationMap[loc.parentId])
  }

  if (wLoading || locLoading) return <PageSpinner />
  if (!warehouse) return (
    <div className="p-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Depo bulunamadı.</div>
  )

  const isCreating = bulkProgress !== null

  const previewRows = preview?.rows ?? []
  const showPreviewRows = previewRows.length <= 6
    ? previewRows
    : [...previewRows.slice(0, 3), null, ...previewRows.slice(-3)]

  const deletePreviewShow = deletePreview.length <= 8
    ? deletePreview
    : [...deletePreview.slice(0, 4), null, ...deletePreview.slice(-4)]

  return (
    <div className="p-6">
      {/* Breadcrumb */}
      <button
        onClick={() => navigate('/inventory/warehouses')}
        className="flex items-center gap-1 text-sm mb-4 hover:opacity-80 transition-opacity"
        style={{ color: 'var(--text-s)' }}>
        <ChevronLeft size={14} />
        Depolar
      </button>

      {/* Warehouse info card */}
      <div className="card p-5 mb-6">
        <div className="flex items-center gap-3 mb-1">
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>
            {getWarehouseName(warehouse)}
          </h1>
          <code className="text-xs px-2 py-0.5 rounded-md font-mono"
            style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
            {warehouse.code}
          </code>
          <Badge variant={warehouse.isActive ? 'success' : 'neutral'}>
            {warehouse.isActive ? 'Aktif' : 'Pasif'}
          </Badge>
        </div>
        <div className="flex items-center gap-4 text-sm" style={{ color: 'var(--text-s)' }}>
          <span>{WAREHOUSE_TYPES[warehouse.warehouseType] ?? warehouse.warehouseType}</span>
          {warehouse.isSellableOnline && <span className="text-green-600">✓ Online satışa açık</span>}
          {warehouse.address && <span>{warehouse.address}</span>}
        </div>
      </div>

      {/* Locations header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h2 className="text-base font-semibold" style={{ color: 'var(--text)' }}>Lokasyonlar</h2>
          <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
            {locations.length} kayıt
            {totalPages > 1 && ` · Sayfa ${page}/${totalPages}`}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {[true, false].map(v => (
              <button key={String(v)}
                onClick={() => { setActiveOnly(v); setPage(1) }}
                className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all',
                  activeOnly === v ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
                style={activeOnly === v ? { color: 'var(--text)' } : {}}>
                {v ? 'Aktif' : 'Tümü'}
              </button>
            ))}
          </div>
          <PermissionGuard permission={PERM}>
            <button
              onClick={openDelete}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-sm font-medium transition-colors"
              style={{ color: 'var(--text-s)', background: 'var(--surface2)', border: '1px solid var(--border)' }}>
              <Trash2 size={13} />
              Toplu Sil
            </button>
            <Button size="sm" onClick={openBulk}><Plus size={14} className="mr-1" />Yeni Lokasyon</Button>
          </PermissionGuard>
        </div>
      </div>

      {/* Locations table */}
      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'BARKOD', 'TİP', 'AD', 'DURUM', 'SIRALAMA', ''].map(h => (
                <th key={h} className={cn('px-4 py-3 text-xs font-semibold', h === '' ? 'w-20' : 'text-left')}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {locations.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Lokasyon bulunamadı.
              </td></tr>
            )}
            {pagedLocations.map(loc => {
              const depth = getDepth(loc)
              return (
                <tr key={loc.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <span className="text-sm font-mono font-medium"
                      style={{ color: 'var(--text)', paddingLeft: `${depth * 16}px` }}>
                      {depth > 0 && <span style={{ color: 'var(--text-s)' }} className="mr-1">└</span>}
                      {loc.code}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <code className="text-xs px-2 py-0.5 rounded-md font-mono"
                      style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                      {loc.barcode}
                    </code>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-m)' }}>
                      {LOCATION_TYPES.find(t => t.value === loc.locationType)?.label ?? loc.locationType}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>{loc.name ?? '—'}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge variant={loc.isActive ? 'success' : 'neutral'}>{loc.isActive ? 'Aktif' : 'Pasif'}</Badge>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>{loc.sortOrder}</span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <PermissionGuard permission={PERM}>
                      <button
                        className="text-xs px-2 py-1 rounded-lg transition-colors"
                        style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                        onClick={e => openEdit(loc, e)}>
                        Düzenle
                      </button>
                    </PermissionGuard>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between mt-3">
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, locations.length)} / {locations.length}
          </p>
          <div className="flex items-center gap-1">
            <button onClick={() => setPage(1)} disabled={page === 1}
              className="px-2 py-1 rounded-lg text-xs disabled:opacity-40"
              style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>«</button>
            <button onClick={() => setPage(p => p - 1)} disabled={page === 1}
              className="px-2 py-1.5 rounded-lg text-xs disabled:opacity-40"
              style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>‹ Önceki</button>
            {/* page numbers (up to 5 around current) */}
            {Array.from({ length: totalPages }, (_, i) => i + 1)
              .filter(p => p === 1 || p === totalPages || Math.abs(p - page) <= 2)
              .reduce<(number | '…')[]>((acc, p, i, arr) => {
                if (i > 0 && (p as number) - (arr[i - 1] as number) > 1) acc.push('…')
                acc.push(p)
                return acc
              }, [])
              .map((p, i) => p === '…'
                ? <span key={`e${i}`} className="px-1 text-xs" style={{ color: 'var(--text-s)' }}>…</span>
                : <button key={p} onClick={() => setPage(p as number)}
                    className={cn('w-7 h-7 rounded-lg text-xs font-medium transition-all',
                      page === p ? 'bg-[var(--brand)] text-white' : '')}
                    style={page !== p ? { border: '1px solid var(--border)', color: 'var(--text)' } : {}}>
                    {p}
                  </button>
              )}
            <button onClick={() => setPage(p => p + 1)} disabled={page === totalPages}
              className="px-2 py-1.5 rounded-lg text-xs disabled:opacity-40"
              style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki ›</button>
            <button onClick={() => setPage(totalPages)} disabled={page === totalPages}
              className="px-2 py-1 rounded-lg text-xs disabled:opacity-40"
              style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>»</button>
          </div>
        </div>
      )}

      {/* ── Bulk Create Modal ─────────────────────────────────────────────────── */}
      <Modal open={bulkOpen} onClose={() => !isCreating && setBulkOpen(false)} title="Lokasyon Ekle">
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Başlangıç Kodu <span className="text-red-500">*</span></label>
              <input className="inp font-mono" value={bulkForm.startCode}
                onChange={e => setBulkForm(f => ({ ...f, startCode: e.target.value }))}
                placeholder="Örn: MW-A1-0801"
                readOnly={isCreating} />
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Sondaki sayı artırılarak seri oluşturulur.</p>
            </div>
            <div>
              <label className="flbl">Lokasyon Sayısı <span className="text-red-500">*</span></label>
              <IntegerInput value={bulkForm.count}
                onChange={v => setBulkForm(f => ({ ...f, count: Math.min(500, Math.max(1, v ?? 1)) }))} />
            </div>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="flbl">Sıralama Başlangıcı</label>
              <IntegerInput value={bulkForm.sortStart}
                onChange={v => setBulkForm(f => ({ ...f, sortStart: v ?? 1 }))} />
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>MAX(Sıra)+1 = {defaultSortStart}</p>
            </div>
            <div>
              <label className="flbl">Rezerve Önceliği</label>
              <IntegerInput value={bulkForm.reservePriority}
                onChange={v => setBulkForm(f => ({ ...f, reservePriority: v ?? 0 }))} />
            </div>
            <div>
              <label className="flbl">Toplama Sırası</label>
              <IntegerInput value={bulkForm.pickingOrder}
                onChange={v => setBulkForm(f => ({ ...f, pickingOrder: v ?? 0 }))} />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Tip</label>
              <select className="inp" value={bulkForm.locationType}
                onChange={e => setBulkForm(f => ({ ...f, locationType: e.target.value }))}>
                {LOCATION_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
            <div>
              <label className="flbl">Üst Lokasyon <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
              <select className="inp" value={bulkForm.parentId}
                onChange={e => setBulkForm(f => ({ ...f, parentId: e.target.value }))}>
                <option value="">— Üst yok —</option>
                {allLocations.map(l => (
                  <option key={l.id} value={l.id}>{l.code}{l.name ? ` — ${l.name}` : ''}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Preview */}
          {preview && preview.rows.length > 0 && (
            <div>
              <p className="text-xs font-semibold mb-1.5" style={{ color: 'var(--text-s)' }}>
                ÖNİZLEME — {preview.rows.length} lokasyon
              </p>
              <div className="rounded-xl overflow-hidden text-xs font-mono" style={{ border: '1px solid var(--border)' }}>
                <table className="w-full">
                  <thead>
                    <tr style={{ background: 'var(--surface2)', borderBottom: '1px solid var(--border)' }}>
                      <th className="px-3 py-1.5 text-left font-semibold" style={{ color: 'var(--text-s)' }}>KOD</th>
                      <th className="px-3 py-1.5 text-left font-semibold" style={{ color: 'var(--text-s)' }}>BARKOD</th>
                      <th className="px-3 py-1.5 text-center font-semibold" style={{ color: 'var(--text-s)' }}>SIRALAMA</th>
                    </tr>
                  </thead>
                  <tbody>
                    {showPreviewRows.map((row, i) =>
                      row === null
                        ? <tr key="e"><td colSpan={3} className="px-3 py-1 text-center" style={{ color: 'var(--text-s)' }}>⋯ {preview.rows.length - 6} satır daha</td></tr>
                        : <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                            <td className="px-3 py-1.5" style={{ color: 'var(--text)' }}>{row.code}</td>
                            <td className="px-3 py-1.5" style={{ color: 'var(--text-m)' }}>{row.barcode}</td>
                            <td className="px-3 py-1.5 text-center" style={{ color: 'var(--text-s)' }}>{row.sort}</td>
                          </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Progress */}
          {isCreating && bulkProgress && (
            <div>
              <div className="flex justify-between text-xs mb-1" style={{ color: 'var(--text-s)' }}>
                <span>Oluşturuluyor...</span>
                <span>{bulkProgress.done} / {bulkProgress.total}</span>
              </div>
              <div className="h-2 rounded-full overflow-hidden" style={{ background: 'var(--surface2)' }}>
                <div className="h-full rounded-full transition-all"
                  style={{ background: 'var(--brand)', width: `${(bulkProgress.done / bulkProgress.total) * 100}%` }} />
              </div>
            </div>
          )}
          {bulkError && <p className="text-xs text-red-500">{bulkError}</p>}
        </div>
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setBulkOpen(false)} disabled={isCreating}>İptal</Button>
          <Button onClick={runBulkCreate} loading={isCreating}
            disabled={!preview || preview.rows.length === 0 || isCreating}>
            {preview ? `${preview.rows.length} Lokasyon Oluştur` : 'Oluştur'}
          </Button>
        </div>
      </Modal>

      {/* ── Bulk Delete Modal ─────────────────────────────────────────────────── */}
      <Modal open={deleteOpen} onClose={() => setDeleteOpen(false)} title="Toplu Lokasyon Sil">
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Başlangıç Kodu <span className="text-red-500">*</span></label>
              <input className="inp font-mono" value={deleteForm.startCode}
                onChange={e => { setDeleteForm(f => ({ ...f, startCode: e.target.value })); setDeleteResult(null) }}
                placeholder="Örn: MW-A1-0801" />
            </div>
            <div>
              <label className="flbl">Bitiş Kodu <span className="text-red-500">*</span></label>
              <input className="inp font-mono" value={deleteForm.endCode}
                onChange={e => { setDeleteForm(f => ({ ...f, endCode: e.target.value })); setDeleteResult(null) }}
                placeholder="Örn: MW-A1-0900" />
            </div>
          </div>

          {/* Delete preview */}
          {deletePreview.length > 0 && !deleteResult && (
            <div>
              <p className="text-xs font-semibold mb-1.5" style={{ color: 'var(--text-s)' }}>
                SİLİNECEK LOKASYONLAR — {deletePreview.length} adet
              </p>
              <div className="rounded-xl overflow-hidden text-xs" style={{ border: '1px solid var(--border)' }}>
                <table className="w-full font-mono">
                  <thead>
                    <tr style={{ background: 'var(--surface2)', borderBottom: '1px solid var(--border)' }}>
                      <th className="px-3 py-1.5 text-left font-semibold" style={{ color: 'var(--text-s)' }}>KOD</th>
                      <th className="px-3 py-1.5 text-left font-semibold" style={{ color: 'var(--text-s)' }}>TİP</th>
                      <th className="px-3 py-1.5 text-center font-semibold" style={{ color: 'var(--text-s)' }}>DURUM</th>
                    </tr>
                  </thead>
                  <tbody>
                    {deletePreviewShow.map((loc, _i) =>
                      loc === null
                        ? <tr key="e"><td colSpan={3} className="px-3 py-1 text-center" style={{ color: 'var(--text-s)' }}>⋯ {deletePreview.length - 8} lokasyon daha</td></tr>
                        : <tr key={loc.id} style={{ borderBottom: '1px solid var(--border)' }}>
                            <td className="px-3 py-1.5" style={{ color: 'var(--text)' }}>{loc.code}</td>
                            <td className="px-3 py-1.5" style={{ color: 'var(--text-m)' }}>
                              {LOCATION_TYPES.find(t => t.value === loc.locationType)?.label ?? loc.locationType}
                            </td>
                            <td className="px-3 py-1.5 text-center">
                              <Badge variant={loc.isActive ? 'success' : 'neutral'}>{loc.isActive ? 'Aktif' : 'Pasif'}</Badge>
                            </td>
                          </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {deletePreview.length === 0 && deleteForm.startCode && deleteForm.endCode && (
            <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu aralıkta lokasyon bulunamadı.</p>
          )}

          {/* Result: blocked */}
          {deleteResult?.blocked && (
            <div className="rounded-xl p-4" style={{ background: 'rgba(239,68,68,0.07)', border: '1px solid rgba(239,68,68,0.3)' }}>
              <p className="text-sm font-semibold text-red-600 mb-2">
                Silme engellendi — {deleteResult.blockedLocations.length} dolu lokasyon var
              </p>
              <p className="text-xs text-red-500 mb-3">
                Aşağıdaki lokasyonlarda ürün bulunduğu için silme işlemi gerçekleştirilmedi.
                Ürünleri başka lokasyona taşıyın veya stok düzeltmesi yapın.
              </p>
              <div className="rounded-lg overflow-hidden text-xs font-mono" style={{ border: '1px solid rgba(239,68,68,0.2)' }}>
                <table className="w-full">
                  <thead>
                    <tr style={{ background: 'rgba(239,68,68,0.07)', borderBottom: '1px solid rgba(239,68,68,0.2)' }}>
                      <th className="px-3 py-1.5 text-left font-semibold text-red-600">KOD</th>
                      <th className="px-3 py-1.5 text-center font-semibold text-red-600">ÜRÜN MİKTARI</th>
                    </tr>
                  </thead>
                  <tbody>
                    {deleteResult.blockedLocations.map(b => (
                      <tr key={b.code} style={{ borderBottom: '1px solid rgba(239,68,68,0.1)' }}>
                        <td className="px-3 py-1.5 text-red-700">{b.code}</td>
                        <td className="px-3 py-1.5 text-center font-semibold text-red-700">{b.quantity}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Result: success */}
          {deleteResult && !deleteResult.blocked && (
            <div className="rounded-xl p-3 text-sm text-green-700"
              style={{ background: 'rgba(34,197,94,0.08)', border: '1px solid rgba(34,197,94,0.3)' }}>
              ✓ {deleteResult.deletedCount} lokasyon başarıyla silindi.
            </div>
          )}
        </div>

        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setDeleteOpen(false)}>
            {deleteResult && !deleteResult.blocked ? 'Kapat' : 'İptal'}
          </Button>
          {(!deleteResult || deleteResult.blocked) && (
            <Button
              onClick={runBulkDelete}
              disabled={!deleteForm.startCode || !deleteForm.endCode || deletePreview.length === 0}
              style={{ background: 'var(--danger, #ef4444)', borderColor: 'var(--danger, #ef4444)' }}>
              {deletePreview.length > 0 ? `${deletePreview.length} Lokasyonu Sil` : 'Sil'}
            </Button>
          )}
        </div>
      </Modal>

      {/* ── Edit Modal ────────────────────────────────────────────────────────── */}
      <Modal open={!!editTarget} onClose={() => setEditTarget(null)} title="Lokasyon Düzenle">
        {editTarget && (
          <div className="mb-3 p-3 rounded-xl text-sm" style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
            <span className="font-mono font-medium">{editTarget.code}</span>
            {editTarget.barcode && <span className="ml-2 text-xs" style={{ color: 'var(--text-s)' }}>({editTarget.barcode})</span>}
          </div>
        )}
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Tip</label>
              <select className="inp" value={editForm.locationType}
                onChange={e => setEditForm(f => ({ ...f, locationType: e.target.value }))}>
                {LOCATION_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
            <div>
              <label className="flbl">Ad <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
              <input className="inp" value={editForm.name}
                onChange={e => setEditForm(f => ({ ...f, name: e.target.value }))}
                placeholder="İnsan okunabilir ad" />
            </div>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="flbl">Sıra</label>
              <IntegerInput value={editForm.sortOrder} onChange={v => setEditForm(f => ({ ...f, sortOrder: v ?? 0 }))} />
            </div>
            <div>
              <label className="flbl">Rezerve Önceliği</label>
              <IntegerInput value={editForm.reservePriority} onChange={v => setEditForm(f => ({ ...f, reservePriority: v ?? 0 }))} />
            </div>
            <div>
              <label className="flbl">Toplama Sırası</label>
              <IntegerInput value={editForm.pickingOrder} onChange={v => setEditForm(f => ({ ...f, pickingOrder: v ?? 0 }))} />
            </div>
          </div>
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
              checked={editForm.isActive}
              onChange={e => setEditForm(f => ({ ...f, isActive: e.target.checked }))} />
            <span className="text-sm" style={{ color: 'var(--text)' }}>Aktif</span>
          </label>
        </div>
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setEditTarget(null)}>İptal</Button>
          <Button onClick={() => updateMutation.mutate()} loading={updateMutation.isPending}>Kaydet</Button>
        </div>
      </Modal>
    </div>
  )
}
