import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'
import { RETURN_STATUS_MAP } from './orderConstants'
import { DataGrid, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'

const TABS = [
  { key: 'requested', label: 'Talep Edilen' },
  { key: 'approved',  label: 'Onaylı' },
  { key: 'received',  label: 'Teslim Alınan' },
  { key: 'refunded',  label: 'Geri Ödenen' },
  { key: 'rejected',  label: 'Reddedilen' },
  { key: '',          label: 'Tümü' },
]

export interface ReturnSummary {
  id: string
  returnNumber: string
  orderId: string
  memberId: string
  returnType: string
  status: string
  refundMethod: string
  refundStatus: string
  refundAmount: number
  createdAt: string
  cargoReturnCode?: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ReturnReason {
  id: string
  nameI18n: Record<string, string>
  isDefault: boolean
  isActive: boolean
  sortOrder: number
  color?: string
  icon?: string
  extraData?: { subReasons?: string[] }
}

// ── İade nedenleri yönetim modalı (return_reason lookup — P0 bulgusu) ─────────
function ReasonEditModal({ reason, onClose }: { reason: ReturnReason | 'new'; onClose: () => void }) {
  const queryClient = useQueryClient()
  const isNew = reason === 'new'
  const r = isNew ? undefined : reason
  const [name, setName] = useState(r?.nameI18n?.['tr'] ?? '')
  const [subReasons, setSubReasons] = useState((r?.extraData?.subReasons ?? []).join('\n'))
  const [isActive, setIsActive] = useState(r?.isActive ?? true)
  const [sortOrder, setSortOrder] = useState(r?.sortOrder ?? 0)
  const [error, setError] = useState('')

  const save = useMutation({
    mutationFn: async () => {
      const subs = subReasons.split('\n').map(s => s.trim()).filter(Boolean)
      const body = {
        nameI18n: { ...(r?.nameI18n ?? {}), tr: name.trim() },
        color: r?.color ?? null,
        icon: r?.icon ?? null,
        isDefault: r?.isDefault ?? false,
        isActive,
        sortOrder,
        extraData: { ...(r?.extraData ?? {}), subReasons: subs },
      }
      if (isNew) await api.post('/lookup/types/return_reason/values', body)
      else await api.put(`/lookup/values/${r!.id}`, body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['return-reasons'] })
      onClose()
    },
    onError: (e: unknown) => {
      const err = e as { response?: { data?: { error?: string } } }
      setError(err.response?.data?.error ?? 'Kaydedilemedi.')
    },
  })

  return (
    <Modal open onClose={onClose} title={isNew ? 'Yeni İade Nedeni' : 'İade Nedenini Düzenle'}>
      <div className="space-y-3">
        <div>
          <label className="flbl">Neden (ana başlık) <span className="text-red-500">*</span></label>
          <input className="inp" value={name} onChange={e => setName(e.target.value)}
            placeholder="ör. Bedeni olmadı" />
        </div>
        <div>
          <label className="flbl">Alt Nedenler <span className="text-xs" style={{ color: 'var(--text-s)' }}>(her satır bir seçenek — sitedeki aramalı listede görünür)</span></label>
          <textarea className="ta" rows={6} value={subReasons} onChange={e => setSubReasons(e.target.value)}
            placeholder={'Küçük geldi\nBüyük geldi'} />
        </div>
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
            Aktif
          </label>
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            Sıra
            <input type="number" className="inp w-20 py-1" value={sortOrder}
              onChange={e => setSortOrder(parseInt(e.target.value) || 0)} />
          </label>
        </div>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!name.trim()}>Kaydet</Button>
      </div>
    </Modal>
  )
}

function ReasonsModal({ onClose }: { onClose: () => void }) {
  const [editing, setEditing] = useState<ReturnReason | 'new' | null>(null)

  const { data: reasons = [], isLoading } = useQuery<ReturnReason[]>({
    queryKey: ['return-reasons'],
    queryFn: async () => (await api.get('/lookup/types/return_reason/values?activeOnly=false')).data.data,
  })

  return (
    <>
      <Modal open onClose={onClose} title="İade Nedenleri">
        <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
          Sitedeki iade talep formunun ana/alt neden listesi buradan yönetilir. Pasif neden formda görünmez;
          geçmiş taleplerdeki kayıtlar etkilenmez.
        </p>
        {isLoading && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</p>}
        <div className="space-y-1 max-h-96 overflow-y-auto">
          {reasons.map(r => (
            <div key={r.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-[var(--surface2)] cursor-pointer"
              onClick={() => setEditing(r)}>
              <span className="text-sm" style={{ color: r.isActive ? 'var(--text)' : 'var(--text-s)' }}>
                {r.nameI18n?.['tr'] ?? '—'}
              </span>
              <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                {(r.extraData?.subReasons?.length ?? 0)} alt neden
              </span>
              {!r.isActive && <Badge variant="neutral">Pasif</Badge>}
              <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>Düzenle →</span>
            </div>
          ))}
          {!isLoading && reasons.length === 0 && (
            <p className="text-sm" style={{ color: 'var(--text-s)' }}>Tanımlı neden yok.</p>
          )}
        </div>
        <div className="flex justify-between gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button size="sm" onClick={() => setEditing('new')}>+ Yeni Neden</Button>
          <Button variant="secondary" onClick={onClose}>Kapat</Button>
        </div>
      </Modal>
      {editing && <ReasonEditModal reason={editing} onClose={() => setEditing(null)} />}
    </>
  )
}

// ── İade listesi — DataGrid F4: sekme ?tab= (requested varsayılan), filtre/arama/sıralama URL'de ──
const RETURN_EXTRA_FILTERS: GridFilterField[] = [
  { key: 'refundStatus', label: 'Geri ödeme durumu', type: 'text', ops: ['eq', 'contains'] },
  { key: 'refundMethod', label: 'Geri ödeme yöntemi', type: 'text', ops: ['eq', 'contains'] },
  { key: 'trackingNumber', label: 'Kargo takip no', type: 'text' },
  { key: 'cargoReturnCode', label: 'Kargo iade kodu', type: 'text' },
  { key: 'cargoReceivedAt', label: 'Teslim alınma tarihi', type: 'date' },
]

export function ReturnsPage() {
  const navigate = useNavigate()
  const grid = useGridState('returns', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const tab = sp.get('tab') ?? 'requested'
  const [reasonsOpen, setReasonsOpen] = useState(false)

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<ReturnSummary>>({
    queryKey: ['returns', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/orders/returns?${grid.toParams({ status: tab !== 'all' ? tab : undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const returns = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const switchTab = (key: string) => grid.mutate(n => { if (key === 'requested') n.delete('tab'); else n.set('tab', key) })

  const columns: GridColumn<ReturnSummary>[] = [
    { key: 'returnNumber', header: 'İADE NO', frozen: true, lockVisible: true, sortable: true, minWidth: 140,
      cell: r => <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{r.returnNumber}</code> },
    { key: 'returnType', header: 'TİP', priority: 3, cell: r => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{r.returnType === 'refund' ? 'İade' : r.returnType}</span> },
    { key: 'refundAmount', header: 'TUTAR', sortable: true, align: 'right', priority: 1, filter: { type: 'number', label: 'Tutar' },
      cell: r => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{r.refundAmount.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺</span> },
    { key: 'refundStatus', header: 'GERİ ÖDEME', sortable: true, priority: 2,
      cell: r => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{r.refundMethod}{r.refundStatus ? ` · ${r.refundStatus}` : ''}</span> },
    { key: 'status', header: 'DURUM', lockVisible: true, sortable: true, priority: 1,
      cell: r => { const st = RETURN_STATUS_MAP[r.status] ?? { label: r.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> } },
    { key: 'cargoReturnCode', header: 'KARGO İADE KODU', priority: 3, defaultVisible: false, cell: r => <span className="text-xs font-mono" style={{ color: 'var(--text-s)' }}>{r.cargoReturnCode ?? '—'}</span> },
    { key: 'createdAt', header: 'TARİH', sortable: true, priority: 2, filter: { type: 'date', label: 'Tarih', quick: true },
      cell: r => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{new Date(r.createdAt).toLocaleDateString('tr-TR')}</span> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>İadeler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{totalCount.toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>
        <Button size="sm" variant="secondary" onClick={() => setReasonsOpen(true)}>İade Nedenleri</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => {
          const key = t.key === '' ? 'all' : t.key
          return (
            <button key={key} className={cn('stab', tab === key && 'active')} onClick={() => switchTab(key)}>{t.label}</button>
          )
        })}
      </div>

      <DataGrid<ReturnSummary>
        gridId="returns"
        grid={grid}
        columns={columns}
        extraFilters={RETURN_EXTRA_FILTERS}
        search={{ placeholder: 'İade no, kargo takip no, kargo iade kodu…' }}
        rows={returns}
        totalCount={totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={r => navigate(`/orders/returns/${r.id}`)}
        empty="İade bulunamadı."
        minWidth={760}
        export={{ endpoint: '/orders/returns/export', named: () => ({ status: tab !== 'all' ? tab : undefined }), fallbackFileName: 'iadeler.xlsx' }}
      />

      {reasonsOpen && <ReasonsModal onClose={() => setReasonsOpen(false)} />}
    </div>
  )
}
