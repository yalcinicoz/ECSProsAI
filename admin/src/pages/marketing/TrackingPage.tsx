import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { RefreshCw, Send, RotateCcw, ExternalLink } from 'lucide-react'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DataTable, Pager } from '@/components/ui/DataTable'
import { tarihSaat } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

/**
 * Pazarlama → Takip & Reklam (İE-4 Faz D-5, 2026-08-22 — docs/reklam-analytics-entegrasyon-is-akisi.md).
 * Kanal bazlı: aktif takip entegrasyonları (GA4/GTM/Ads/Meta/TikTok/…) kartları — mod (client/server/GTM),
 * son başarılı/hata, 24 saat sayıları; commerce event outbox özeti + listesi (bekleyen/hatalı yeniden dene);
 * "Test event gönder" (Meta/TikTok yalnız testEventCode doluyken, GA4 debug ucuna). Ayarlar Firma → Entegrasyonlar'da.
 */

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; code: string; nameI18n: Record<string, string>; firmId: string; firmName: string }
interface ServiceStatus {
  code: string; serviceType: string; integrationId: string; platformaOzel: boolean; ownership: string
  modes: string[]; settings: Record<string, string>
  lastSuccessAt?: string | null; lastFailureAt?: string | null; ok24: number; fail24: number; lastError?: string | null
}
interface StatusDto {
  enabled: boolean; dryRun: boolean; consentBanner: boolean; consentDefault: string; purchaseAt: string
  services: ServiceStatus[]
  outbox: { pending: number; error: number; done24: number; skipped24: number; lastEventAt?: string | null }
}
interface OutboxRow {
  id: string; eventName: string; dedupId: string; source: string; status: string; attemptCount: number
  nextAttemptAt?: string | null; lastError?: string | null; targetsJson?: string | null; createdAt: string; processedAt?: string | null
}
interface Paged<T> { items: T[]; totalCount: number; page: number; pageSize: number }
interface FeedStatusDto {
  enabled: boolean; intervalHours: number; feedsEnabled: boolean; xmlUrl?: string | null; csvUrl?: string | null; keyPending: boolean
  status?: { lastRunAt?: string | null; durationMs: number; productCount: number; itemCount: number; inStockCount: number; xmlBytes: number; csvBytes: number; error?: string | null; running: boolean } | null
}

const getName = (i18n?: Record<string, string> | null) => i18n?.tr || i18n?.en || Object.values(i18n ?? {})[0] || ''
const SERVIS_AD: Record<string, string> = {
  ga4: 'Google Analytics 4', gtm: 'Google Tag Manager', google_ads: 'Google Ads', google_merchant: 'Merchant Center',
  google_search_console: 'Search Console', meta: 'Meta Pixel / CAPI', tiktok: 'TikTok', pinterest: 'Pinterest',
  microsoft_ads: 'Microsoft Ads (UET)', microsoft_clarity: 'Microsoft Clarity',
}
const DURUM: Record<string, [string, BadgeVariant]> = {
  pending: ['Bekliyor', 'warning'], done: ['Gönderildi', 'success'], error: ['Hata', 'danger'], skipped: ['Atlandı', 'default'],
}
const MOD: Record<string, [string, BadgeVariant]> = { client: ['Tarayıcı', 'info'], server: ['Sunucu', 'success'], gtm: ['GTM', 'info'] }

export function TrackingPage() {
  const qc = useQueryClient()
  const [selectedChannelId, setSelectedChannelId] = useState<string>(() => sessionStorage.getItem('tracking.channelId') ?? '')
  const [tab, setTab] = useState('')
  const [page, setPage] = useState(1)
  const [testMsg, setTestMsg] = useState<string | null>(null)

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
  const effectiveChannelId = selectedChannelId || channels[0]?.id || ''
  useEffect(() => {
    if (effectiveChannelId) sessionStorage.setItem('tracking.channelId', effectiveChannelId)
  }, [effectiveChannelId])

  const { data: st, isLoading, refetch, isFetching } = useQuery<StatusDto>({
    queryKey: ['tracking-status', effectiveChannelId],
    queryFn: async () => (await api.get(`/tracking/status?firmPlatformId=${effectiveChannelId}`)).data.data,
    enabled: !!effectiveChannelId, refetchInterval: 15000,
  })
  const { data: ob } = useQuery<Paged<OutboxRow>>({
    queryKey: ['tracking-outbox', effectiveChannelId, tab, page],
    queryFn: async () => {
      const p = new URLSearchParams({ firmPlatformId: effectiveChannelId, page: String(page), pageSize: '30' })
      if (tab) p.set('status', tab)
      return (await api.get(`/tracking/outbox?${p}`)).data.data
    },
    enabled: !!effectiveChannelId, refetchInterval: 15000,
  })
  const { data: feed } = useQuery<FeedStatusDto>({
    queryKey: ['tracking-feed', effectiveChannelId],
    queryFn: async () => (await api.get(`/tracking/feed-status?firmPlatformId=${effectiveChannelId}`)).data.data,
    enabled: !!effectiveChannelId, refetchInterval: 10000,
  })
  const [feedMsg, setFeedMsg] = useState<string | null>(null)
  // FAZ 10/A6: tetik DB kuyruğuna yazılır, worker düğümü ~10 sn içinde sahiplenir.
  // Tamamlanmayı lastRunAt DEĞİŞİMİNDEN anlarız (tetik anındaki değer saklanır — saat
  // farkından etkilenmez); "kuyruğa alındı" mesajı asılı kalmasın diye ✓ mesajına çevrilir.
  const [feedBeklenen, setFeedBeklenen] = useState<string | null>(null) // tetik anındaki lastRunAt
  const [handledFeedCompletionKey, setHandledFeedCompletionKey] = useState<string | null>(null)
  const feedGen = useMutation({
    mutationFn: async () => api.post('/tracking/feed/generate', { firmPlatformId: effectiveChannelId }),
    onSuccess: () => {
      setFeedBeklenen(feed?.status?.lastRunAt ?? '(hiç)')
      setFeedMsg('Üretim kuyruğa alındı — worker ~10 sn içinde başlar, durum bu kartta yenilenir.')
      setTimeout(() => qc.invalidateQueries({ queryKey: ['tracking-feed'] }), 3000)
    },
    onError: (e: unknown) => setFeedMsg((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Üretim başlatılamadı.'),
  })
  const retry = useMutation({
    mutationFn: async (id: string) => api.post(`/tracking/outbox/${id}/retry`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['tracking-outbox'] }); qc.invalidateQueries({ queryKey: ['tracking-status'] }) },
  })
  const test = useMutation({
    mutationFn: async () => (await api.post('/tracking/test-event', { firmPlatformId: effectiveChannelId })).data,
    onSuccess: (d) => { setTestMsg(d?.data?.outboxId ? `Test event kuyruğa yazıldı (${d.data.dedupId}). 5-10 sn içinde sonucu aşağıda görürsünüz.` : 'Test event yazılamadı (takip kapalı olabilir).'); setTab(''); setPage(1); setTimeout(() => { qc.invalidateQueries({ queryKey: ['tracking-outbox'] }); qc.invalidateQueries({ queryKey: ['tracking-status'] }) }, 7000) },
    onError: (e: unknown) => setTestMsg((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Test event gönderilemedi.'),
  })

  const completedStatus = feed?.status
  const completionKey = completedStatus
    ? `${effectiveChannelId}:${completedStatus.lastRunAt ?? '(hiç)'}:${completedStatus.error ?? ''}`
    : null
  if (feedBeklenen !== null && completedStatus && !completedStatus.running
    && (completedStatus.lastRunAt ?? '(hiç)') !== feedBeklenen
    && completionKey !== handledFeedCompletionKey) {
    setHandledFeedCompletionKey(completionKey)
    setFeedMsg(completedStatus.error
      ? `Üretim HATAYLA bitti: ${completedStatus.error}`
      : `✓ Üretim tamamlandı — ${completedStatus.itemCount} kalem, ${Math.round(completedStatus.durationMs / 1000)} sn.`)
    setFeedBeklenen(null)
  }

  const outboxRows = ob?.items ?? []
  const totalPages = Math.ceil((ob?.totalCount ?? 0) / 30)

  return (
    <div className="p-6">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Takip &amp; Reklam</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Kanal bazlı analytics / pixel / dönüşüm entegrasyonlarının durumu. Kimlik ve ayarlar
            <Link to="/settings/firms" className="underline ml-1">Firma → Entegrasyonlar</Link> ekranından girilir.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <select className="sel" value={effectiveChannelId} onChange={e => { setSelectedChannelId(e.target.value); setPage(1) }}>
            {channels.map(c => <option key={c.id} value={c.id}>{c.firmName} — {getName(c.nameI18n) || c.code}</option>)}
          </select>
          <Button variant="secondary" size="sm" onClick={() => refetch()} disabled={isFetching}><RefreshCw className={cn('w-4 h-4', isFetching && 'animate-spin')} /></Button>
          <Button size="sm" onClick={() => { setTestMsg(null); test.mutate() }} disabled={!effectiveChannelId || test.isPending || !st?.enabled}>
            <Send className="w-4 h-4 mr-1" /> Test event gönder
          </Button>
        </div>
      </div>

      {st && (
        <div className="mb-4 flex flex-wrap gap-2 text-xs">
          <Badge variant={st.enabled ? 'success' : 'danger'}>{st.enabled ? (st.dryRun ? 'Takip AKTİF (DRY-RUN — dış platforma gönderilmez)' : 'Takip AKTİF') : 'Takip KAPALI (Tracking:Enabled=false)'}</Badge>
          <Badge variant="info">Consent bandı: {st.consentBanner ? 'açık' : 'kapalı'} / varsayılan {st.consentDefault}</Badge>
          <Badge variant="info">Satın alma anı: {st.purchaseAt}</Badge>
          <Badge variant={st.outbox.pending > 0 ? 'warning' : 'default'}>Kuyruk: {st.outbox.pending} bekliyor</Badge>
          <Badge variant={st.outbox.error > 0 ? 'danger' : 'default'}>{st.outbox.error} hatalı</Badge>
          <Badge variant="default">24s: {st.outbox.done24} gönderildi / {st.outbox.skipped24} atlandı</Badge>
        </div>
      )}
      {testMsg && <div className="mb-4 rounded-lg border px-3 py-2 text-sm" style={{ borderColor: 'var(--border)', color: 'var(--text)' }}>{testMsg}</div>}

      {isLoading && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</p>}
      {st && st.services.length === 0 && (
        <div className="mb-6 rounded-xl border p-6 text-sm" style={{ borderColor: 'var(--border)', color: 'var(--text-s)' }}>
          Bu kanalda aktif takip entegrasyonu yok — sitede hiçbir analytics/pixel script'i basılmaz ve çerez bandı gösterilmez.
          Eklemek için <Link to="/settings/firms" className="underline">Firma → Entegrasyonlar</Link>'dan GA4 / Meta / Google Ads vb. kaydı açın.
        </div>
      )}
      {st && st.services.length > 0 && (
        <div className="mb-6 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
          {st.services.map(s => (
            <div key={s.code} className="rounded-xl border p-4" style={{ borderColor: 'var(--border)', background: 'var(--surface)' }}>
              <div className="flex items-start justify-between gap-2">
                <div>
                  <div className="font-semibold" style={{ color: 'var(--text)' }}>{SERVIS_AD[s.code] ?? s.code}</div>
                  <div className="mt-1 flex flex-wrap gap-1">
                    {s.modes.map(m => <Badge key={m} variant={MOD[m]?.[1] ?? 'default'}>{MOD[m]?.[0] ?? m}</Badge>)}
                    <Badge variant="default">{s.platformaOzel ? 'kanala özel' : 'firma geneli'}</Badge>
                    <Badge variant="default">{s.ownership === 'platform' ? 'ECSPros hesabı' : 'müşteri hesabı'}</Badge>
                  </div>
                </div>
                <Badge variant={s.fail24 > 0 && s.lastFailureAt && (!s.lastSuccessAt || s.lastFailureAt > s.lastSuccessAt) ? 'danger' : s.modes.includes('server') ? 'success' : 'info'}>
                  {s.modes.includes('server') ? (s.fail24 > 0 ? 'hata var' : 'çalışıyor') : 'tarayıcı'}
                </Badge>
              </div>
              <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-1 text-xs" style={{ color: 'var(--text-s)' }}>
                {Object.entries(s.settings).slice(0, 6).map(([k, v]) => (
                  <div key={k} className="col-span-2 flex justify-between gap-2"><dt className="font-mono">{k}</dt><dd className="truncate" title={v}>{v}</dd></div>
                ))}
                {s.modes.includes('server') && (<>
                  <dt>Son başarılı</dt><dd>{tarihSaat(s.lastSuccessAt)}</dd>
                  <dt>Son hata</dt><dd>{tarihSaat(s.lastFailureAt)}</dd>
                  <dt>24 saat</dt><dd>{s.ok24} ✓ / {s.fail24} ✗</dd>
                </>)}
              </dl>
              {s.lastError && <p className="mt-2 break-words text-xs text-red-600" title={s.lastError}>{s.lastError.slice(0, 160)}</p>}
            </div>
          ))}
        </div>
      )}

      {feed && (
        <div className="mb-6 rounded-xl border p-4" style={{ borderColor: 'var(--border)', background: 'var(--surface)' }}>
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <div className="font-semibold" style={{ color: 'var(--text)' }}>Ürün feed'i (Google Merchant Center / Meta katalog)</div>
              <div className="mt-1 flex flex-wrap gap-1">
                <Badge variant={feed.enabled ? 'success' : 'default'}>{feed.enabled ? 'Merchant entegrasyonu aktif' : 'Merchant entegrasyonu yok'}</Badge>
                {feed.enabled && <Badge variant={feed.feedsEnabled ? 'info' : 'danger'}>{feed.feedsEnabled ? `her ${feed.intervalHours} saatte` : 'Feeds:Enabled=false'}</Badge>}
                {feed.status?.running && <Badge variant="warning">üretiliyor…</Badge>}
                {feed.status?.error && <Badge variant="danger">hata</Badge>}
              </div>
            </div>
            <Button size="sm" onClick={() => { setFeedMsg(null); feedGen.mutate() }} disabled={!feed.enabled || !feed.feedsEnabled || feedGen.isPending || !!feed.status?.running}>
              <RefreshCw className="w-4 h-4 mr-1" /> Şimdi üret
            </Button>
          </div>
          {feed.enabled ? (
            <dl className="mt-3 grid grid-cols-1 gap-x-6 gap-y-1 text-xs md:grid-cols-2" style={{ color: 'var(--text-s)' }}>
              <div className="flex justify-between gap-2"><dt>Son üretim</dt><dd>{tarihSaat(feed.status?.lastRunAt)} {feed.status?.durationMs ? `(${Math.round(feed.status.durationMs / 1000)} sn)` : ''}</dd></div>
              <div className="flex justify-between gap-2"><dt>Ürün / kalem (stokta)</dt><dd>{feed.status ? `${feed.status.productCount} / ${feed.status.itemCount} (${feed.status.inStockCount})` : '—'}</dd></div>
              <div className="flex justify-between gap-2"><dt>XML</dt><dd>{feed.status ? `${Math.round(feed.status.xmlBytes / 1024)} KB` : '—'}</dd></div>
              <div className="flex justify-between gap-2"><dt>CSV</dt><dd>{feed.status ? `${Math.round(feed.status.csvBytes / 1024)} KB` : '—'}</dd></div>
              <div className="md:col-span-2 flex items-center justify-between gap-2"><dt>Google Shopping XML</dt><dd className="truncate font-mono" title={feed.xmlUrl ?? ''}>{feed.keyPending ? 'anahtar ilk üretimde oluşur' : (feed.xmlUrl ?? '—')}</dd>
                {feed.xmlUrl && <Button variant="secondary" size="sm" onClick={() => navigator.clipboard?.writeText(feed.xmlUrl!)}>Kopyala</Button>}</div>
              <div className="md:col-span-2 flex items-center justify-between gap-2"><dt>Meta katalog CSV</dt><dd className="truncate font-mono" title={feed.csvUrl ?? ''}>{feed.keyPending ? 'anahtar ilk üretimde oluşur' : (feed.csvUrl ?? '—')}</dd>
                {feed.csvUrl && <Button variant="secondary" size="sm" onClick={() => navigator.clipboard?.writeText(feed.csvUrl!)}>Kopyala</Button>}</div>
              {feed.status?.error && <div className="md:col-span-2 text-red-600 break-words">{feed.status.error}</div>}
              {feedMsg && <div className="md:col-span-2" style={{ color: 'var(--text)' }}>{feedMsg}</div>}
            </dl>
          ) : (
            <p className="mt-2 text-xs" style={{ color: 'var(--text-s)' }}>Feed için <Link to="/settings/firms" className="underline">Firma → Entegrasyonlar</Link>'dan "Google Merchant Center" kaydı açın (merchantId, ülke TR, dil tr, para TRY, kargo bedeli). Kategori eşlemesi: Kanal Kategorileri → "Google ürün kategorisi".</p>
          )}
        </div>
      )}

      <div className="mb-2 flex items-center justify-between">
        <h2 className="font-semibold" style={{ color: 'var(--text)' }}>Event kuyruğu (outbox)</h2>
        <span className="text-xs" style={{ color: 'var(--text-s)' }}>{ob?.totalCount ?? 0} kayıt · son event {tarihSaat(st?.outbox.lastEventAt)}</span>
      </div>
      <div className="tab-scroll mb-3 flex gap-1" style={{ borderBottom: '1px solid var(--border)' }}>
        {[['', 'Tümü'], ['pending', 'Bekleyen'], ['error', 'Hatalı'], ['done', 'Gönderilen'], ['skipped', 'Atlanan']].map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')} onClick={() => { setTab(v); setPage(1) }}>{l}</button>
        ))}
      </div>
      <DataTable<OutboxRow>
        columns={[
          { header: 'ZAMAN', cell: r => tarihSaat(r.createdAt) },
          { header: 'EVENT', cell: r => <code className="text-xs font-mono">{r.eventName}</code> },
          { header: 'KAYNAK', cell: r => r.source },
          { header: 'DURUM', cell: r => <Badge variant={DURUM[r.status]?.[1] ?? 'default'}>{DURUM[r.status]?.[0] ?? r.status}</Badge> },
          { header: 'HEDEFLER', cell: r => <span className="text-xs font-mono break-all" title={r.targetsJson ?? ''}>{(r.targetsJson ?? '[]').slice(0, 90)}</span> },
          { header: 'DENEME', cell: r => `${r.attemptCount}${r.nextAttemptAt ? ' → ' + tarihSaat(r.nextAttemptAt) : ''}` },
          { header: 'HATA', cell: r => <span className="text-xs text-red-600" title={r.lastError ?? ''}>{(r.lastError ?? '').slice(0, 80)}</span> },
          { header: '', cell: r => (r.status === 'error' || r.status === 'skipped') ? (
            <Button variant="secondary" size="sm" onClick={() => retry.mutate(r.id)} title="Yeniden dene"><RotateCcw className="w-3.5 h-3.5" /></Button>) : null },
        ]}
        rows={outboxRows}
        empty="Kuyrukta kayıt yok"
      />
      {totalPages > 1 && <Pager page={page} totalPages={totalPages} onChange={setPage} />}
      <p className="mt-4 text-xs" style={{ color: 'var(--text-s)' }}>
        <ExternalLink className="inline w-3 h-3 mr-1" />Meta Events Manager → Test Events sekmesinde görmek için kanal Meta kaydına <code>testEventCode</code> girin; canlıda BOŞ bırakın.
        GA4 test event'i doğrulama ucuna gider (mülke yazılmaz). Tarayıcı tarafı event'ler bu listede görünmez (GA4 DebugView / Pixel Helper ile izlenir).
      </p>
    </div>
  )
}
