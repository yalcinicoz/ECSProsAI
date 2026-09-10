import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { errText } from '@/components/ui/DataTable.utils'
import { ORDER_STATUS_MAP } from '@/pages/orders/orderConstants'

/** FAZ 15.4f (2026-09-10, eski "Set Koli Sipariş Sorgula"): set (toplama görevi) / koli / sipariş no / tarih / personel ile
 * kolideki siparişleri masa-yuva bilgisiyle listeler. Sipariş no girilirse o siparişin KOLİSİNDEKİ tüm siparişler döner. */

interface Row {
  planId: string; planNumber: string; plannedAt: string; planStatus: string
  boxId?: string | null; boxNumber?: number | null; boxStatus?: string | null; takenBy?: string | null; takenAt?: string | null; stationNumber?: number | null
  deskNumber?: number | null; deskSlotNumber?: number | null; binNumber: number; binStatus: string
  orderId: string; orderNumber: string; orderStatus?: string | null; customer?: string | null; orderCreatedAt: string; invoiceDate?: string | null
  lineCount: number; pickedLines: number
}
interface User { id: string; username: string; firstName: string; lastName: string }

const bos = { planNumber: '', boxNumber: '', orderNumber: '', from: '', to: '', takenBy: '' }

export function BoxLookupPage() {
  const [form, setForm] = useState(bos)
  const [q, setQ] = useState<typeof bos | null>(null)
  const { data: users = [] } = useQuery<User[]>({ queryKey: ['iam-users-select-mini'], queryFn: async () => (await api.get('/iam/users?page=1&pageSize=200')).data.data.items, staleTime: 5 * 60_000 })
  const ad = (id?: string | null) => { const u = users.find(x => x.id === id); return u ? (`${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username) : (id ? id.slice(0, 8) : '—') }

  const { data: rows = [], isFetching, error } = useQuery<Row[]>({
    queryKey: ['box-lookup', q],
    queryFn: async () => {
      const p = new URLSearchParams()
      if (q!.planNumber) p.set('planNumber', q!.planNumber.trim())
      if (q!.boxNumber) p.set('boxNumber', q!.boxNumber)
      if (q!.orderNumber) p.set('orderNumber', q!.orderNumber.trim())
      if (q!.from) p.set('from', new Date(q!.from).toISOString())
      if (q!.to) { const d = new Date(q!.to); d.setDate(d.getDate() + 1); p.set('to', d.toISOString()) }
      if (q!.takenBy) p.set('takenBy', q!.takenBy)
      return (await api.get(`/fulfillment/box-lookup?${p}`)).data.data
    },
    enabled: q !== null,
    retry: false,
  })

  const f = (k: keyof typeof bos) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setForm(x => ({ ...x, [k]: e.target.value }))
  const koliler = rows.reduce<Record<string, Row[]>>((acc, r) => { const k = `${r.planNumber} / ${r.boxNumber ?? '—'}`; (acc[k] ??= []).push(r); return acc }, {})

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>Koli Sorgu</h1>
        <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>Set (toplama görevi) · koli · sipariş no ile kolideki siparişler, masa ve yuva bilgisi. Sipariş no girilirse o siparişin kolisinin tamamı gelir.</p>
      </div>
      <form className="card p-4 mb-4 grid grid-cols-2 md:grid-cols-6 gap-3 items-end" onSubmit={e => { e.preventDefault(); setQ({ ...form }) }}>
        <div><label className="flbl">Set / görev no</label><input className="inp font-mono" value={form.planNumber} onChange={f('planNumber')} placeholder="PP-2026…" /></div>
        <div><label className="flbl">Koli no</label><input className="inp" type="number" min={1} value={form.boxNumber} onChange={f('boxNumber')} /></div>
        <div><label className="flbl">Sipariş no</label><input className="inp font-mono" value={form.orderNumber} onChange={f('orderNumber')} /></div>
        <div><label className="flbl">Başlangıç</label><input className="inp" type="date" value={form.from} onChange={f('from')} /></div>
        <div><label className="flbl">Bitiş</label><input className="inp" type="date" value={form.to} onChange={f('to')} /></div>
        <div><label className="flbl">Personel (koli zimmeti)</label>
          <select className="inp" value={form.takenBy} onChange={f('takenBy')}><option value="">Hepsi</option>{users.map(u => <option key={u.id} value={u.id}>{ad(u.id)}</option>)}</select></div>
        <div className="col-span-2 md:col-span-6 flex gap-2 justify-end">
          <Button type="button" variant="secondary" onClick={() => { setForm(bos); setQ(null) }}>Temizle</Button>
          <Button type="submit" loading={isFetching}>Sorgula</Button>
        </div>
      </form>
      {error && <p className="text-sm mb-3 text-red-500">{errText(error)}</p>}
      {q && !isFetching && rows.length === 0 && !error && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Kayıt bulunamadı.</p>}
      {Object.entries(koliler).map(([k, list]) => {
        const ilk = list[0]
        return (
          <div key={k} className="card p-0 overflow-hidden mb-4">
            <div className="flex flex-wrap items-center gap-3 px-4 py-2" style={{ background: 'var(--surface2)', borderBottom: '1px solid var(--border)' }}>
              <span className="text-sm font-bold font-mono" style={{ color: 'var(--text)' }}>Set {ilk.planNumber}</span>
              <span className="text-sm" style={{ color: 'var(--text-m)' }}>Koli <b>{ilk.boxNumber ?? '—'}</b>{ilk.boxStatus && <> · {ilk.boxStatus}</>}</span>
              {ilk.deskNumber != null && <span className="text-sm" style={{ color: 'var(--text-m)' }}>Masa <b>{ilk.deskNumber}</b></span>}
              {ilk.stationNumber != null && <span className="text-sm" style={{ color: 'var(--text-m) ' }}>İstasyon {ilk.stationNumber}</span>}
              {ilk.takenBy && <span className="text-xs" style={{ color: 'var(--text-s)' }}>zimmet: {ad(ilk.takenBy)}{ilk.takenAt && ` · ${new Date(ilk.takenAt).toLocaleString('tr-TR')}`}</span>}
              <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>{list.length} sipariş · plan {new Date(ilk.plannedAt).toLocaleString('tr-TR')}</span>
              <Link to={`/fulfillment/sorting-wall/${ilk.planId}`} className="text-xs underline" style={{ color: 'var(--brand)' }}>Koli duvarı →</Link>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead><tr style={{ borderBottom: '1px solid var(--border)' }}>
                  {['YUVA', 'GÖZ', 'SİPARİŞ', 'MÜŞTERİ', 'SİPARİŞ TARİHİ', 'DURUM', 'TOPLAMA', 'FATURA TARİHİ'].map(h => <th key={h} className="text-left px-4 py-2 text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>)}
                </tr></thead>
                <tbody>
                  {list.map(r => { const st = r.orderStatus ? (ORDER_STATUS_MAP[r.orderStatus] ?? { label: r.orderStatus, variant: 'neutral' as const }) : null; return (
                    <tr key={r.orderId} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td className="px-4 py-1.5 font-mono">{r.deskSlotNumber ?? '—'}</td>
                      <td className="px-4 py-1.5 font-mono">{r.binNumber} <span className="text-xs" style={{ color: 'var(--text-s)' }}>{r.binStatus}</span></td>
                      <td className="px-4 py-1.5"><Link to={`/orders/${r.orderId}`} className="font-mono underline" style={{ color: 'var(--brand)' }}>{r.orderNumber}</Link></td>
                      <td className="px-4 py-1.5" style={{ color: 'var(--text-m)' }}>{r.customer ?? '—'}</td>
                      <td className="px-4 py-1.5 text-xs" style={{ color: 'var(--text-s)' }}>{new Date(r.orderCreatedAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}</td>
                      <td className="px-4 py-1.5">{st ? <Badge variant={st.variant}>{st.label}</Badge> : '—'}</td>
                      <td className="px-4 py-1.5 text-xs" style={{ color: 'var(--text-m)' }}>{r.pickedLines}/{r.lineCount} satır</td>
                      <td className="px-4 py-1.5 text-xs" style={{ color: 'var(--text-s)' }}>{r.invoiceDate ? new Date(r.invoiceDate).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' }) : '—'}</td>
                    </tr>) })}
                </tbody>
              </table>
            </div>
          </div>
        )
      })}
    </div>
  )
}
