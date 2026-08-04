import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'

// O1 (2026-08-04): Bildirim Şablonları — sipariş onayı SMS/e-posta şablonları +
// sipariş onay politikası (kapıda/kart/link ömrü, kanal bazlı). Şablon değişkenleri
// gönderim anında sunucuda doldurulur; buradaki önizleme örnek verilerledir.

const DEGISKENLER = [
  { key: '{ad}', desc: 'Alıcı adı' },
  { key: '{soyad}', desc: 'Alıcı soyadı' },
  { key: '{siparisNo}', desc: 'Sipariş numarası' },
  { key: '{tutar}', desc: 'Sipariş tutarı' },
  { key: '{link}', desc: 'Onay bağlantısı' },
  { key: '{sure}', desc: 'Link ömrü (saat)' },
]

const ORNEK: Record<string, string> = {
  '{ad}': 'Ayşe', '{soyad}': 'Yılmaz', '{siparisNo}': 'MIS0000042',
  '{tutar}': '1.249,90', '{link}': 'https://site/o/abc123…', '{sure}': '24',
}

// Sunucudaki gömülü varsayılanlarla birebir (OrderConfirmationService)
const VARSAYILAN_SMS = 'Sayin {ad} {soyad}, {siparisNo} nolu siparisinizi onaylamak icin: {link} (Link {sure} saat gecerlidir.)'
const VARSAYILAN_EMAIL_KONU = '{siparisNo} — Siparişinizi Onaylayın'
const VARSAYILAN_EMAIL_BODY = '<p>Sayın {ad} {soyad},</p><p>{siparisNo} numaralı {tutar} TL tutarındaki siparişinizi onaylamak için <a href="{link}">buraya tıklayın</a>.</p><p>Bağlantı {sure} saat geçerlidir. Siparişi siz vermediyseniz bu iletiyi yok sayabilirsiniz.</p>'

interface TemplateDto {
  id: string; typeCode: string; channel: string; languageCode: string
  subject?: string | null; body: string; isActive: boolean
}
interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; code?: string; nameI18n?: Record<string, string>; settings?: Record<string, unknown> }

const tr = (m?: Record<string, string> | null) => m?.['tr'] ?? Object.values(m ?? {})[0] ?? ''

function onizle(metin: string) {
  let s = metin
  for (const [k, v] of Object.entries(ORNEK)) s = s.split(k).join(v)
  return s
}

export function NotificationTemplatesPage() {
  const queryClient = useQueryClient()
  const [err, setErr] = useState('')
  const [bilgi, setBilgi] = useState('')

  const { data: templates = [] } = useQuery<TemplateDto[]>({
    queryKey: ['notification-templates', 'siparis_onay'],
    queryFn: async () => (await api.get('/core/notification-templates?typeCode=siparis_onay')).data.data ?? [],
  })

  const smsKayit = templates.find(t => t.channel === 'sms')
  const emailKayit = templates.find(t => t.channel === 'email')

  const [smsBody, setSmsBody] = useState<string | null>(null)
  const [emailKonu, setEmailKonu] = useState<string | null>(null)
  const [emailBody, setEmailBody] = useState<string | null>(null)
  const sms = smsBody ?? smsKayit?.body ?? VARSAYILAN_SMS
  const eKonu = emailKonu ?? emailKayit?.subject ?? VARSAYILAN_EMAIL_KONU
  const eBody = emailBody ?? emailKayit?.body ?? VARSAYILAN_EMAIL_BODY

  const kaydet = useMutation({
    mutationFn: async (p: { channel: string; subject?: string; body: string }) => {
      await api.put('/core/notification-templates', {
        typeCode: 'siparis_onay', channel: p.channel, languageCode: 'tr',
        subject: p.subject ?? null, body: p.body, isActive: true,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notification-templates'] })
      setBilgi('Şablon kaydedildi.'); setErr('')
      window.setTimeout(() => setBilgi(''), 2500)
    },
    onError: (e) => setErr((e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'Kaydedilemedi.'),
  })

  // ── Onay politikası (kanal bazlı) ──
  const { data: firms = [] } = useQuery<Firm[]>({
    queryKey: ['firms'], queryFn: async () => (await api.get('/core/firms')).data.data ?? [],
  })
  const [firmId, setFirmId] = useState('')
  const seciliFirm = firmId || firms[0]?.id || ''
  const { data: channels = [] } = useQuery<Channel[]>({
    queryKey: ['firm-platforms', seciliFirm],
    queryFn: async () => (await api.get(`/core/firms/${seciliFirm}/platforms`)).data.data ?? [],
    enabled: !!seciliFirm,
  })
  const [platformId, setPlatformId] = useState('')
  const seciliPlatform = channels.find(c => c.id === (platformId || channels[0]?.id))
  const politika = (seciliPlatform?.settings?.['orderConfirmPolicy'] ?? {}) as { cod?: string; card?: string }
  const [cod, setCod] = useState<string | null>(null)
  const [card, setCard] = useState<string | null>(null)
  const [saat, setSaat] = useState<string | null>(null)
  const codDeger = cod ?? politika.cod ?? 'always'
  const cardDeger = card ?? politika.card ?? 'first_order'
  const saatDeger = saat ?? String(seciliPlatform?.settings?.['orderConfirmLinkHours'] ?? 24)

  const politikaKaydet = useMutation({
    mutationFn: async () => {
      if (!seciliPlatform) throw new Error('Kanal seçin.')
      await api.put(`/core/firm-platforms/${seciliPlatform.id}/order-confirm-settings`, {
        cod: codDeger, card: cardDeger, linkHours: parseInt(saatDeger) || 24,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['firm-platforms'] })
      setBilgi('Onay politikası kaydedildi (siteye ~1 dk içinde yansır).'); setErr('')
      window.setTimeout(() => setBilgi(''), 3000)
    },
    onError: (e) => setErr((e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'Kaydedilemedi.'),
  })

  const degiskenSatiri = useMemo(() => (
    <div className="flex flex-wrap gap-1.5 mb-2">
      {DEGISKENLER.map(d => (
        <code key={d.key} title={d.desc} className="text-xs px-1.5 py-0.5 rounded cursor-default"
          style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}>
          {d.key}
        </code>
      ))}
      <span className="text-xs" style={{ color: 'var(--text-s)' }}>— metne kopyalayıp yerleştirin; gönderimde gerçek değerle doldurulur</span>
    </div>
  ), [])

  return (
    <div className="p-6 max-w-4xl">
      <h1 className="text-xl font-bold mb-1" style={{ color: 'var(--text)' }}>Bildirim Şablonları</h1>
      <p className="text-sm mb-5" style={{ color: 'var(--text-s)' }}>
        Sipariş onayı SMS/e-posta içerikleri ve onay politikası. Onay bağlantısına tıklayan müşteri siparişini onaylar;
        onaylı sipariş eski sisteme "Hazırlanıyor" olarak aktarılır.
      </p>

      {err && <p className="text-sm mb-3" style={{ color: '#ef4444' }}>{err}</p>}
      {bilgi && <p className="text-sm mb-3" style={{ color: '#16a34a' }}>{bilgi}</p>}

      {/* SMS şablonu */}
      <div className="card p-4 mb-4 space-y-2">
        <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>SMS ŞABLONU (Sipariş Onayı)</p>
        {degiskenSatiri}
        <textarea className="inp font-mono text-sm" rows={3} value={sms} onChange={e => setSmsBody(e.target.value)} />
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          {sms.length} karakter (değişkenler doldurulunca uzunluk değişir; Türkçe karakter SMS maliyetini artırabilir)
        </p>
        <div className="p-3 rounded text-sm" style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
          <span className="text-xs font-semibold block mb-1" style={{ color: 'var(--text-s)' }}>ÖNİZLEME</span>
          {onizle(sms)}
        </div>
        <Button size="sm" onClick={() => kaydet.mutate({ channel: 'sms', body: sms })} disabled={kaydet.isPending}>SMS Şablonunu Kaydet</Button>
      </div>

      {/* E-posta şablonu */}
      <div className="card p-4 mb-4 space-y-2">
        <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>E-POSTA ŞABLONU (Sipariş Onayı)</p>
        {degiskenSatiri}
        <div>
          <label className="flbl">Konu</label>
          <input className="inp" value={eKonu} onChange={e => setEmailKonu(e.target.value)} />
        </div>
        <div>
          <label className="flbl">Gövde (HTML)</label>
          <textarea className="inp font-mono text-xs" rows={6} value={eBody} onChange={e => setEmailBody(e.target.value)} />
        </div>
        <div className="p-3 rounded text-sm" style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
          <span className="text-xs font-semibold block mb-1" style={{ color: 'var(--text-s)' }}>ÖNİZLEME — {onizle(eKonu)}</span>
          <div dangerouslySetInnerHTML={{ __html: onizle(eBody) }} />
        </div>
        <Button size="sm" onClick={() => kaydet.mutate({ channel: 'email', subject: eKonu, body: eBody })} disabled={kaydet.isPending}>E-posta Şablonunu Kaydet</Button>
      </div>

      {/* Onay politikası */}
      <div className="card p-4 space-y-3">
        <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>ONAY POLİTİKASI (kanal bazlı)</p>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Firma</label>
            <select className="inp" value={seciliFirm} onChange={e => { setFirmId(e.target.value); setPlatformId(''); setCod(null); setCard(null); setSaat(null) }}>
              {firms.map(f => <option key={f.id} value={f.id}>{tr(f.nameI18n)}</option>)}
            </select>
          </div>
          <div>
            <label className="flbl">Kanal</label>
            <select className="inp" value={seciliPlatform?.id ?? ''} onChange={e => { setPlatformId(e.target.value); setCod(null); setCard(null); setSaat(null) }}>
              {channels.map(c => <option key={c.id} value={c.id}>{tr(c.nameI18n) || c.code}</option>)}
            </select>
          </div>
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className="flbl">Kapıda ödemeli sipariş</label>
            <select className="inp" value={codDeger} onChange={e => setCod(e.target.value)}>
              <option value="always">Her zaman onay iste</option>
              <option value="never">Onay isteme</option>
            </select>
          </div>
          <div>
            <label className="flbl">Kartla ödenen sipariş</label>
            <select className="inp" value={cardDeger} onChange={e => setCard(e.target.value)}>
              <option value="first_order">Yalnız ilk sipariş/misafir</option>
              <option value="always">Her zaman onay iste</option>
              <option value="never">Onay isteme</option>
            </select>
          </div>
          <div>
            <label className="flbl">Link ömrü (saat)</label>
            <input className="inp" type="number" min="1" max="168" value={saatDeger} onChange={e => setSaat(e.target.value)} />
          </div>
        </div>
        <Button size="sm" onClick={() => politikaKaydet.mutate()} disabled={politikaKaydet.isPending}>Politikayı Kaydet</Button>
      </div>
    </div>
  )
}
