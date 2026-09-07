// Müşteri İlişkileri — kayıt listesi (eski /crm/musteri-iliskileri-yonetimi). Varsayılan: gizli olmayan durumlar;
// sayaç kutuları durum süzgeci; satır tıklama → detay (takip no). "Kontrol" sütunu = son işlemi ben okudum mu.
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Pagination } from '@/components/ui/Pagination'
import { PageSpinner } from '@/components/ui/Spinner'
import { cn } from '@/lib/utils'
import { TYPE_LABEL, fmtTarih, fmtTelefon, useTicketSettings, useAdminUsers, usePlatformNames, type TicketPage } from './ticketShared'

export function TicketsPage() {
  const navigate = useNavigate()
  const [sp, setSp] = useSearchParams()
  const { data: settings } = useTicketSettings(true)
  const { data: users = [] } = useAdminUsers()
  const { data: platformNames = {} } = usePlatformNames()

  const get = (k: string) => sp.get(k) ?? ''
  const set = (k: string, v: string) => setSp((p) => { const n = new URLSearchParams(p); if (v) n.set(k, v); else n.delete(k); if (k !== 'page') n.delete('page'); return n }, { replace: true })
  const [search, setSearch] = useState(get('search'))
  const [customer, setCustomer] = useState(get('customer'))
  const [orderNumber, setOrderNumber] = useState(get('orderNumber'))
  const [trackingNo, setTrackingNo] = useState(get('trackingNo'))

  const params = Object.fromEntries([...sp.entries()].filter(([, v]) => v))
  const { data, isLoading } = useQuery<TicketPage>({
    queryKey: ['tickets', params],
    queryFn: async () => (await api.get('/crm/tickets', { params: { pageSize: 25, ...params } })).data.data,
  })
  const status = get('status')

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

      {/* Sayaçlar — tıklanınca durum süzgeci */}
      <div className="flex flex-wrap gap-2 mb-4">
        <button onClick={() => set('status', '')} className={cn('px-3 py-2 rounded-xl text-sm', !status && 'ring-2')}
          style={{ background: 'var(--surface2)', color: 'var(--text)', border: '1px solid var(--border)' }}>
          Toplam <b>{(data?.counters ?? []).filter((c) => !settings?.statuses.find((s) => s.code === c.statusCode)?.isHidden).reduce((a, c) => a + c.count, 0)}</b>
        </button>
        {(settings?.statuses ?? []).map((s) => {
          const n = data?.counters.find((c) => c.statusCode === s.code)?.count ?? 0
          return (
            <button key={s.code} onClick={() => set('status', status === s.code ? '' : s.code)}
              className={cn('px-3 py-2 rounded-xl text-sm', status === s.code && 'ring-2')}
              title={s.isHidden ? 'Gizli durum — yalnız bu süzgeçle görünür' : undefined}
              style={{ background: `${s.color}18`, color: s.color, border: `1px solid ${s.color}55`, opacity: s.isHidden ? 0.7 : 1 }}>
              {s.name} <b>{n}</b>
            </button>
          )
        })}
      </div>

      {/* Süzgeçler */}
      <div className="card p-3 mb-4 flex flex-wrap items-end gap-2">
        <div><label className="flbl">Takip No</label><input className="inp font-mono" style={{ width: 130 }} value={trackingNo} onChange={(e) => setTrackingNo(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && set('trackingNo', trackingNo)} /></div>
        <div><label className="flbl">Sipariş No</label><input className="inp font-mono" style={{ width: 150 }} value={orderNumber} onChange={(e) => setOrderNumber(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && set('orderNumber', orderNumber)} /></div>
        <div><label className="flbl">Müşteri / Arayan (ad ya da telefon)</label><input className="inp" style={{ width: 220 }} value={customer} onChange={(e) => setCustomer(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && set('customer', customer)} /></div>
        <div><label className="flbl">İçerikte ara</label><input className="inp" style={{ width: 200 }} value={search} onChange={(e) => setSearch(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && set('search', search)} /></div>
        <div><label className="flbl">Tür</label>
          <select className="inp" value={get('type')} onChange={(e) => set('type', e.target.value)}><option value="">Tümü</option><option value="complaint">Şikayet</option><option value="request">Talep</option></select></div>
        <div><label className="flbl">Konu</label>
          <select className="inp" value={get('subjectId')} onChange={(e) => set('subjectId', e.target.value)}><option value="">Tümü</option>{(settings?.subjects ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></div>
        <div><label className="flbl">Kayıt açan</label>
          <select className="inp" value={get('createdBy')} onChange={(e) => set('createdBy', e.target.value)}><option value="">Tümü</option>{users.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}</select></div>
        <div><label className="flbl">Tarih</label><div className="flex gap-1"><input type="date" className="inp" value={get('from')} onChange={(e) => set('from', e.target.value)} /><input type="date" className="inp" value={get('to')} onChange={(e) => set('to', e.target.value)} /></div></div>
        <label className="flex items-center gap-1.5 text-xs pb-2" style={{ color: 'var(--text-m)' }}><input type="checkbox" checked={get('taggedMe') === 'true'} onChange={(e) => set('taggedMe', e.target.checked ? 'true' : '')} /> Etiketlendiklerim</label>
        <label className="flex items-center gap-1.5 text-xs pb-2" style={{ color: 'var(--text-m)' }}><input type="checkbox" checked={get('unreadByMe') === 'true'} onChange={(e) => set('unreadByMe', e.target.checked ? 'true' : '')} /> Okumadıklarım</label>
        <label className="flex items-center gap-1.5 text-xs pb-2" style={{ color: 'var(--text-m)' }}><input type="checkbox" checked={get('includeHidden') === 'true'} onChange={(e) => set('includeHidden', e.target.checked ? 'true' : '')} /> Gizlenenler</label>
        <Button size="sm" variant="secondary" onClick={() => { set('search', search); set('customer', customer); set('orderNumber', orderNumber); set('trackingNo', trackingNo) }}>Ara</Button>
        <button className="text-xs underline pb-2" style={{ color: 'var(--text-s)' }} onClick={() => { setSearch(''); setCustomer(''); setOrderNumber(''); setTrackingNo(''); setSp(new URLSearchParams(), { replace: true }) }}>temizle</button>
      </div>

      {isLoading || !data ? <PageSpinner /> : (
        <div className="card overflow-hidden p-0">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr style={{ background: 'var(--surface2)' }}>
                  {['Takip No', 'Tür / Konu', 'Sipariş', 'Müşteri', 'Arayan', 'Kayıt', 'Son İşlem', 'Kontrol', 'Durum'].map((h) => (
                    <th key={h} className="px-3 py-2 text-left text-xs font-semibold whitespace-nowrap" style={{ color: 'var(--text-s)' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.items.map((t) => (
                  <tr key={t.id} onClick={() => navigate(`/crm/tickets/${t.trackingNo}`)} className="cursor-pointer hover:opacity-90"
                    style={{ borderTop: '1px solid var(--border)', background: t.isHidden ? 'var(--surface2)' : undefined, borderLeft: `4px solid ${t.statusColor}` }}>
                    <td className="px-3 py-2 font-mono whitespace-nowrap" style={{ color: 'var(--text)' }}>
                      {t.trackingNo}
                      {t.taggedMe && <span className="ml-1 text-[10px] px-1 rounded" style={{ background: '#3b82f620', color: '#3b82f6' }}>etiket</span>}
                      {t.isHidden && <span className="ml-1 text-[10px] px-1 rounded" style={{ background: '#6b728020', color: '#6b7280' }}>gizli</span>}
                    </td>
                    <td className="px-3 py-2" style={{ color: 'var(--text)' }}>
                      <span className="text-xs" style={{ color: t.type === 'complaint' ? '#ef4444' : '#3b82f6' }}>{TYPE_LABEL[t.type] ?? t.type}</span><br />{t.subjectName}
                    </td>
                    <td className="px-3 py-2 font-mono text-xs whitespace-nowrap" style={{ color: 'var(--text)' }}>
                      {t.orderNumber ?? '—'}{t.firmPlatformId && platformNames[t.firmPlatformId] && <><br /><span style={{ color: 'var(--text-s)' }}>{platformNames[t.firmPlatformId]}</span></>}
                    </td>
                    <td className="px-3 py-2" style={{ color: 'var(--text)' }}>{t.customerName || '—'}<br /><span className="text-xs" style={{ color: 'var(--text-s)' }}>{fmtTelefon(t.customerPhone)}</span></td>
                    <td className="px-3 py-2" style={{ color: 'var(--text)' }}>{t.callerName || '—'}<br /><span className="text-xs" style={{ color: 'var(--text-s)' }}>{fmtTelefon(t.callerPhone)}</span></td>
                    <td className="px-3 py-2 text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{fmtTarih(t.createdAt)}<br />{t.createdByName}</td>
                    <td className="px-3 py-2 text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{fmtTarih(t.lastActivityAt)}<br />{t.updatedByName ?? ''} {t.activityCount > 0 && <span style={{ color: 'var(--text-s)' }}>({t.activityCount})</span>}</td>
                    <td className="px-3 py-2 text-xs whitespace-nowrap">
                      {t.readByMe ? <span style={{ color: '#22c55e' }}>Kontrol edildi</span> : <span className="font-semibold" style={{ color: '#ef4444' }}>Kontrol edilmedi</span>}
                    </td>
                    <td className="px-3 py-2 whitespace-nowrap"><span className="text-xs px-2 py-0.5 rounded-full font-medium" style={{ background: `${t.statusColor}20`, color: t.statusColor }}>{t.statusName}</span></td>
                  </tr>
                ))}
                {data.items.length === 0 && <tr><td colSpan={9} className="px-3 py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Kayıt yok.</td></tr>}
              </tbody>
            </table>
          </div>
          <div className="p-3" style={{ borderTop: '1px solid var(--border)' }}>
            <Pagination page={data.page} totalPages={Math.max(1, Math.ceil(data.totalCount / data.pageSize))} totalCount={data.totalCount} pageSize={data.pageSize} onChange={(p) => set('page', String(p))} />
          </div>
        </div>
      )}
    </div>
  )
}
