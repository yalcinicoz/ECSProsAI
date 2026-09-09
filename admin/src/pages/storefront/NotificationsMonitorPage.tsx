import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'
import { useFirmPlatforms } from '@/pages/cms/cmsPageShared'
import { PushTemplatesTab, PushLogTab } from './PushTabs'

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

function StockAlertsTab({ platformId }: { platformId: string }) {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (StockAlertGrid.Schema) + Excel + görünümler.
  const [sp] = useSearchParams()
  const status = sp.get('status') ?? 'active'
  const grid = useGridState('stock-alerts', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })

  const named = () => ({ status: status || undefined, firmPlatformId: platformId || undefined })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<StockAlert>>({
    queryKey: ['admin-stock-alerts', status, platformId, ...grid.queryKey],
    queryFn: async () => (await api.get(`/store-notifications/stock-alerts?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const columns: GridColumn<StockAlert>[] = [
    { key: 'createdAt', header: 'TARİH', priority: 1, frozen: true, sortable: true, minWidth: 150,
      filter: { type: 'date', label: 'Kayıt tarihi', quick: true },
      cell: a => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
        {new Date(a.createdAt).toLocaleString('tr-TR')}</span> },
    { key: 'productCode', header: 'ÜRÜN KODU', priority: 1, lockVisible: true, frozen: true, sortable: true,
      filter: { type: 'text', label: 'Ürün kodu', ops: ['startswith', 'contains', 'eq'] },
      cell: a => <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{a.productCode || '—'}</code> },
    { key: 'variantInfo', header: 'VARYANT', priority: 2, sortable: true, filter: { type: 'text', label: 'Varyant' },
      cell: a => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{a.variantInfo || '—'}</span> },
    { key: 'email', header: 'E-POSTA', priority: 1, sortable: true, filter: { type: 'text', label: 'E-posta' },
      cell: a => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{a.email || '—'}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(ALERT_STATUS).map(([value, v]) => ({ value, label: v.label })) },
      cell: a => <Badge variant={ALERT_STATUS[a.status]?.variant ?? 'neutral'}>{ALERT_STATUS[a.status]?.label ?? a.status}</Badge> },
    { key: 'notifiedAt', header: 'BİLDİRİM ZAMANI', priority: 2, sortable: true,
      filter: { type: 'date', label: 'Bildirim zamanı' },
      filters: [{ field: 'notified', label: 'Bildirimi gönderilmiş', type: 'boolean' }],
      cell: a => <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {a.notifiedAt ? new Date(a.notifiedAt).toLocaleString('tr-TR') : '—'}</span> },
  ]

  return (
    <DataGrid<StockAlert>
      gridId="stock-alerts"
      views
      grid={grid}
      columns={columns}
      rows={data?.items ?? []}
      totalCount={data?.totalCount ?? 0}
      loading={isLoading}
      fetching={isFetching}
      error={listError ? errText(listError) : null}
      empty='Stok alarmı yok. Müşteriler tükenen ürünlerde "Stok gelince haber ver" ile kayıt bırakır.'
      search={{ placeholder: 'E-posta veya ürün kodu ara…' }}
      minWidth={980}
      filterLeading={
        <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={status} aria-label="Durum"
          onChange={e => grid.mutate(n => { if (e.target.value) n.set('status', e.target.value); else n.delete('status') })}>
          <option value="active">Bekleyenler</option>
          <option value="notified">Bildirilenler</option>
          <option value="cancelled">İptal Edilenler</option>
          <option value="">Tümü</option>
        </select>
      }
      export={{ endpoint: '/store-notifications/stock-alerts/export', named, fallbackFileName: 'stok-alarmlari.xlsx' }}
      compact={{
        title: a => a.productCode || '—',
        subtitle: a => `${a.email || '—'}${a.variantInfo ? ` · ${a.variantInfo}` : ''}`,
        right: a => new Date(a.createdAt).toLocaleDateString('tr-TR'),
        badge: a => <Badge variant={ALERT_STATUS[a.status]?.variant ?? 'neutral'}>{ALERT_STATUS[a.status]?.label ?? a.status}</Badge>,
      }}
    />
  )
}

function SavedSearchesTab({ platformId }: { platformId: string }) {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (SavedSearchGrid.Schema) + Excel + görünümler.
  const [sp] = useSearchParams()
  const notifyFilter = sp.get('notifyEnabled') ?? 'true'
  const grid = useGridState('saved-searches', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })

  const named = () => ({
    notifyEnabled: notifyFilter || undefined,
    firmPlatformId: platformId || undefined,
  })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<SavedSearch>>({
    queryKey: ['admin-saved-searches', notifyFilter, platformId, ...grid.queryKey],
    queryFn: async () => (await api.get(`/store-notifications/saved-searches?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const columns: GridColumn<SavedSearch>[] = [
    { key: 'createdAt', header: 'TARİH', priority: 1, frozen: true, sortable: true, minWidth: 150,
      filter: { type: 'date', label: 'Kayıt tarihi', quick: true },
      cell: s2 => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
        {new Date(s2.createdAt).toLocaleString('tr-TR')}</span> },
    { key: 'name', header: 'AD', priority: 1, lockVisible: true, frozen: true, sortable: true,
      filter: { type: 'text', label: 'Ad' },
      cell: s2 => <span className="text-sm" style={{ color: 'var(--text)' }}>{s2.name || '—'}</span> },
    { key: 'query', header: 'SORGU', priority: 1, sortable: true, minWidth: 280,
      filter: { type: 'text', label: 'Sorgu metni' },
      cell: s2 => <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{s2.query}</code> },
    { key: 'notifyEnabled', header: 'BİLDİRİM', priority: 1, sortable: true,
      filter: { type: 'boolean', label: 'Bildirim açık' },
      cell: s2 => <Badge variant={s2.notifyEnabled ? 'success' : 'neutral'}>{s2.notifyEnabled ? 'Açık' : 'Kapalı'}</Badge> },
    { key: 'lastNotifiedAt', header: 'SON BİLDİRİM', priority: 2, sortable: true,
      filter: { type: 'date', label: 'Son bildirim' },
      filters: [{ field: 'notified', label: 'Bildirimi gönderilmiş', type: 'boolean' }],
      cell: s2 => <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {s2.lastNotifiedAt ? new Date(s2.lastNotifiedAt).toLocaleString('tr-TR') : 'Henüz gönderilmedi'}</span> },
  ]

  return (
    <DataGrid<SavedSearch>
      gridId="saved-searches"
      views
      grid={grid}
      columns={columns}
      rows={data?.items ?? []}
      totalCount={data?.totalCount ?? 0}
      loading={isLoading}
      fetching={isFetching}
      error={listError ? errText(listError) : null}
      empty='Kayıtlı arama yok. Üyeler "Favori Aramalarım"dan kaydeder; bildirim açıksa yeni ürün düştüğünde e-posta gider.'
      search={{ placeholder: 'Arama sorgusu veya ad ara…' }}
      minWidth={940}
      filterLeading={
        <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={notifyFilter} aria-label="Bildirim"
          onChange={e => grid.mutate(n => { if (e.target.value) n.set('notifyEnabled', e.target.value); else n.delete('notifyEnabled') })}>
          <option value="true">Bildirim Açık</option>
          <option value="false">Bildirim Kapalı</option>
          <option value="">Tümü</option>
        </select>
      }
      export={{ endpoint: '/store-notifications/saved-searches/export', named, fallbackFileName: 'kayitli-aramalar.xlsx' }}
      compact={{
        title: s2 => s2.name || s2.query,
        subtitle: s2 => s2.query,
        right: s2 => new Date(s2.createdAt).toLocaleDateString('tr-TR'),
        badge: s2 => <Badge variant={s2.notifyEnabled ? 'success' : 'neutral'}>{s2.notifyEnabled ? 'Açık' : 'Kapalı'}</Badge>,
      }}
    />
  )
}

type MonitorTab = 'stock-alerts' | 'saved-searches' | 'push-templates' | 'push-log'
const MONITOR_TABS: MonitorTab[] = ['stock-alerts', 'saved-searches', 'push-templates', 'push-log']

export function NotificationsMonitorPage() {
  // ?tab=push-log&deviceId=... ile üye detayından doğrudan deneme formuna gelinir.
  const [sp, setSp] = useSearchParams()
  const ilkTab = sp.get('tab') as MonitorTab | null
  const [tab, setTab] = useState<MonitorTab>(ilkTab && MONITOR_TABS.includes(ilkTab) ? ilkTab : 'stock-alerts')

  /**
   * ★ Sekme değişince GRID PARAMETRELERİ TEMİZLENİR.
   * Neden: useGridState URL parametrelerini (page, search, sort, dir, f. ve fq. önekli filtreler)
   * gridId'ye göre AD ALANINA ALMAZ — yalnız localStorage tercihleri gridId'li. Bu sayfada iki ayrı grid var (
   * stok alarmları / kayıtlı aramalar); temizlemezsek bir sekmede seçilen sıralama diğerine taşınır ve o şemada
   * olmayan anahtar yüzünden liste "Geçersiz sıralama alanı" ile 400 döner.
   * Aynı durum çok grid'li her sayfa için geçerlidir.
   */
  const sekmeyeGec = (t: MonitorTab) => {
    setTab(t)
    setSp(prev => {
      const n = new URLSearchParams(prev)
      for (const k of [...n.keys()]) if (k.startsWith('f.') || k.startsWith('fq.')) n.delete(k)
      for (const k of ['page', 'search', 'sort', 'dir', 'status', 'notifyEnabled']) n.delete(k)
      n.set('tab', t)
      return n
    }, { replace: true })
  }
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
          onClick={() => sekmeyeGec('stock-alerts')}>Stok Alarmları</button>
        <button className={cn('stab', tab === 'saved-searches' && 'active')}
          onClick={() => sekmeyeGec('saved-searches')}>Kayıtlı Aramalar</button>
        <button className={cn('stab', tab === 'push-templates' && 'active')}
          onClick={() => sekmeyeGec('push-templates')}>Push Şablonları</button>
        <button className={cn('stab', tab === 'push-log' && 'active')}
          onClick={() => sekmeyeGec('push-log')}>Push Gönderimleri</button>
      </div>

      {tab === 'stock-alerts' ? <StockAlertsTab platformId={platformId} />
        : tab === 'saved-searches' ? <SavedSearchesTab platformId={platformId} />
        : tab === 'push-templates' ? <PushTemplatesTab />
        : <PushLogTab />}
    </div>
  )
}
