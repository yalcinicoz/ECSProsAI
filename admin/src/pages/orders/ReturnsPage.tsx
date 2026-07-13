import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'

export const RETURN_STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  requested: { label: 'Talep Edildi', variant: 'warning' },
  approved:  { label: 'Onaylandı',    variant: 'warning' },
  received:  { label: 'Teslim Alındı', variant: 'success' },
  refunded:  { label: 'Geri Ödendi',  variant: 'success' },
  rejected:  { label: 'Reddedildi',   variant: 'danger' },
}

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

// ── İade listesi ──────────────────────────────────────────────────────────────
export function ReturnsPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState('requested')
  const [page, setPage] = useState(1)
  const [reasonsOpen, setReasonsOpen] = useState(false)

  const { data, isLoading } = useQuery<PagedResult<ReturnSummary>>({
    queryKey: ['returns', tab, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      return (await api.get(`/orders/returns?${params}`)).data.data
    },
  })

  const returns = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.ceil(totalCount / 20)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>İadeler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{totalCount} kayıt</p>
        </div>
        <Button size="sm" variant="secondary" onClick={() => setReasonsOpen(true)}>İade Nedenleri</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t.key} className={cn('stab', tab === t.key && 'active')}
            onClick={() => { setTab(t.key); setPage(1) }}>
            {t.label}
          </button>
        ))}
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['İADE NO', 'TİP', 'TUTAR', 'GERİ ÖDEME', 'DURUM', 'TARİH', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && returns.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>İade bulunamadı.</td></tr>
            )}
            {returns.map(r => {
              const st = RETURN_STATUS_MAP[r.status] ?? { label: r.status, variant: 'neutral' as const }
              return (
                <tr key={r.id} onClick={() => navigate(`/orders/returns/${r.id}`)}
                  className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{r.returnNumber}</code>
                  </td>
                  <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>
                    {r.returnType === 'refund' ? 'İade' : r.returnType}
                  </td>
                  <td className="px-4 py-3 text-sm font-medium" style={{ color: 'var(--text)' }}>
                    {r.refundAmount.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                  </td>
                  <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                    {r.refundMethod}{r.refundStatus ? ` · ${r.refundStatus}` : ''}
                  </td>
                  <td className="px-4 py-3"><Badge variant={st.variant}>{st.label}</Badge></td>
                  <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                    {new Date(r.createdAt).toLocaleDateString('tr-TR')}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>← Önceki</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki →</button>
        </div>
      )}

      {reasonsOpen && <ReasonsModal onClose={() => setReasonsOpen(false)} />}
    </div>
  )
}
