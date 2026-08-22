import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Save, Copy } from 'lucide-react'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Textarea } from '@/components/ui/Textarea'

/**
 * Vitrin → Takip & Çerez (İE-6 Faz F, 2026-08-22 — docs/reklam-analytics-entegrasyon-is-akisi.md).
 * Kanal bazlı: çerez bandı metinleri (başlık/açıklama/politika linki), satın alma event anı,
 * son 30 gün consent dağılımı (ispat günlüğü) ve KVKK/GDPR aydınlatma metni için ek madde şablonu.
 * EU kararı: banner her zaman açık, varsayılan DENY — buradan kapatılamaz (yalnız metin düzenlenir).
 * Ayar jsonb: FirmPlatform.Settings."tracking" (PUT /core/firm-platforms/{id}/tracking-settings).
 */

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; code: string; nameI18n: Record<string, string>; firmId: string; firmName: string; settings?: Record<string, unknown> }
interface ConsentStats { days: number; total: number; fullAccept: number; fullReject: number; partial: number; withMember: number; analytics: number; ads: number; lastAt?: string | null }

const getName = (i18n?: Record<string, string> | null) => i18n?.tr || i18n?.en || Object.values(i18n ?? {})[0] || ''

const KVKK_SABLON = `Çerezler ve Reklam/Analitik Platformlarına Aktarılan Veriler
Web sitemizde zorunlu çerezlerin yanı sıra, yalnızca açık rızanız halinde analitik (Google Analytics 4, Microsoft Clarity),
reklam/dönüşüm ölçümü (Google Ads, Meta/Facebook Pixel ve Conversions API, TikTok, Pinterest, Microsoft Advertising)
ve kişiselleştirme çerezleri kullanılmaktadır. Rızanız halinde; ziyaret ettiğiniz sayfalar, sepete eklediğiniz ürünler
ve tamamladığınız siparişlere ilişkin bilgiler ile çerez kimlikleri, IP adresiniz ve tarayıcı bilginiz bu platformlara
aktarılabilir. E-posta adresi ve telefon numarası gibi kişisel veriler yalnızca geri döndürülemez biçimde (SHA-256)
karma hale getirilerek iletilir. Bu platformların bir kısmı verileri yurt dışında işleyebilir. Tercihlerinizi dilediğiniz
zaman sayfa altındaki "Çerez Tercihleri" bağlantısından değiştirebilirsiniz; rızanızı geri çekmeniz halinde ilgili
çerezler kullanılmaz ve veri aktarımı durdurulur. Tercih kayıtlarınız ispat amacıyla 12 ay süreyle saklanır.`

export function TrackingConsentPage() {
  const qc = useQueryClient()
  const [selectedChannelId, setSelectedChannelId] = useState<string>(() => sessionStorage.getItem('trackingConsent.channelId') ?? '')
  const [form, setForm] = useState({ purchaseAt: 'confirmed', bannerTitle: '', bannerText: '', policyUrl: '', policyLabel: '' })
  const [msg, setMsg] = useState<string | null>(null)
  useEffect(() => { if (selectedChannelId) sessionStorage.setItem('trackingConsent.channelId', selectedChannelId) }, [selectedChannelId])

  const { data: firms = [] } = useQuery<Firm[]>({ queryKey: ['firms'], queryFn: async () => (await api.get('/core/firms')).data.data ?? [] })
  const platformQueries = useQueries({
    queries: firms.map(f => ({
      queryKey: ['firm-platforms', f.id],
      queryFn: async (): Promise<Channel[]> => ((await api.get(`/core/firms/${f.id}/platforms`)).data.data ?? [])
        .map((c: Channel) => ({ ...c, firmId: f.id, firmName: getName(f.nameI18n) })),
      enabled: firms.length > 0,
    })),
  })
  const channels = useMemo(() => platformQueries.flatMap(q => q.data ?? []), [platformQueries])
  const selected = channels.find(c => c.id === selectedChannelId)
  useEffect(() => { if (!selectedChannelId && channels.length) setSelectedChannelId(channels[0].id) }, [channels, selectedChannelId])
  useEffect(() => {
    const t = (selected?.settings?.['tracking'] ?? {}) as Record<string, unknown>
    setForm({
      purchaseAt: (t['purchaseAt'] as string) || 'confirmed',
      bannerTitle: (t['bannerTitle'] as string) || '',
      bannerText: (t['bannerText'] as string) || '',
      policyUrl: (t['policyUrl'] as string) || '',
      policyLabel: (t['policyLabel'] as string) || '',
    })
  }, [selected])

  const { data: stats } = useQuery<ConsentStats>({
    queryKey: ['consent-stats', selectedChannelId],
    queryFn: async () => (await api.get(`/tracking/consent-stats?firmPlatformId=${selectedChannelId}`)).data.data,
    enabled: !!selectedChannelId,
  })

  const save = useMutation({
    mutationFn: async () => api.put(`/core/firm-platforms/${selectedChannelId}/tracking-settings`, {
      purchaseAt: form.purchaseAt,
      bannerTitle: form.bannerTitle || null, bannerText: form.bannerText || null,
      policyUrl: form.policyUrl || null, policyLabel: form.policyLabel || null,
    }),
    onSuccess: () => { setMsg('Kaydedildi — vitrinde en geç 2 dk içinde etkili olur.'); if (selected) qc.invalidateQueries({ queryKey: ['firm-platforms', selected.firmId] }) },
    onError: (e: unknown) => setMsg((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Kaydedilemedi.'),
  })
  const pct = (n: number) => stats && stats.total ? Math.round(n * 100 / stats.total) + '%' : '—'

  return (
    <div className="p-6 max-w-5xl">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Takip &amp; Çerez</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Çerez bandı metinleri, satın alma event anı ve izin istatistikleri. Entegrasyon durumu için
            <Link to="/marketing/tracking" className="underline ml-1">Pazarlama → Takip &amp; Reklam</Link>.
          </p>
        </div>
        <select className="sel" value={selectedChannelId} onChange={e => setSelectedChannelId(e.target.value)}>
          {channels.map(c => <option key={c.id} value={c.id}>{c.firmName} — {getName(c.nameI18n) || c.code}</option>)}
        </select>
      </div>

      <div className="mb-4 flex flex-wrap gap-2 text-xs">
        <Badge variant="success">Çerez bandı: AÇIK (kapatılamaz — EU/KVKK kararı)</Badge>
        <Badge variant="info">Varsayılan: tüm kategoriler REDDEDİLMİŞ (Consent Mode v2)</Badge>
        <Badge variant="default">Kategoriler: Analitik · Reklam · Kişiselleştirme (+ zorunlu)</Badge>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2 rounded-xl border p-4 space-y-3" style={{ borderColor: 'var(--border)', background: 'var(--surface)' }}>
          <h2 className="font-semibold" style={{ color: 'var(--text)' }}>Band metinleri</h2>
          <label className="block text-sm">
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>Başlık (boş = "Çerez tercihleriniz")</span>
            <Input value={form.bannerTitle} onChange={e => setForm(f => ({ ...f, bannerTitle: e.target.value }))} placeholder="Çerez tercihleriniz" />
          </label>
          <label className="block text-sm">
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>Açıklama (boş = varsayılan metin)</span>
            <Textarea rows={3} value={form.bannerText} onChange={e => setForm(f => ({ ...f, bannerText: e.target.value }))} placeholder="Alışveriş deneyiminizi iyileştirmek, site trafiğini analiz etmek ve size uygun reklamlar göstermek için çerezler kullanıyoruz…" />
          </label>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <label className="block text-sm">
              <span className="text-xs" style={{ color: 'var(--text-s)' }}>Politika sayfası linki (boş = /gizlilik-ve-guvenlik)</span>
              <Input value={form.policyUrl} onChange={e => setForm(f => ({ ...f, policyUrl: e.target.value }))} placeholder="/gizlilik-ve-guvenlik" />
            </label>
            <label className="block text-sm">
              <span className="text-xs" style={{ color: 'var(--text-s)' }}>Link metni</span>
              <Input value={form.policyLabel} onChange={e => setForm(f => ({ ...f, policyLabel: e.target.value }))} placeholder="Gizlilik ve Çerez Politikası" />
            </label>
          </div>
          <label className="block text-sm">
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>Sunucu taraflı "satın alma" event anı</span>
            <select className="sel mt-1" value={form.purchaseAt} onChange={e => setForm(f => ({ ...f, purchaseAt: e.target.value }))}>
              <option value="confirmed">Sipariş onaylandığında (varsayılan — ödeme alındı/onaylandı)</option>
              <option value="created">Sipariş oluşturulduğunda</option>
            </select>
          </label>
          <div className="flex items-center gap-3">
            <Button onClick={() => { setMsg(null); save.mutate() }} disabled={!selectedChannelId || save.isPending}><Save className="w-4 h-4 mr-1" /> Kaydet</Button>
            {msg && <span className="text-sm" style={{ color: 'var(--text-s)' }}>{msg}</span>}
          </div>
        </div>

        <div className="rounded-xl border p-4" style={{ borderColor: 'var(--border)', background: 'var(--surface)' }}>
          <h2 className="font-semibold" style={{ color: 'var(--text)' }}>İzin istatistiği (son 30 gün)</h2>
          {!stats || stats.total === 0 ? (
            <p className="mt-2 text-sm" style={{ color: 'var(--text-s)' }}>Henüz kayıt yok — band gösterildikçe tercihler burada birikir.</p>
          ) : (
            <dl className="mt-2 space-y-1 text-sm" style={{ color: 'var(--text)' }}>
              <div className="flex justify-between"><dt>Toplam tercih</dt><dd>{stats.total}</dd></div>
              <div className="flex justify-between"><dt>Tümünü kabul</dt><dd>{stats.fullAccept} ({pct(stats.fullAccept)})</dd></div>
              <div className="flex justify-between"><dt>Tümünü red</dt><dd>{stats.fullReject} ({pct(stats.fullReject)})</dd></div>
              <div className="flex justify-between"><dt>Kısmi</dt><dd>{stats.partial}</dd></div>
              <div className="flex justify-between"><dt>Analitik izni</dt><dd>{pct(stats.analytics)}</dd></div>
              <div className="flex justify-between"><dt>Reklam izni</dt><dd>{pct(stats.ads)}</dd></div>
              <div className="flex justify-between"><dt>Üye eşleşmeli</dt><dd>{stats.withMember}</dd></div>
            </dl>
          )}
          <p className="mt-3 text-xs" style={{ color: 'var(--text-s)' }}>Tercih günlüğü 12 ay saklanır (ispat); IP yalnız hash'li.</p>
        </div>
      </div>

      <div className="mt-4 rounded-xl border p-4" style={{ borderColor: 'var(--border)', background: 'var(--surface)' }}>
        <div className="flex items-center justify-between">
          <h2 className="font-semibold" style={{ color: 'var(--text)' }}>KVKK / GDPR aydınlatma metni — ek madde şablonu</h2>
          <Button variant="secondary" size="sm" onClick={() => navigator.clipboard?.writeText(KVKK_SABLON)}><Copy className="w-3.5 h-3.5 mr-1" /> Kopyala</Button>
        </div>
        <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Hukuk onayından geçirip İçerik → Sayfalar'daki gizlilik/aydınlatma sayfasına ekleyin.</p>
        <pre className="mt-2 whitespace-pre-wrap text-xs leading-5 rounded-lg p-3" style={{ background: 'var(--bg)', color: 'var(--text)' }}>{KVKK_SABLON}</pre>
      </div>
    </div>
  )
}
