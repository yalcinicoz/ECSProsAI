// Müşteri İlişkileri — kayıt detayı (eski MusteriIliskileriYonetimiDetay): sol kayıt bilgileri + "Yeni İşlem"; sağ sekmeler
// Kayıt / Sipariş / Üye / Log — sayfadan ayrılmadan müşteri ve sipariş bilgisi. Açılışta POST open (okundu + bildirim
// "kayda girdi"); GET yan etkisiz. Bildirim izi: kim gördü, kim kayda girdi (tarih-saat-kullanıcı).
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { QuillEditor } from '@/components/QuillEditor'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/store/auth'
import { ORDER_STATUS_MAP } from '@/pages/orders/orderConstants'
import type { OrderSummary } from '@/pages/orders/OrdersPage'
import { KIND_LABEL, TYPE_LABEL, apiErrorMessage, fmtTarih, fmtTelefon, uploadTicketFile, useTicketSettings, useAdminUsers, usePlatformNames, type TicketDetail, type TicketActivity } from './ticketShared'

interface OrderDetail {
  id: string; orderNumber: string; status: string; paymentStatus: string; grandTotal: number; currencyCode: string; createdAt: string
  shippingRecipientName: string; shippingRecipientPhone: string; shippingAddressLine: string; internalNotes?: string | null
  items: { productName?: string; name?: string; sku?: string; quantity: number; unitPrice: number; total?: number; variantName?: string }[]
  payments: { method?: string; paymentMethod?: string; amount: number; status?: string }[]
}
interface MemberSummary { id: string; firstName: string; lastName: string; email?: string; phone?: string; isActive?: boolean; createdAt?: string; memberGroupId?: string }

function Ekler({ files }: { files: string[] }) {
  if (files.length === 0) return null
  return (
    <div className="flex flex-wrap gap-2 mt-2">
      {files.map((f) => /\.(jpe?g|png|webp|gif)$/i.test(f)
        ? <a key={f} href={f} target="_blank" rel="noreferrer"><img src={f} alt="" className="h-24 rounded-lg object-cover" style={{ border: '1px solid var(--border)' }} /></a>
        : <a key={f} href={f} target="_blank" rel="noreferrer" className="text-xs underline" style={{ color: 'var(--brand)' }}>📎 {f.split('/').pop()}</a>)}
    </div>
  )
}

function Satir({ l, v }: { l: string; v: React.ReactNode }) {
  return (
    <div className="flex gap-2 text-sm py-1" style={{ borderTop: '1px solid var(--border)' }}><span className="w-32 shrink-0 text-xs pt-0.5" style={{ color: 'var(--text-s)' }}>{l}</span><span style={{ color: 'var(--text)' }}>{v}</span></div>
  )
}

function IslemSatiri({ a }: { a: TicketActivity }) {
  return (
    <div className="flex gap-3 py-3" style={{ borderTop: '1px solid var(--border)' }}>
      <div className="w-40 shrink-0 text-xs" style={{ color: 'var(--text-s)' }}>
        <div className="font-semibold" style={{ color: 'var(--text)' }}>{a.userName}</div>
        <div>{fmtTarih(a.createdAt)}</div>
        {a.reads.length > 0 && <div className="mt-1" title={a.reads.map((r) => `${r.userName} · ${fmtTarih(r.readAt)}`).join('\n')}>👁 {a.reads.length} kişi gördü</div>}
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-sm prose-crm" style={{ color: 'var(--text)' }} dangerouslySetInnerHTML={{ __html: a.bodyHtml }} />
        <Ekler files={a.attachments} />
        {a.statusChanged && (
          <div className="mt-2 text-xs rounded-lg px-2 py-1 inline-block" style={{ background: a.isResolvedStatus ? '#22c55e18' : '#f59e0b18', color: a.isResolvedStatus ? '#15803d' : '#b45309' }}>
            Durum değiştirildi: <b>{a.previousStatusName}</b> → <b>{a.statusName}</b>
          </div>
        )}
        {a.taggedUserId && (
          <div className="mt-2 text-xs rounded-lg px-2 py-1 inline-block ml-1" style={{ background: '#3b82f618', color: '#1d4ed8' }}>
            🏷 <b>{a.taggedUserName}</b> — kayıt ile ilgilenebilir misiniz?
          </div>
        )}
      </div>
    </div>
  )
}

export function TicketDetailPage() {
  const { trackingNo } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const me = useAuthStore((s) => s.user)
  const { data: settings } = useTicketSettings(true)
  const { data: users = [] } = useAdminUsers()
  const { data: platformNames = {} } = usePlatformNames()
  const [tab, setTab] = useState<'kayit' | 'siparis' | 'uye' | 'log'>('kayit')
  const [modal, setModal] = useState(false)
  const openedFor = useRef<string | null>(null)

  const { data: t, isLoading, error } = useQuery<TicketDetail>({
    queryKey: ['ticket', trackingNo],
    queryFn: async () => (await api.get(`/crm/tickets/${trackingNo}`)).data.data,
    enabled: !!trackingNo,
  })

  // Kaydı açtım → okundu satırları + bildirimlerim "kayda girdi" (bir kez / kayıt)
  useEffect(() => {
    if (!t || openedFor.current === t.id) return
    openedFor.current = t.id
    api.post(`/crm/tickets/${t.id}/open`).then(() => {
      qc.invalidateQueries({ queryKey: ['ticket', trackingNo] })
      qc.invalidateQueries({ queryKey: ['ticket-notifications'] })
      qc.invalidateQueries({ queryKey: ['ticket-notifications-pending'] })
    }).catch(() => {})
  }, [t, trackingNo, qc])

  const { data: order } = useQuery<OrderDetail>({
    queryKey: ['order', t?.orderId],
    queryFn: async () => (await api.get(`/orders/${t!.orderId}`)).data.data,
    enabled: !!t?.orderId,
  })
  const { data: memberOrders } = useQuery<{ items: OrderSummary[]; totalCount: number }>({
    queryKey: ['member-orders', t?.memberId],
    queryFn: async () => (await api.get(`/orders?memberId=${t!.memberId}&pageSize=10`)).data.data,
    enabled: !!t?.memberId,
  })
  const { data: member } = useQuery<MemberSummary>({
    queryKey: ['member', t?.memberId],
    queryFn: async () => (await api.get(`/crm/members/${t!.memberId}`)).data.data,
    enabled: !!t?.memberId,
  })

  // kullanıcı adı haritası: işlemlerdeki anlık görüntüler + aktif kullanıcılar (bildirim izi userId taşır)
  const adlar = useMemo(() => {
    const m: Record<string, string> = {}
    users.forEach((u) => { m[u.id] = u.fullName })
    if (t) {
      if (t.createdByUserId) m[t.createdByUserId] = t.createdByName
      t.activities.forEach((a) => { if (a.userId) m[a.userId] = a.userName; if (a.taggedUserId && a.taggedUserName) m[a.taggedUserId] = a.taggedUserName; a.reads.forEach((r) => { m[r.userId] = r.userName }) })
    }
    return m
  }, [users, t])

  const hide = useMutation({
    mutationFn: async (hidden: boolean) => api.post(`/crm/tickets/${t!.id}/hidden`, hidden),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['ticket', trackingNo] }); qc.invalidateQueries({ queryKey: ['tickets'] }) },
  })

  if (isLoading) return <PageSpinner />
  if (error || !t) return (
    <div className="p-6"><div className="text-sm" style={{ color: '#dc2626' }}>Kayıt bulunamadı.</div>
      <button className="mt-2 text-sm underline" style={{ color: 'var(--brand)' }} onClick={() => navigate('/crm/tickets')}>← Listeye dön</button></div>
  )

  return (
    <div className="p-6">
      <div className="flex items-center gap-2 mb-4 text-sm" style={{ color: 'var(--text-s)' }}>
        <button className="underline" onClick={() => navigate('/crm/tickets')}>Müşteri İlişkileri</button><span>/</span><span className="font-mono">{t.trackingNo}</span>
        {t.isHidden && <Badge variant="neutral">gizli</Badge>}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Sol: kayıt bilgileri */}
        <div className="space-y-4">
          <div className="card p-4">
            <div className="text-lg font-bold" style={{ color: 'var(--text)' }}>{t.customerName || t.callerName || '—'}</div>
            <div className="text-sm mb-2" style={{ color: 'var(--text-m)' }}>{fmtTelefon(t.customerPhone || t.callerPhone)}</div>
            <Satir l="Arayan" v={<>{t.callerName || '—'}<br /><span className="text-xs" style={{ color: 'var(--text-s)' }}>{fmtTelefon(t.callerPhone)}</span></>} />
            <Satir l="Tür" v={<span style={{ color: t.type === 'complaint' ? '#ef4444' : '#3b82f6' }}>{TYPE_LABEL[t.type] ?? t.type}</span>} />
            <Satir l="Konu" v={t.subjectName} />
            <Satir l="Sipariş No" v={t.orderNumber ? <>{t.orderId ? <Link to={`/orders/${t.orderId}`} className="underline font-mono" style={{ color: 'var(--brand)' }}>{t.orderNumber}</Link> : <span className="font-mono">{t.orderNumber}</span>}{t.firmPlatformId && platformNames[t.firmPlatformId] && <span className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>({platformNames[t.firmPlatformId]})</span>}</> : '—'} />
            <Satir l="Takip No" v={<span className="font-mono">{t.trackingNo}</span>} />
            <Satir l="Kayıt" v={<>{t.createdByName}<br /><span className="text-xs" style={{ color: 'var(--text-s)' }}>{fmtTarih(t.createdAt)}</span></>} />
            <Satir l="Güncelleme" v={<>{t.updatedByName ?? '—'}<br /><span className="text-xs" style={{ color: 'var(--text-s)' }}>{fmtTarih(t.updatedAt)}</span></>} />
            <div className="mt-3 rounded-xl px-3 py-2 text-center font-semibold" style={{ background: `${t.statusColor}22`, color: t.statusColor }}>{t.statusName}</div>
          </div>
          <div className="card p-4 space-y-2">
            <Button className="w-full" onClick={() => setModal(true)}>Yeni İşlem Başlat</Button>
            <Button className="w-full" variant="secondary" size="sm" onClick={() => { if (window.confirm(t.isHidden ? 'Kayıt geri alınsın mı?' : 'Kayıt gizlensin mi? (listede yalnız "Gizlenenler" süzgeciyle görünür)')) hide.mutate(!t.isHidden) }} loading={hide.isPending}>
              {t.isHidden ? 'Gizlemeyi Geri Al' : 'Kaydı Gizle'}
            </Button>
          </div>
          {t.previousTickets.length > 0 && (
            <div className="card p-4">
              <h3 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>Müşterinin diğer kayıtları ({t.previousTickets.length})</h3>
              {t.previousTickets.map((p) => (
                <Link key={p.id} to={`/crm/tickets/${p.trackingNo}`} className="block text-xs py-1 hover:underline" style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
                  <span className="font-mono">{p.trackingNo}</span> · {p.subjectName} · <span style={{ color: p.statusColor }}>{p.statusName}</span> · <span style={{ color: 'var(--text-s)' }}>{fmtTarih(p.createdAt)}</span>
                </Link>
              ))}
            </div>
          )}
        </div>

        {/* Sağ: sekmeler */}
        <div className="lg:col-span-2 space-y-4">
          <div className="flex items-center gap-1" style={{ borderBottom: '1px solid var(--border)' }}>
            {([['kayit', 'Kayıt'], ['siparis', 'Sipariş'], ['uye', 'Üye'], ['log', 'Log']] as const).map(([k, l]) => (
              <button key={k} className={cn('stab', tab === k && 'active')} onClick={() => setTab(k)}>{l}</button>
            ))}
          </div>

          {tab === 'kayit' && (
            <>
              <div className="card p-4">
                <div className="text-xs mb-2" style={{ color: 'var(--text-s)' }}>Kayıt içeriği · {t.createdByName} · {fmtTarih(t.createdAt)}</div>
                <div className="text-sm prose-crm" style={{ color: 'var(--text)' }} dangerouslySetInnerHTML={{ __html: t.bodyHtml || '<i>İçerik yok.</i>' }} />
                <Ekler files={t.attachments} />
              </div>
              <div className="card p-4">
                <h3 className="text-sm font-semibold mb-1" style={{ color: 'var(--text)' }}>Yapılan İşlemler ({t.activities.length})</h3>
                {t.activities.length === 0 && <p className="text-xs" style={{ color: 'var(--text-s)' }}>Henüz işlem yapılmadı.</p>}
                {t.activities.map((a) => <IslemSatiri key={a.id} a={a} />)}
              </div>
            </>
          )}

          {tab === 'siparis' && (
            <div className="card p-4 space-y-3">
              {!t.orderNumber && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu kayıtta sipariş numarası yok.</p>}
              {t.orderNumber && !t.orderId && <p className="text-sm" style={{ color: '#b45309' }}>Sipariş <span className="font-mono">{t.orderNumber}</span> bu sistemde bulunamadı (eski sistem siparişi). Kayıt bilgileri anlık görüntüdür.</p>}
              {order && (
                <>
                  <div className="flex items-center justify-between">
                    <div>
                      <Link to={`/orders/${order.id}`} className="font-mono font-semibold underline" style={{ color: 'var(--brand)' }}>{order.orderNumber}</Link>
                      <span className="ml-2"><Badge variant={ORDER_STATUS_MAP[order.status]?.variant ?? 'neutral'}>{ORDER_STATUS_MAP[order.status]?.label ?? order.status}</Badge></span>
                    </div>
                    <div className="text-sm" style={{ color: 'var(--text)' }}><b>{order.grandTotal.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} {order.currencyCode}</b> · {fmtTarih(order.createdAt)}</div>
                  </div>
                  <div className="text-xs" style={{ color: 'var(--text-m)' }}>Teslimat: {order.shippingRecipientName} · {fmtTelefon(order.shippingRecipientPhone)} · {order.shippingAddressLine}</div>
                  <table className="w-full text-sm">
                    <thead><tr style={{ background: 'var(--surface2)' }}>{['Ürün', 'Adet', 'Birim', 'Tutar'].map((h) => <th key={h} className="px-2 py-1 text-left text-xs" style={{ color: 'var(--text-s)' }}>{h}</th>)}</tr></thead>
                    <tbody>{order.items.map((i, idx) => (
                      <tr key={idx} style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
                        <td className="px-2 py-1">{i.productName ?? i.name}{i.variantName ? ` · ${i.variantName}` : ''}{i.sku ? <span className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>{i.sku}</span> : null}</td>
                        <td className="px-2 py-1">{i.quantity}</td><td className="px-2 py-1">{i.unitPrice.toLocaleString('tr-TR')}</td><td className="px-2 py-1">{(i.total ?? i.unitPrice * i.quantity).toLocaleString('tr-TR')}</td>
                      </tr>))}</tbody>
                  </table>
                  {order.payments.length > 0 && <div className="text-xs" style={{ color: 'var(--text-m)' }}>Ödeme: {order.payments.map((p) => `${p.method ?? p.paymentMethod ?? ''} ${p.amount.toLocaleString('tr-TR')} ${p.status ?? ''}`).join(' · ')}</div>}
                  {order.internalNotes && <div className="text-xs rounded-lg p-2" style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>Sipariş notu: {order.internalNotes}</div>}
                </>
              )}
              {memberOrders && memberOrders.items.length > 0 && (
                <div>
                  <h4 className="text-xs font-semibold mt-2 mb-1" style={{ color: 'var(--text)' }}>Müşterinin siparişleri ({memberOrders.totalCount})</h4>
                  {memberOrders.items.map((o) => (
                    <Link key={o.id} to={`/orders/${o.id}`} className="block text-xs py-1 hover:underline" style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
                      <span className="font-mono">{o.orderNumber}</span> · {ORDER_STATUS_MAP[o.status]?.label ?? o.status} · {o.grandTotal.toLocaleString('tr-TR')} {o.currencyCode} · <span style={{ color: 'var(--text-s)' }}>{fmtTarih(o.createdAt)}</span>
                    </Link>
                  ))}
                </div>
              )}
            </div>
          )}

          {tab === 'uye' && (
            <div className="card p-4 space-y-3">
              {!t.memberId && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu kayıt bir üyeye bağlı değil{t.legacyMemberId ? ` (eski üye no ${t.legacyMemberId})` : ''}. Müşteri: <b style={{ color: 'var(--text)' }}>{t.customerName}</b> {fmtTelefon(t.customerPhone)}</p>}
              {member && (
                <div className="text-sm" style={{ color: 'var(--text)' }}>
                  <Link to={`/crm/members/${member.id}`} className="font-semibold underline" style={{ color: 'var(--brand)' }}>{member.firstName} {member.lastName}</Link>
                  <div className="text-xs mt-1" style={{ color: 'var(--text-m)' }}>{member.email ?? '—'} · {fmtTelefon(member.phone)} · {member.isActive === false ? 'pasif' : 'aktif'}{member.createdAt ? ` · üyelik ${fmtTarih(member.createdAt)}` : ''}</div>
                </div>
              )}
              <div>
                <h4 className="text-xs font-semibold mb-1" style={{ color: 'var(--text)' }}>Müşterinin diğer kayıtları ({t.previousTickets.length})</h4>
                {t.previousTickets.length === 0 && <p className="text-xs" style={{ color: 'var(--text-s)' }}>Başka kayıt yok.</p>}
                {t.previousTickets.map((p) => (
                  <Link key={p.id} to={`/crm/tickets/${p.trackingNo}`} className="block text-xs py-1 hover:underline" style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
                    <span className="font-mono">{p.trackingNo}</span> · {p.subjectName} · <span style={{ color: p.statusColor }}>{p.statusName}</span> · {p.createdByName} · <span style={{ color: 'var(--text-s)' }}>{fmtTarih(p.createdAt)}</span>
                  </Link>
                ))}
              </div>
            </div>
          )}

          {tab === 'log' && (
            <div className="space-y-4">
              <div className="card p-4">
                <h3 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>Bildirimler — kim gördü, kim kayda girdi</h3>
                {t.notifications.length === 0 && <p className="text-xs" style={{ color: 'var(--text-s)' }}>Bu kayıt için bildirim üretilmedi.</p>}
                {t.notifications.length > 0 && (
                  <table className="w-full text-xs">
                    <thead><tr style={{ background: 'var(--surface2)' }}>{['Kişi', 'Neden', 'Gönderildi', 'Gördü', 'Kayda girdi'].map((h) => <th key={h} className="px-2 py-1 text-left" style={{ color: 'var(--text-s)' }}>{h}</th>)}</tr></thead>
                    <tbody>{t.notifications.map((n, i) => (
                      <tr key={i} style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
                        <td className="px-2 py-1">{adlar[n.userId] ?? n.userId.slice(0, 8)}</td>
                        <td className="px-2 py-1">{KIND_LABEL[n.kind] ?? n.kind}</td>
                        <td className="px-2 py-1">{fmtTarih(n.sentAt)}</td>
                        <td className="px-2 py-1">{n.seenAt ? <span style={{ color: '#15803d' }}>{fmtTarih(n.seenAt)}</span> : <span style={{ color: '#ef4444' }}>görmedi</span>}</td>
                        <td className="px-2 py-1">{n.openedAt ? <span style={{ color: '#15803d' }}>{fmtTarih(n.openedAt)}</span> : <span className="font-semibold" style={{ color: '#ef4444' }}>{n.seenAt ? 'gördü, kayda girmedi' : 'girmedi'}</span>}</td>
                      </tr>))}</tbody>
                  </table>
                )}
              </div>
              <div className="card p-4">
                <h3 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>Kontrol edenler (işlem bazında okundu)</h3>
                <table className="w-full text-xs">
                  <thead><tr style={{ background: 'var(--surface2)' }}>{['İşlem', 'Personel', 'Tarih'].map((h) => <th key={h} className="px-2 py-1 text-left" style={{ color: 'var(--text-s)' }}>{h}</th>)}</tr></thead>
                  <tbody>{t.activities.flatMap((a) => a.reads.map((r, i) => (
                    <tr key={a.id + i} style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
                      <td className="px-2 py-1">{a.userName} · {fmtTarih(a.createdAt)}</td><td className="px-2 py-1">{r.userName}</td><td className="px-2 py-1">{fmtTarih(r.readAt)}</td>
                    </tr>)))}</tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      </div>

      {modal && settings && (
        <YeniIslemModal ticket={t} statuses={settings.statuses} users={users.filter((u) => u.id !== me?.id)} onClose={() => setModal(false)}
          onSaved={() => { setModal(false); qc.invalidateQueries({ queryKey: ['ticket', trackingNo] }); qc.invalidateQueries({ queryKey: ['tickets'] }) }} />
      )}
    </div>
  )
}

function YeniIslemModal({ ticket, statuses, users, onClose, onSaved }: {
  ticket: TicketDetail; statuses: { id: string; name: string; isHidden: boolean }[]; users: { id: string; fullName: string }[]; onClose: () => void; onSaved: () => void
}) {
  const [bodyHtml, setBodyHtml] = useState('')
  const [statusId, setStatusId] = useState(ticket.statusId)
  const [tagged, setTagged] = useState<string | null>(null)
  const [files, setFiles] = useState<string[]>([])
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')
  const save = useMutation({
    mutationFn: async () => api.post(`/crm/tickets/${ticket.id}/activities`, { bodyHtml, attachments: files, statusId, taggedUserId: tagged, taggedUserName: users.find((u) => u.id === tagged)?.fullName ?? null }),
    onSuccess: onSaved,
    onError: (e: unknown) => setError(apiErrorMessage(e, 'İşlem kaydedilemedi.')),
  })
  async function uploadFile(f: File) {
    setUploading(true); setError('')
    try { const url = await uploadTicketFile(f); setFiles((p) => [...p, url]) } catch (e) { setError(apiErrorMessage(e, 'Dosya yüklenemedi.')) } finally { setUploading(false) }
  }
  return (
    <Modal open onClose={onClose} title={`Yeni İşlem — ${ticket.trackingNo}`} size="lg"
      footer={<><Button variant="secondary" onClick={onClose}>Vazgeç</Button><Button onClick={() => save.mutate()} loading={save.isPending}>Kaydet</Button></>}>
      <div className="space-y-3">
        <div><label className="flbl">İçerik *</label><QuillEditor initialHtml="" onChange={setBodyHtml} /></div>
        <div className="grid grid-cols-2 gap-3">
          <div><label className="flbl">Kayıt Durumu</label>
            <select className="inp w-full" value={statusId} onChange={(e) => setStatusId(e.target.value)}>
              {statuses.filter((s) => !s.isHidden || s.id === ticket.statusId).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select></div>
          <div><label className="flbl">Etiketlenecek personel (isteğe bağlı)</label>
            <SearchableSelect value={tagged} onChange={setTagged} options={users.map((u) => ({ value: u.id, label: u.fullName }))} placeholder="— Seçilmedi —" clearable portal /></div>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {files.map((f) => <span key={f} className="text-xs px-2 py-1 rounded-lg" style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}>📎 {f.split('/').pop()} <button onClick={() => setFiles((p) => p.filter((x) => x !== f))}>×</button></span>)}
          <label className="px-3 py-1.5 rounded-lg text-xs cursor-pointer" style={{ border: '1px dashed var(--border)', color: 'var(--text-m)' }}>
            {uploading ? 'Yükleniyor…' : '+ Ek'}
            <input type="file" className="hidden" accept="image/jpeg,image/png,image/webp,image/gif,application/pdf" onChange={(e) => { const f = e.target.files?.[0]; if (f) uploadFile(f); e.target.value = '' }} />
          </label>
        </div>
        {error && <p className="text-sm" style={{ color: '#dc2626' }}>{error}</p>}
      </div>
    </Modal>
  )
}
