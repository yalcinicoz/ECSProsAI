import { useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'
import { useFirmPlatforms } from '@/pages/cms/cmsPageShared'

interface StockAlert {
  id: string
  firmPlatformId: string
  memberId: string
  email?: string
  productCode?: string
  variantInfo?: string
  status: string
  notifiedAt?: string
  createdAt: string
}

interface SavedSearch {
  id: string
  firmPlatformId: string
  memberId: string
  name?: string
  query: string
  notifyEnabled: boolean
  lastNotifiedAt?: string
  createdAt: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const ALERT_STATUS: Record<string, { label: string; variant: 'warning' | 'success' | 'neutral' }> = {
  active:    { label: 'Bekliyor',   variant: 'warning' },
  notified:  { label: 'Bildirildi', variant: 'success' },
  cancelled: { label: 'İptal',      variant: 'neutral' },
}

function Pager({ page, totalPages, setPage }: { page: number; totalPages: number; setPage: (fn: (p: number) => number) => void }) {
  if (totalPages <= 1) return null
  return (
    <div className="flex items-center justify-center gap-2 mt-4">
      <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
        className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
        style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>← Önceki</button>
      <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
      <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
        className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
        style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki →</button>
    </div>
  )
}

function StockAlertsTab({ platformId }: { platformId: string }) {
  const [status, setStatus] = useState('active')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery<PagedResult<StockAlert>>({
    queryKey: ['admin-stock-alerts', status, platformId, appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (status) params.set('status', status)
      if (platformId) params.set('firmPlatformId', platformId)
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/store-notifications/stock-alerts?${params}`)).data.data
    },
  })

  const alerts = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <>
      <div className="flex items-center gap-2 mb-4">
        <select className="inp text-sm py-1.5 px-3 h-auto w-auto" value={status}
          onChange={e => { setStatus(e.target.value); setPage(1) }}>
          <option value="active">Bekleyenler</option>
          <option value="notified">Bildirilenler</option>
          <option value="cancelled">İptal Edilenler</option>
          <option value="">Tümü</option>
        </select>
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
          placeholder="E-posta veya ürün kodu ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setAppliedSearch(search.trim()); setPage(1) } }} />
        <button onClick={() => { setAppliedSearch(search.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
        <span className="text-sm ml-auto" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</span>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['TARİH', 'ÜRÜN KODU', 'VARYANT', 'E-POSTA', 'DURUM', 'BİLDİRİM ZAMANI'].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left"
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && alerts.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Stok alarmı yok. Müşteriler tükenen ürünlerde "Stok gelince haber ver" ile kayıt bırakır.
              </td></tr>
            )}
            {alerts.map(a => (
              <tr key={a.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
                  {new Date(a.createdAt).toLocaleString('tr-TR')}
                </td>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{a.productCode || '—'}</code>
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{a.variantInfo || '—'}</td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{a.email || '—'}</td>
                <td className="px-4 py-3">
                  <Badge variant={ALERT_STATUS[a.status]?.variant ?? 'neutral'}>
                    {ALERT_STATUS[a.status]?.label ?? a.status}
                  </Badge>
                </td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {a.notifiedAt ? new Date(a.notifiedAt).toLocaleString('tr-TR') : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} totalPages={totalPages} setPage={setPage} />
    </>
  )
}

function SavedSearchesTab({ platformId }: { platformId: string }) {
  const [notifyFilter, setNotifyFilter] = useState('true')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery<PagedResult<SavedSearch>>({
    queryKey: ['admin-saved-searches', notifyFilter, platformId, appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (notifyFilter) params.set('notifyEnabled', notifyFilter)
      if (platformId) params.set('firmPlatformId', platformId)
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/store-notifications/saved-searches?${params}`)).data.data
    },
  })

  const searches = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <>
      <div className="flex items-center gap-2 mb-4">
        <select className="inp text-sm py-1.5 px-3 h-auto w-auto" value={notifyFilter}
          onChange={e => { setNotifyFilter(e.target.value); setPage(1) }}>
          <option value="true">Bildirim Açık</option>
          <option value="false">Bildirim Kapalı</option>
          <option value="">Tümü</option>
        </select>
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
          placeholder="Arama sorgusu veya ad ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setAppliedSearch(search.trim()); setPage(1) } }} />
        <button onClick={() => { setAppliedSearch(search.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
        <span className="text-sm ml-auto" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</span>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['TARİH', 'AD', 'SORGU', 'BİLDİRİM', 'SON BİLDİRİM'].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left"
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={5} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && searches.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Kayıtlı arama yok. Üyeler "Favori Aramalarım"dan kaydeder; bildirim açıksa yeni ürün düştüğünde e-posta gider.
              </td></tr>
            )}
            {searches.map(s => (
              <tr key={s.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
                  {new Date(s.createdAt).toLocaleString('tr-TR')}
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text)' }}>{s.name || '—'}</td>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{s.query}</code>
                </td>
                <td className="px-4 py-3">
                  <Badge variant={s.notifyEnabled ? 'success' : 'neutral'}>
                    {s.notifyEnabled ? 'Açık' : 'Kapalı'}
                  </Badge>
                </td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {s.lastNotifiedAt ? new Date(s.lastNotifiedAt).toLocaleString('tr-TR') : 'Henüz gönderilmedi'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} totalPages={totalPages} setPage={setPage} />
    </>
  )
}

export function NotificationsMonitorPage() {
  const [tab, setTab] = useState<'stock-alerts' | 'saved-searches'>('stock-alerts')
  const [platformId, setPlatformId] = useState('')
  const [scanResult, setScanResult] = useState('')

  const { data: platforms = [] } = useFirmPlatforms()

  const scan = useMutation({
    mutationFn: async () => (await api.post('/store-notifications/saved-search-scan')).data,
    onSuccess: (d: { data?: { sent?: number } }) =>
      setScanResult(`Tarama tamamlandı — ${d.data?.sent ?? 0} e-posta gönderildi.`),
    onError: () => setScanResult('Tarama başarısız oldu.'),
  })

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4 gap-3 flex-wrap">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Bildirimler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Stok alarmı ve kayıtlı arama bildirimlerinin izlemesi — gönderimler otomatik koşar
          </p>
        </div>
        <div className="flex items-center gap-2">
          {/* Düğüm her zaman DOM'da kalır, yalnız metni/görünürlüğü değişir — tarayıcı
              çeviri/yazım uzantılarının metin düğümlerini sarmalamasıyla React'in yeni
              kardeş düğüm eklemesi (insertBefore) çakışmasın diye. */}
          <span className="text-sm" style={{ color: 'var(--text-s)', display: scanResult ? undefined : 'none' }}>
            {scanResult}
          </span>
          <Button size="sm" variant="secondary" loading={scan.isPending}
            onClick={() => { setScanResult(''); scan.mutate() }}
            title="Kayıtlı arama taramasını beklemeden şimdi çalıştırır; günde-1 sınırı korunur, yinelenen e-posta üretmez.">
            Şimdi Tara
          </Button>
          <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 180 }}
            value={platformId} onChange={e => setPlatformId(e.target.value)}>
            <option value="">Tüm platformlar</option>
            {platforms.map(p => (
              <option key={p.id} value={p.id}>{p.nameI18n?.['tr'] ?? p.id}</option>
            ))}
          </select>
        </div>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'stock-alerts' && 'active')}
          onClick={() => setTab('stock-alerts')}>Stok Alarmları</button>
        <button className={cn('stab', tab === 'saved-searches' && 'active')}
          onClick={() => setTab('saved-searches')}>Kayıtlı Aramalar</button>
      </div>

      {tab === 'stock-alerts' ? <StockAlertsTab platformId={platformId} /> : <SavedSearchesTab platformId={platformId} />}
    </div>
  )
}
