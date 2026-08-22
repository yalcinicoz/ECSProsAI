import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { RefreshCw, Send, RotateCcw, ExternalLink } from 'lucide-react'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DataTable, Pager, tarihSaat } from '@/components/ui/DataTable'
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
  useEffect(() => { if (selectedChannelId) sessionStorage.setItem('tracking.channelId', selectedChannelId) }, [selectedChannelId])

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
  useEffect(() => { if (!selectedChannelId && channels.length) setSelectedChannelId(channels[0].id) }, [channels, selectedChannelId])

  const { data: st, isLoading, refetch, isFetching } = useQuery<StatusDto>({
    queryKey: ['tracking-status', selectedChannelId],
    queryFn: async () => (await api.get(`/tracking/status?firmPlatformId=${selectedChannelId}`)).data.data,
    enabled: !!selectedChannelId, refetchInterval: 15000,
  })
  const { data: ob } = useQuery<Paged<OutboxRow>>({
    queryKey: ['tracking-outbox', selectedChannelId, tab, page],
    queryFn: async () => {
      const p = new URLSearchParams({ firmPlatformId: selectedChannelId, page: String(page), pageSize: '30' })
      if (tab) p.set('status', tab)
      return (await api.get(`/tracking/outbox?${p}`)).data.data
    },
    enabled: !!selectedChannelId, refetchInterval: 15000,
  })
  const retry = useMutation({
    mutationFn: async (id: string) => api.post(`/tracking/outbox/${id}/retry`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['tracking-outbox'] }); qc.invalidateQueries({ queryKey: ['tracking-status'] }) },
  })
  const test = useMutation({
    mutationFn: async () => (await api.post('/tracking/test-event', { firmPlatformId: selectedChannelId })).data,
    onSuccess: (d) => { setTestMsg(d?.data?.outboxId ? `Test event kuyruğa yazıldı (${d.data.dedupId}). 5-10 sn içinde sonucu aşağıda görürsünüz.` : 'Test event yazılamadı (takip kapalı olabilir).'); setTab(''); setPage(1); setTimeout(() => { qc.invalidateQueries({ queryKey: ['tracking-outbox'] }); qc.invalidateQueries({ queryKey: ['tracking-status'] }) }, 7000) },
    onError: (e: unknown) => setTestMsg((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Test event gönderilemedi.'),
  })

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
          <select className="sel" value={selectedChannelId} onChange={e => { setSelectedChannelId(e.target.value); setPage(1) }}>
            {channels.map(c => <option key={c.id} value={c.id}>{c.firmName} — {getName(c.nameI18n) || c.code}</option>)}
          </select>
          <Button variant="secondary" size="sm" onClick={() => refetch()} disabled={isFetching}><RefreshCw className={cn('w-4 h-4', isFetching && 'animate-spin')} /></Button>
          <Button size="sm" onClick={() => { setTestMsg(null); test.mutate() }} disabled={!selectedChannelId || test.isPending || !st?.enabled}>
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
