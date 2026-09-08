// Müşteri İlişkileri — kayıt listesi (eski /crm/musteri-iliskileri-yonetimi). Varsayılan: gizli olmayan durumlar;
// sayaç kutuları durum süzgeci; satır tıklama → detay (takip no). "Kontrol" sütunu = son işlemi ben okudum mu.
// DataGrid F4 (2026-09-08): filtre/arama/sıralama/sayfa URL'de (eski ?status=&search=&customer=… derin linkleri korunur — adlandırılmış
// parametreler sunucuda aynen çalışır); grid f.* filtreleri TicketGrid.Schema beyaz listesi; kolon tercihleri localStorage'da.
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'
import { DataGrid, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { TYPE_LABEL, fmtTarih, fmtTelefon, useTicketSettings, useAdminUsers, usePlatformNames, type TicketListItem, type TicketPage } from './ticketShared'

// Sunucu tarafında adlandırılmış kalan parametreler (derin link + export "named")
const NAMED_KEYS = ['status', 'subjectId', 'createdBy', 'from', 'to', 'trackingNo', 'customer', 'orderNumber', 'memberId', 'orderId', 'taggedMe', 'unreadByMe', 'includeHidden', 'type'] as const

export function TicketsPage() {
  const navigate = useNavigate()
  const [sp] = useSearchParams()
  const grid = useGridState('tickets', { defaultPageSize: 25, defaultSort: 'createdAt', defaultDir: 'desc' })
  const { data: settings } = useTicketSettings(true)
  const { data: users = [] } = useAdminUsers()
  const { data: platformNames = {} } = usePlatformNames()

  const get = (k: string) => sp.get(k) ?? ''
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })
  const named = () => Object.fromEntries(NAMED_KEYS.map(k => [k, get(k) || undefined]))
  const status = get('status')

  const { data, isLoading, isFetching, error } = useQuery<TicketPage>({
    queryKey: ['tickets', ...grid.queryKey, ...NAMED_KEYS.map(k => get(k))],
    queryFn: async () => (await api.get(`/crm/tickets?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const extraFilters: GridFilterField[] = [
    { key: 'type', label: 'Tür', type: 'enum', quick: true, options: [{ value: 'complaint', label: 'Şikayet' }, { value: 'request', label: 'Talep' }] },
  ]

  const columns: GridColumn<TicketListItem>[] = [
    { key: 'trackingNo', header: 'Takip No', frozen: true, lockVisible: true, sortable: true, filter: { type: 'number', label: 'Takip no' },
      cell: t => <span className="font-mono whitespace-nowrap" style={{ color: 'var(--text)' }}>
        {t.trackingNo}
        {t.taggedMe && <span className="ml-1 text-[10px] px-1 rounded" style={{ background: '#3b82f620', color: '#3b82f6' }}>etiket</span>}
        {t.isHidden && <span className="ml-1 text-[10px] px-1 rounded" style={{ background: '#6b728020', color: '#6b7280' }}>gizli</span>}
      </span> },
    { key: 'type', header: 'Tür / Konu', sortable: true, priority: 1,
      cell: t => <span style={{ color: 'var(--text)' }}><span className="text-xs" style={{ color: t.type === 'complaint' ? '#ef4444' : '#3b82f6' }}>{TYPE_LABEL[t.type] ?? t.type}</span><br />{t.subjectName}</span> },
    { key: 'order', header: 'Sipariş', filters: [{ field: 'orderNumber', label: 'Sipariş no', type: 'text', ops: ['eq', 'startswith', 'contains'] }], priority: 2,
      cell: t => <span className="font-mono text-xs whitespace-nowrap" style={{ color: 'var(--text)' }}>
        {t.orderNumber ?? '—'}{t.firmPlatformId && platformNames[t.firmPlatformId] && <><br /><span style={{ color: 'var(--text-s)' }}>{platformNames[t.firmPlatformId]}</span></>}
      </span> },
    { key: 'customer', header: 'Müşteri', filters: [{ field: 'customerPhone', label: 'Müşteri telefon', type: 'text', ops: ['contains', 'startswith'] }], sortable: true, priority: 1, filter: { type: 'text', label: 'Müşteri adı', quick: true },
      cell: t => <span style={{ color: 'var(--text)' }}>{t.customerName || '—'}<br /><span className="text-xs" style={{ color: 'var(--text-s)' }}>{fmtTelefon(t.customerPhone)}</span></span> },
    { key: 'caller', header: 'Arayan', filters: [{ field: 'caller', label: 'Arayan', type: 'text' }, { field: 'callerPhone', label: 'Arayan telefon', type: 'text', ops: ['contains', 'startswith'] }], priority: 3,
      cell: t => <span style={{ color: 'var(--text)' }}>{t.callerName || '—'}<br /><span className="text-xs" style={{ color: 'var(--text-s)' }}>{fmtTelefon(t.callerPhone)}</span></span> },
    { key: 'createdAt', header: 'Kayıt', filters: [{ field: 'createdByName', label: 'Kayıt açan (ad)', type: 'text' }], sortable: true, priority: 2, filter: { type: 'date', label: 'Kayıt tarihi', quick: true },
      cell: t => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{fmtTarih(t.createdAt)}<br />{t.createdByName}</span> },
    { key: 'lastActivityAt', header: 'Son İşlem', filters: [{ field: 'lastActivityAt', label: 'Son işlem tarihi', type: 'date' }, { field: 'activityCount', label: 'İşlem sayısı', type: 'number' }], sortable: true, priority: 3,
      cell: t => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{fmtTarih(t.lastActivityAt)}<br />{t.updatedByName ?? ''} {t.activityCount > 0 && <span style={{ color: 'var(--text-s)' }}>({t.activityCount})</span>}</span> },
    { key: 'control', header: 'Kontrol', priority: 2, exportable: false,
      cell: t => t.readByMe ? <span className="text-xs" style={{ color: '#22c55e' }}>Kontrol edildi</span> : <span className="text-xs font-semibold" style={{ color: '#ef4444' }}>Kontrol edilmedi</span> },
    { key: 'status', header: 'Durum', filters: [{ field: 'hidden', label: 'Gizli', type: 'boolean' }], lockVisible: true, sortable: true, priority: 1,
      cell: t => <span className="text-xs px-2 py-0.5 rounded-full font-medium whitespace-nowrap" style={{ background: `${t.statusColor}20`, color: t.statusColor }}>{t.statusName}</span> },
  ]

  const toplam = (data?.counters ?? []).filter(c => !settings?.statuses.find(s => s.code === c.statusCode)?.isHidden).reduce((a, c) => a + c.count, 0)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Müşteri İlişkileri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>Talep ve şikayet kayıtları. Satıra tıklayın → detay; işlemler, etiketleme ve durum değişikliği kaydın içinde.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="secondary" size="sm" onClick={() => navigate('/crm/tickets/settings')}>Ayarlar</Button>
          <Button size="sm" onClick={() => navigate('/crm/tickets/new')}>+ Yeni Kayıt</Button>
        </div>
      </div>

      {/* Sayaçlar — tıklanınca durum süzgeci (?status=, sunucu sayaçları diğer aktif filtrelerle tutarlı) */}
      <div className="flex flex-wrap gap-2 mb-4">
        <button onClick={() => setNamed('status', '')} className={cn('px-3 py-2 rounded-xl text-sm', !status && 'ring-2')}
          style={{ background: 'var(--surface2)', color: 'var(--text)', border: '1px solid var(--border)' }}>
          Toplam <b>{toplam}</b>
        </button>
        {(settings?.statuses ?? []).map(s => {
          const n = data?.counters.find(c => c.statusCode === s.code)?.count ?? 0
          return (
            <button key={s.code} onClick={() => setNamed('status', status === s.code ? '' : s.code)}
              className={cn('px-3 py-2 rounded-xl text-sm', status === s.code && 'ring-2')}
              title={s.isHidden ? 'Gizli durum — yalnız bu süzgeçle görünür' : undefined}
              style={{ background: `${s.color}18`, color: s.color, border: `1px solid ${s.color}55`, opacity: s.isHidden ? 0.7 : 1 }}>
              {s.name} <b>{n}</b>
            </button>
          )
        })}
      </div>

      <DataGrid<TicketListItem>
        gridId="tickets"
        views
        grid={grid}
        columns={columns}
        extraFilters={extraFilters}
        search={{ placeholder: 'İçerikte ara (kayıt + işlem metinleri)…' }}
        filterLeading={
          <>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={get('subjectId')} aria-label="Konu" onChange={e => setNamed('subjectId', e.target.value)}>
              <option value="">Konu: Tümü</option>{(settings?.subjects ?? []).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={get('createdBy')} aria-label="Kayıt açan" onChange={e => setNamed('createdBy', e.target.value)}>
              <option value="">Kayıt açan: Tümü</option>{users.map(u => <option key={u.id} value={u.id}>{u.fullName}</option>)}
            </select>
          </>
        }
        toolbarBelow={
          <div className="flex flex-wrap items-center gap-3 text-xs" style={{ color: 'var(--text-m)' }}>
            <label className="flex items-center gap-1.5"><input type="checkbox" checked={get('taggedMe') === 'true'} onChange={e => setNamed('taggedMe', e.target.checked ? 'true' : '')} /> Etiketlendiklerim</label>
            <label className="flex items-center gap-1.5"><input type="checkbox" checked={get('unreadByMe') === 'true'} onChange={e => setNamed('unreadByMe', e.target.checked ? 'true' : '')} /> Okumadıklarım</label>
            <label className="flex items-center gap-1.5"><input type="checkbox" checked={get('includeHidden') === 'true'} onChange={e => setNamed('includeHidden', e.target.checked ? 'true' : '')} /> Gizlenenler</label>
            {(get('subjectId') || get('createdBy') || get('taggedMe') || get('unreadByMe') || get('includeHidden') || get('from') || get('to') || get('orderNumber') || get('customer') || get('trackingNo')) && (
              <button className="underline" style={{ color: 'var(--text-s)' }} onClick={() => grid.mutate(n => NAMED_KEYS.filter(k => k !== 'status').forEach(k => n.delete(k)))}>ek süzgeçleri temizle</button>
            )}
          </div>
        }
        rows={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={t => navigate(`/crm/tickets/${t.trackingNo}`)}
        empty="Kayıt yok."
        minWidth={980}
        export={{ endpoint: '/crm/tickets/export', named, fallbackFileName: 'talepler.xlsx' }}
      />
    </div>
  )
}
