import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import type { ReturnReason } from './ReturnsPage'
import { RETURN_STATUS_MAP, RETURN_TYPE_MAP, REFUND_STATUS_MAP, REFUND_NA_REASON_MAP, REFUND_METHOD_MAP } from './orderConstants'

interface ReturnItem {
  id: string
  orderItemId: string
  variantId: string
  quantity: number
  returnReasonId: string
  customerNotes?: string
  unitRefundAmount: number
  totalRefundAmount: number
  status: string
  inspectionResult?: string
  inspectionNotes?: string
  stockAlreadyIn?: boolean
}

interface ReturnRefund {
  id: string
  refundMethod: string
  amount: number
  status: string
  processedAt?: string
}

interface ReturnDetail {
  id: string
  returnNumber: string
  orderId: string
  memberId: string
  returnType: string
  customerNotes?: string
  status: string
  returnTrackingNumber?: string
  returnCargoSentAt?: string
  returnCargoReceivedAt?: string
  inspectionNotes?: string
  inspectionCompletedAt?: string
  refundMethod: string
  refundStatus: string
  refundAmount: number
  createdAt: string
  items: ReturnItem[]
  refunds: ReturnRefund[]
  cargoReturnCode?: string
  imageUrls?: string[]
  // İade planı (2026-09-10): geri ödeme yok nedeni + şu an ödenebilecek üst sınır (IadeOdemeKurali)
  refundNotApplicableReason?: string | null
  refundUpperLimit: number
  refundIban?: string | null          // 15.4g
  refundAccountHolder?: string | null
}

interface OrderInfo {
  orderNumber: string
  items: { id: string; productName: string; variantInfo: string; sku: string }[]
}

interface Warehouse { id: string; code: string; nameI18n?: Record<string, string> }

function money(v: number) {
  return `${v.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺`
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card p-4">
      <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text)' }}>{title}</h2>
      {children}
    </div>
  )
}

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  if (value === undefined || value === null || value === '') return null
  return (
    <div className="flex gap-2 text-sm py-0.5">
      <span className="shrink-0 w-36" style={{ color: 'var(--text-s)' }}>{label}</span>
      <span style={{ color: 'var(--text)' }}>{value}</span>
    </div>
  )
}

export function ReturnDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [rejectOpen, setRejectOpen] = useState(false)
  const [receiveOpen, setReceiveOpen] = useState(false)
  const [refundOpen, setRefundOpen] = useState(false)
  const [rejectReason, setRejectReason] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [inspectionNotes, setInspectionNotes] = useState('')
  const [refundMethod, setRefundMethod] = useState('')
  const [refundAmount, setRefundAmount] = useState('')
  const [iban, setIban] = useState('')
  const [accountHolder, setAccountHolder] = useState('')
  const [actionError, setActionError] = useState('')

  const { data: ret, isLoading } = useQuery<ReturnDetail>({
    queryKey: ['return-detail', id],
    queryFn: async () => (await api.get(`/orders/returns/${id}`)).data.data,
    enabled: !!id,
  })

  const { data: order } = useQuery<OrderInfo>({
    queryKey: ['order-detail', ret?.orderId],
    queryFn: async () => (await api.get(`/orders/${ret!.orderId}`)).data.data,
    enabled: !!ret?.orderId,
  })

  const { data: reasons = [] } = useQuery<ReturnReason[]>({
    queryKey: ['return-reasons'],
    queryFn: async () => (await api.get('/lookup/types/return_reason/values?activeOnly=false')).data.data,
  })

  const { data: warehouses = [] } = useQuery<Warehouse[]>({
    queryKey: ['warehouses', true],
    queryFn: async () => (await api.get('/inventory/warehouses')).data.data,
    enabled: receiveOpen,
  })

  function invalidate() {
    queryClient.invalidateQueries({ queryKey: ['return-detail', id] })
    queryClient.invalidateQueries({ queryKey: ['returns'] })
  }

  function onActionError(e: unknown) {
    const err = e as { response?: { data?: { error?: string } } }
    setActionError(err.response?.data?.error ?? 'İşlem başarısız oldu.')
  }

  const approve = useMutation({
    mutationFn: async () => { await api.post(`/orders/returns/${id}/approve`, {}) },
    onSuccess: invalidate,
    onError: onActionError,
  })
  const reject = useMutation({
    mutationFn: async () => { await api.patch(`/orders/returns/${id}/reject`, { reason: rejectReason }) },
    onSuccess: () => { invalidate(); setRejectOpen(false) },
    onError: onActionError,
  })
  const receive = useMutation({
    mutationFn: async () => {
      await api.post(`/orders/returns/${id}/receive`, { warehouseId, inspectionNotes: inspectionNotes || null })
    },
    onSuccess: () => { invalidate(); setReceiveOpen(false) },
    onError: onActionError,
  })
  const saveBank = useMutation({
    mutationFn: async () => { await api.put(`/orders/returns/${id}/refund-bank`, { iban: iban.trim() || null, accountHolder: accountHolder.trim() || null }) },
    onSuccess: invalidate,
    onError: onActionError,
  })
  const refund = useMutation({
    mutationFn: async () => {
      await api.post(`/orders/returns/${id}/refund`, {
        refundMethod: refundMethod || ret!.refundMethod,
        amount: parseFloat(refundAmount) || ret!.refundAmount,
        iban: iban.trim() || null, accountHolder: accountHolder.trim() || null,   // 15.4g: havalede zorunlu
      })
    },
    onSuccess: () => { invalidate(); setRefundOpen(false) },
    onError: onActionError,
  })

  if (isLoading || !ret) return <PageSpinner />

  const st = RETURN_STATUS_MAP[ret.status] ?? { label: ret.status, variant: 'neutral' as const }
  const tip = RETURN_TYPE_MAP[ret.returnType]
  const geriOdemeYok = ret.refundStatus === 'not_applicable'
  const rs = REFUND_STATUS_MAP[ret.refundStatus]
  const ustSinir = ret.refundUpperLimit ?? 0
  const reasonName = (rid: string) =>
    reasons.find(r => r.id === rid)?.nameI18n?.['tr'] ?? '—'
  const orderItem = (oid: string) => order?.items?.find(i => i.id === oid)

  return (
    <div className="p-6 max-w-4xl">
      {/* Başlık + aksiyonlar */}
      <div className="flex flex-wrap items-center gap-3 mb-1">
        <button onClick={() => navigate('/orders/returns')} className="text-sm" style={{ color: 'var(--text-s)' }}>←</button>
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{ret.returnNumber}</h1>
        <Badge variant={st.variant}>{st.label}</Badge>
        {tip && <Badge variant={tip.variant}>{tip.label}</Badge>}
        <div className="ml-auto flex flex-wrap gap-2">
          {ret.status === 'requested' && (
            <>
              <Button size="sm" onClick={() => { setActionError(''); approve.mutate() }} loading={approve.isPending}>Onayla</Button>
              <Button size="sm" variant="danger" onClick={() => { setActionError(''); setRejectOpen(true) }}>Reddet</Button>
            </>
          )}
          {ret.status === 'approved' && (
            <Button size="sm" onClick={() => { setActionError(''); setReceiveOpen(true) }}>Teslim Al</Button>
          )}
          {ret.status === 'received' && !geriOdemeYok && (
            <Button size="sm" onClick={() => {
              setActionError('')
              setRefundMethod(ret.refundMethod)
              setIban(ret.refundIban ?? ''); setAccountHolder(ret.refundAccountHolder ?? '')
              // Varsayılan: talep tutarı, üst sınırla kırpılmış (tutar üst sınırı aşamaz — E9)
              setRefundAmount(String(ustSinir > 0 ? Math.min(ret.refundAmount, ustSinir) : ret.refundAmount))
              setRefundOpen(true)
            }}>Geri Ödeme Yap</Button>
          )}
        </div>
      </div>
      <p className="text-sm mb-1" style={{ color: 'var(--text-s)' }}>
        Sipariş: <Link to={`/orders/${ret.orderId}`} className="underline" style={{ color: 'var(--brand)' }}>
          {order?.orderNumber ?? ret.orderId}
        </Link>
        {' · '}{new Date(ret.createdAt).toLocaleString('tr-TR')}
      </p>
      {actionError && <p className="text-sm mb-3 text-red-500">{actionError}</p>}

      <div className="space-y-4 mt-4">
        {tip?.label === 'Teslimatsız İade' && (
          <div className="text-sm rounded-lg px-3 py-2" style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
            Teslimatsız İade: paket müşteriye ulaşmadı / kabul edilmedi ya da faturalı sipariş kargoya verilmedi. Sipariş
            &quot;Teslimatsız İade&quot; durumundadır; fatura iptal edildi. Ürün depoya ulaşınca <b>Teslim Al</b> ile stoğa alın
            {ret.items.some(i => i.stockAlreadyIn) && ' (kargoya verilmemişti — stok zaten depoda, teslim alma stok girişi yapmaz)'}.
          </div>
        )}

        <Section title="Geri Ödeme">
          {geriOdemeYok ? (
            <div className="flex items-start gap-2 text-sm" style={{ color: 'var(--text-m)' }}>
              <Badge variant="neutral">Geri ödeme yok</Badge>
              <span title={REFUND_NA_REASON_MAP[ret.refundNotApplicableReason ?? ''] ?? ret.refundNotApplicableReason ?? ''}>
                {REFUND_NA_REASON_MAP[ret.refundNotApplicableReason ?? ''] ?? ret.refundNotApplicableReason ?? 'Müşteriye para iadesi hesaplanmaz.'}
                {' '}— teslim alma sonrası iade kendiliğinden kapanır.
              </span>
            </div>
          ) : (
            <>
              <InfoRow label="Geri Ödeme Tutarı" value={<b>{money(ret.refundAmount)}</b>} />
              <InfoRow label="Geri Ödeme Yöntemi" value={REFUND_METHOD_MAP[ret.refundMethod] ?? ret.refundMethod} />
              <InfoRow label="Geri Ödeme Durumu" value={rs ? <Badge variant={rs.variant}>{rs.label}</Badge> : ret.refundStatus} />
              {ret.refundStatus === 'pending' && (
                <InfoRow label="Ödenebilir Üst Sınır" value={ustSinir > 0 ? money(ustSinir) : 'Tahsilat bulunamadı — geri ödeme yapılamaz'} />
              )}
              {/* 15.4g: havale bilgisi (eski "Müşteriye Ödemeler" IBAN/alıcı) */}
              <InfoRow label="IBAN" value={ret.refundIban ? <code className="text-xs">{ret.refundIban}</code> : (ret.refundMethod === 'bank_transfer' ? <span className="text-red-500">girilmedi — havale için gerekli</span> : undefined)} />
              <InfoRow label="Hesap Sahibi" value={ret.refundAccountHolder} />
            </>
          )}
        </Section>

        <Section title="Talep Bilgisi">
          <InfoRow label="Kargo İade Kodu" value={ret.cargoReturnCode && <code className="text-xs">{ret.cargoReturnCode}</code>} />
          <InfoRow label="İade Kargo Takip" value={ret.returnTrackingNumber} />
          <InfoRow label="Kargoya Verildi" value={ret.returnCargoSentAt && new Date(ret.returnCargoSentAt).toLocaleString('tr-TR')} />
          <InfoRow label="Depoya Ulaştı" value={ret.returnCargoReceivedAt && new Date(ret.returnCargoReceivedAt).toLocaleString('tr-TR')} />
          <InfoRow label="Müşteri Notu" value={ret.customerNotes} />
        </Section>

        <Section title={`Kalemler (${ret.items.length})`}>
          <table className="w-full text-sm">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                {['ÜRÜN', 'ADET', 'NEDEN', 'TUTAR'].map(h => (
                  <th key={h} className="text-left pb-2 text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {ret.items.map(i => {
                const oi = orderItem(i.orderItemId)
                return (
                  <tr key={i.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="py-2 pr-2">
                      <div style={{ color: 'var(--text)' }}>{oi?.productName ?? i.variantId}</div>
                      <div className="text-xs" style={{ color: 'var(--text-s)' }}>
                        {oi ? `${oi.sku}${oi.variantInfo ? ` · ${oi.variantInfo}` : ''}` : ''}
                      </div>
                    </td>
                    <td className="py-2" style={{ color: 'var(--text-m)' }}>{i.quantity}</td>
                    <td className="py-2">
                      <div style={{ color: 'var(--text-m)' }}>{reasonName(i.returnReasonId)}</div>
                      {i.customerNotes && (
                        <div className="text-xs" style={{ color: 'var(--text-s)' }}>{i.customerNotes}</div>
                      )}
                    </td>
                    <td className="py-2 font-medium" style={{ color: 'var(--text)' }}>{money(i.totalRefundAmount)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Section>

        {(ret.imageUrls?.length ?? 0) > 0 && (
          <Section title="Talep Görselleri">
            <div className="flex flex-wrap gap-2">
              {ret.imageUrls!.map((u, i) => (
                <a key={i} href={u} target="_blank" rel="noreferrer">
                  <img src={u} alt={`İade görseli ${i + 1}`} className="w-24 h-24 object-cover rounded-lg"
                    style={{ border: '1px solid var(--border)' }} />
                </a>
              ))}
            </div>
          </Section>
        )}

        {(ret.inspectionNotes || ret.inspectionCompletedAt) && (
          <Section title="Muayene">
            <InfoRow label="Not" value={ret.inspectionNotes} />
            <InfoRow label="Tamamlandı" value={ret.inspectionCompletedAt && new Date(ret.inspectionCompletedAt).toLocaleString('tr-TR')} />
          </Section>
        )}

        {ret.refunds.length > 0 && (
          <Section title="Geri Ödemeler">
            {ret.refunds.map(r => (
              <div key={r.id} className="flex items-center gap-3 text-sm py-1.5" style={{ borderBottom: '1px solid var(--border)' }}>
                <span style={{ color: 'var(--text)' }}>{REFUND_METHOD_MAP[r.refundMethod] ?? r.refundMethod}</span>
                <span className="font-medium" style={{ color: 'var(--text)' }}>{money(r.amount)}</span>
                <span className="text-xs" style={{ color: 'var(--text-s)' }}>{r.status}</span>
                {r.processedAt && (
                  <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
                    {new Date(r.processedAt).toLocaleString('tr-TR')}
                  </span>
                )}
              </div>
            ))}
          </Section>
        )}
      </div>

      {/* Reddet */}
      <Modal open={rejectOpen} onClose={() => setRejectOpen(false)} title="İadeyi Reddet">
        <label className="flbl">Red Nedeni <span className="text-red-500">*</span></label>
        <textarea className="ta" rows={3} value={rejectReason} onChange={e => setRejectReason(e.target.value)}
          placeholder="Müşteriye gösterilecek red nedeni" />
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setRejectOpen(false)}>Vazgeç</Button>
          <Button variant="danger" onClick={() => reject.mutate()} loading={reject.isPending}
            disabled={!rejectReason.trim()}>Reddet</Button>
        </div>
      </Modal>

      {/* Teslim Al */}
      <Modal open={receiveOpen} onClose={() => setReceiveOpen(false)} title="İadeyi Teslim Al">
        <p className="text-sm mb-3" style={{ color: 'var(--text-m)' }}>
          Teslim alma, seçilen depoda stok miktarını geri yükler.
        </p>
        <label className="flbl">Depo <span className="text-red-500">*</span></label>
        <select className="inp" value={warehouseId} onChange={e => setWarehouseId(e.target.value)}>
          <option value="">Depo seçin</option>
          {warehouses.map(w => (
            <option key={w.id} value={w.id}>{w.nameI18n?.['tr'] ?? w.code}</option>
          ))}
        </select>
        <label className="flbl mt-3">Muayene Notu <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
        <textarea className="ta" rows={2} value={inspectionNotes} onChange={e => setInspectionNotes(e.target.value)} />
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setReceiveOpen(false)}>Vazgeç</Button>
          <Button onClick={() => receive.mutate()} loading={receive.isPending} disabled={!warehouseId}>Teslim Al</Button>
        </div>
      </Modal>

      {/* Geri Ödeme */}
      <Modal open={refundOpen} onClose={() => setRefundOpen(false)} title="Geri Ödeme Yap">
        <div className="space-y-3">
          <div>
            <label className="flbl">Yöntem</label>
            <select className="inp" value={refundMethod} onChange={e => setRefundMethod(e.target.value)}>
              {[...new Set([ret.refundMethod, 'card_refund', 'wallet', 'bank_transfer', 'cash'])].filter(m => m && m !== 'none').map(m => (
                <option key={m} value={m}>{REFUND_METHOD_MAP[m] ?? m}</option>
              ))}
            </select>
          </div>
          {refundMethod === 'bank_transfer' && (
            <div className="grid grid-cols-2 gap-2">
              <div><label className="flbl">IBAN <span className="text-red-500">*</span></label>
                <input className="inp font-mono" value={iban} onChange={e => setIban(e.target.value.toUpperCase())} placeholder="TR.." /></div>
              <div><label className="flbl">Hesap Sahibi</label>
                <input className="inp" value={accountHolder} onChange={e => setAccountHolder(e.target.value)} /></div>
              <div className="col-span-2 flex justify-end">
                <Button size="sm" variant="ghost" onClick={() => saveBank.mutate()} loading={saveBank.isPending} disabled={!iban.trim()}>Banka bilgisini kaydet (ödemesiz)</Button>
              </div>
            </div>
          )}
          <div>
            <label className="flbl">Tutar</label>
            <input type="number" step="0.01" min="0" max={ustSinir > 0 ? ustSinir : undefined} className="inp" value={refundAmount}
              onChange={e => setRefundAmount(e.target.value)} />
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Talep tutarı: {money(ret.refundAmount)}{ustSinir > 0 && <> · Üst sınır (tahsil edilen kalan): <b>{money(ustSinir)}</b></>}
            </p>
            {ustSinir > 0 && parseFloat(refundAmount) > ustSinir + 0.005 && (
              <p className="text-xs mt-1 text-red-500">Tutar üst sınırı aşamaz.</p>
            )}
          </div>
        </div>
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setRefundOpen(false)}>Vazgeç</Button>
          <Button onClick={() => refund.mutate()} loading={refund.isPending}
            disabled={!(parseFloat(refundAmount) > 0) || (ustSinir > 0 && parseFloat(refundAmount) > ustSinir + 0.005) || (refundMethod === 'bank_transfer' && iban.trim().length < 15)}>Geri Ödeme Yap</Button>
        </div>
      </Modal>
    </div>
  )
}
