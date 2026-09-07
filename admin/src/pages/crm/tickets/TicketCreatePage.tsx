// Müşteri İlişkileri — yeni kayıt (eski MusteriIliskileriYonetimiYeniKayitFormModal). Konu seçilir → tür konudan türer,
// konunun zorunlu alanları işaretlenir; sipariş no sorgulanınca müşteri bilgisi siparişten dolar; aynı numara birden
// fazla kanaldaysa kanal seçtirilir (K9); görsel gövdeye gömülmez, ek olarak yüklenir (K5).
import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { QuillEditor } from '@/components/QuillEditor'
import { FIELD_LABEL, apiErrorMessage, fmtTarih, fmtTelefon, uploadTicketFile, useTicketSettings, type OrderCandidate } from './ticketShared'

function Lbl({ req, k, children }: { req: Set<string>; k: string; children: React.ReactNode }) {
  return <label className="flbl">{children}{req.has(k) && <span style={{ color: '#ef4444' }}> *</span>}</label>
}

export function TicketCreatePage() {
  const navigate = useNavigate()
  const [sp] = useSearchParams()
  const { data: settings } = useTicketSettings()
  const [subjectId, setSubjectId] = useState('')
  const [callerName, setCallerName] = useState('')
  const [callerPhone, setCallerPhone] = useState('')
  const [orderNumber, setOrderNumber] = useState(sp.get('orderNumber') ?? '')
  const [candidates, setCandidates] = useState<OrderCandidate[] | null>(null)
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null)
  const [customerName, setCustomerName] = useState('')
  const [customerPhone, setCustomerPhone] = useState('')
  const [bodyHtml, setBodyHtml] = useState('')
  const [files, setFiles] = useState<string[]>([])
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')
  const [looking, setLooking] = useState(false)

  const subject = settings?.subjects.find((s) => s.id === subjectId)
  const req = new Set(subject?.requiredFields ?? [])
  const selected = candidates?.find((c) => c.orderId === selectedOrderId) ?? (candidates?.length === 1 ? candidates[0] : null)

  async function siparisSorgula(num = orderNumber) {
    const n = num.trim(); if (!n) { setCandidates(null); return }
    setLooking(true); setError('')
    try {
      const list = (await api.get('/crm/tickets/order-lookup', { params: { number: n } })).data.data as OrderCandidate[]
      setCandidates(list); setSelectedOrderId(list.length === 1 ? list[0].orderId : null)
      if (list.length === 1) { setCustomerName(list[0].customerName); setCustomerPhone(list[0].customerPhone) }
    } catch (e) { setError(apiErrorMessage(e, 'Sipariş sorgulanamadı.')) } finally { setLooking(false) }
  }
  useEffect(() => { if (sp.get('orderNumber')) void siparisSorgula(sp.get('orderNumber')!) /* eslint-disable-line react-hooks/exhaustive-deps */ }, [])

  async function uploadFile(f: File) {
    setUploading(true); setError('')
    try { const url = await uploadTicketFile(f); setFiles((p) => [...p, url]) } catch (e) { setError(apiErrorMessage(e, 'Dosya yüklenemedi.')) } finally { setUploading(false) }
  }

  const create = useMutation({
    mutationFn: async () => (await api.post('/crm/tickets', {
      subjectId, callerName, callerPhone, orderNumber: orderNumber.trim() || null,
      orderId: selected?.orderId ?? null, firmPlatformId: selected?.firmPlatformId ?? null,
      memberId: selected?.memberId ?? null, legacyMemberId: selected?.legacyMemberId ?? null,
      customerName: selected?.customerName ?? customerName, customerPhone: selected?.customerPhone ?? customerPhone,
      bodyHtml, attachments: files,
    })).data.data as { id: string; trackingNo: number },
    onSuccess: (d) => navigate(`/crm/tickets/${d.trackingNo}`),
    onError: (e: unknown) => {
      const data = (e as { response?: { data?: { code?: string; data?: OrderCandidate[] } } })?.response?.data
      if (data?.code === 'channel_required' && data.data) { setCandidates(data.data); setSelectedOrderId(null) }
      setError(apiErrorMessage(e, 'Kayıt açılamadı.'))
    },
  })

  return (
    <div className="p-6 max-w-4xl">
      <div className="flex items-center gap-2 mb-4 text-sm" style={{ color: 'var(--text-s)' }}>
        <button className="underline" onClick={() => navigate('/crm/tickets')}>Müşteri İlişkileri</button><span>/</span><span>Yeni Kayıt</span>
      </div>
      <div className="card p-5 space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <Lbl req={req} k="subject">Konu Başlığı <span style={{ color: '#ef4444' }}>*</span></Lbl>
            <select className="inp w-full" value={subjectId} onChange={(e) => setSubjectId(e.target.value)}>
              <option value="">— Konu seçin —</option>
              {(['complaint', 'request'] as const).map((t) => (
                <optgroup key={t} label={t === 'complaint' ? 'Şikayet' : 'Talep'}>
                  {(settings?.subjects ?? []).filter((s) => s.type === t).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                </optgroup>
              ))}
            </select>
            {subject && <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Tür: <b style={{ color: subject.type === 'complaint' ? '#ef4444' : '#3b82f6' }}>{subject.type === 'complaint' ? 'Şikayet' : 'Talep'}</b>
              {subject.requiredFields.length > 0 && <> · Zorunlu: {subject.requiredFields.map((f) => FIELD_LABEL[f] ?? f).join(', ')}</>}
            </p>}
          </div>
          <div><Lbl req={req} k="callerName">Arayan Ad Soyad</Lbl><input className="inp w-full" value={callerName} onChange={(e) => setCallerName(e.target.value)} /></div>
          <div><Lbl req={req} k="callerPhone">Arayan Telefon</Lbl><input className="inp w-full" value={callerPhone} onChange={(e) => setCallerPhone(e.target.value)} placeholder="05xx xxx xx xx" /></div>
          <div className="col-span-2">
            <Lbl req={req} k="orderNumber">Sipariş No</Lbl>
            <div className="flex gap-2">
              <input className="inp font-mono flex-1" value={orderNumber} onChange={(e) => { setOrderNumber(e.target.value); setCandidates(null) }} onKeyDown={(e) => e.key === 'Enter' && siparisSorgula()} placeholder="ORD-… ya da eski sistem numarası" />
              <Button variant="secondary" size="sm" onClick={() => siparisSorgula()} loading={looking}>Sorgula</Button>
            </div>
            {candidates && candidates.length === 0 && (
              <div className="mt-2 text-xs rounded-lg p-2" style={{ background: '#f59e0b15', color: '#b45309' }}>
                Bu numarayla sipariş bulunamadı (eski sistem siparişi olabilir). Kayıt yine açılır; müşteri bilgisini elle girin.
              </div>
            )}
            {candidates && candidates.length > 1 && (
              <div className="mt-2 rounded-lg p-2 space-y-1" style={{ background: '#3b82f612', border: '1px solid #3b82f655' }}>
                <div className="text-xs font-semibold" style={{ color: '#1d4ed8' }}>Bu numara birden fazla kanalda bulundu — kanal seçin:</div>
                {candidates.map((c) => (
                  <label key={c.orderId} className="flex items-center gap-2 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
                    <input type="radio" name="kanal" checked={selectedOrderId === c.orderId} onChange={() => { setSelectedOrderId(c.orderId); setCustomerName(c.customerName); setCustomerPhone(c.customerPhone) }} />
                    <b>{c.platformName}</b> · {c.orderNumber} · {fmtTarih(c.createdAt)} · {c.customerName} {fmtTelefon(c.customerPhone)}
                  </label>
                ))}
              </div>
            )}
            {selected && (
              <div className="mt-2 text-xs rounded-lg p-2" style={{ background: '#22c55e15', color: '#15803d' }}>
                Sipariş bulundu: <b>{selected.platformName}</b> · {selected.orderNumber} · {fmtTarih(selected.createdAt)} · {selected.grandTotal.toLocaleString('tr-TR')} ₺ — Müşteri: <b>{selected.customerName}</b> {fmtTelefon(selected.customerPhone)}
              </div>
            )}
          </div>
          {(!selected) && (
            <>
              <div><label className="flbl">Müşteri Ad Soyad</label><input className="inp w-full" value={customerName} onChange={(e) => setCustomerName(e.target.value)} placeholder="sipariş bulunursa otomatik dolar" /></div>
              <div><label className="flbl">Müşteri Telefon</label><input className="inp w-full" value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} /></div>
            </>
          )}
        </div>
        <div>
          <Lbl req={req} k="body">İçerik</Lbl>
          <QuillEditor initialHtml="" onChange={setBodyHtml} />
        </div>
        <div>
          <Lbl req={req} k="image">Ekler (görsel / PDF)</Lbl>
          <div className="flex items-center gap-2 flex-wrap">
            {files.map((f) => (
              <span key={f} className="text-xs flex items-center gap-1 px-2 py-1 rounded-lg" style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}>
                <a href={f} target="_blank" rel="noreferrer" className="underline">📎 {f.split('/').pop()}</a>
                <button onClick={() => setFiles((p) => p.filter((x) => x !== f))} title="kaldır">×</button>
              </span>
            ))}
            <label className="px-3 py-1.5 rounded-lg text-xs cursor-pointer" style={{ border: '1px dashed var(--border)', color: 'var(--text-m)' }}>
              {uploading ? 'Yükleniyor…' : '+ Ek yükle'}
              <input type="file" className="hidden" accept="image/jpeg,image/png,image/webp,image/gif,application/pdf" onChange={(e) => { const f = e.target.files?.[0]; if (f) uploadFile(f); e.target.value = '' }} />
            </label>
          </div>
        </div>
        {error && <p className="text-sm" style={{ color: '#dc2626' }}>{error}</p>}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={() => navigate('/crm/tickets')}>Vazgeç</Button>
          <Button onClick={() => create.mutate()} loading={create.isPending} disabled={!subjectId}>Kaydı Aç</Button>
        </div>
      </div>
    </div>
  )
}
