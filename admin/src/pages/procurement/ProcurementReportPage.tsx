/**
 * T6 Tedarik Raporu (docs/urun-tedarik-is-akisi.md §7): dönemsel mutabakat (SA ↔ sayım ↔ fatura —
 * İ4: kesin değildir, deneyimli personel yorumlar) + KPI kartları + satışa girmeyenler + kart-eksik.
 */
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'
import { useSuppliers } from './procurementHelpers'

interface Line { supplierId: string | null; supplierTitle: string; poCount: number; poQuantity: number; poAmount: number; countedQuantity: number; countedCost: number; invoiceAmount: number; diffQuantity: number }
interface Kpis { avgReceiptToCountHours: number | null; avgCountToOnSaleHours: number | null; pendingCount: number; pendingQuantity: number; pending0_2: number; pending3_7: number; pending7Plus: number; placedNotOnSaleCount: number; placedNotOnSaleQuantity: number; openMissingCards: number; oldestMissingCardDays: number | null }
interface NotOnSale { entryId: string; productId: string; productCode: string; name: string; sku: string; quantity: number; placedAt: string }
interface Report { from: string; to: string; lines: Line[]; kpis: Kpis; notOnSale: NotOnSale[] }
type LoadedReport = Report & { loadedAt: number }

const tl = (n: number) => n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const iso = (d: Date) => d.toISOString().slice(0, 10)
const sure = (h: number | null) => h == null ? '—' : h < 48 ? `${h.toFixed(1)} saat` : `${(h / 24).toFixed(1)} gün`

export function ProcurementReportPage() {
  const today = new Date()
  const [from, setFrom] = useState(iso(new Date(today.getTime() - 30 * 86400000)))
  const [to, setTo] = useState(iso(today))
  const [supplierId, setSupplierId] = useState('')
  const [applied, setApplied] = useState({ from, to, supplierId })
  const { data: suppliers = [] } = useSuppliers()

  const { data: rpt, isLoading } = useQuery<LoadedReport>({
    queryKey: ['procurement-report', applied],
    queryFn: async () => {
      const p = new URLSearchParams({ from: applied.from, to: applied.to })
      if (applied.supplierId) p.set('supplierId', applied.supplierId)
      const report = (await api.get(`/procurement/report?${p}`)).data.data as Report
      return { ...report, loadedAt: Date.now() }
    },
  })

  const k = rpt?.kpis
  const kart = (title: string, value: string, sub?: string, warn?: boolean) => (
    <div className="card" style={warn ? { borderColor: '#f59e0b' } : undefined}>
      <p className="text-xs" style={{ color: 'var(--text-s)' }}>{title}</p>
      <p className="text-xl font-bold mt-1" style={{ color: warn ? '#b45309' : 'var(--text)' }}>{value}</p>
      {sub && <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{sub}</p>}
    </div>
  )

  return (
    <div className="p-6">
      <div className="mb-5">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Tedarik Raporu</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Dönemsel mutabakat ve hız göstergeleri. Satın alınan ↔ sayılan karşılaştırması <b>kesin değildir</b> —
          belgeler kabadır, satıcı fazla gönderebilir; sonuçlar dönem bütününde yorumlanır.
        </p>
      </div>

      <div className="card mb-4 flex flex-wrap items-end gap-3">
        <div><label className="flbl mb-1">Başlangıç</label>
          <input type="date" className="inp" value={from} onChange={e => setFrom(e.target.value)} /></div>
        <div><label className="flbl mb-1">Bitiş (dahil)</label>
          <input type="date" className="inp" value={to} onChange={e => setTo(e.target.value)} /></div>
        <div className="min-w-[220px]"><label className="flbl mb-1">Tedarikçi</label>
          <SearchableSelect value={supplierId} onChange={v => setSupplierId(v ?? '')}
            options={[{ value: '', label: 'Tümü' }, ...suppliers.map(s => ({ value: s.id, label: s.title }))]}
            placeholder="Tümü" hasValue={!!supplierId} /></div>
        <Button onClick={() => setApplied({ from, to: iso(new Date(new Date(to).getTime() + 86400000)), supplierId })}>Uygula</Button>
      </div>

      {isLoading || !rpt ? <PageSpinner /> : (
        <>
          {/* KPI kartları */}
          <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-3 mb-4">
            {kart('Teslim → Sayım', sure(k!.avgReceiptToCountHours), 'ortalama (partili sayımlar)')}
            {kart('Sayım → Satışa Giriş', sure(k!.avgCountToOnSaleHours), 'ortalama (damgalılar)')}
            {kart('Yerleştirme Bekleyen', `${k!.pendingCount} kayıt`, `${k!.pendingQuantity} adet · yaş: ${k!.pending0_2}/${k!.pending3_7}/${k!.pending7Plus} (0-2/3-7/7+ gün)`, k!.pending7Plus > 0)}
            {kart('Satışa Girmeyen', `${k!.placedNotOnSaleCount} kayıt`, `${k!.placedNotOnSaleQuantity} adet yerleşti ama yayında değil`, k!.placedNotOnSaleCount > 0)}
            {kart('Açık Kart-Eksik', String(k!.openMissingCards), k!.oldestMissingCardDays != null ? `en eskisi ${k!.oldestMissingCardDays.toFixed(1)} gün` : undefined, k!.openMissingCards > 0)}
            {kart('Dönem', `${new Date(rpt.from).toLocaleDateString('tr-TR')} – ${new Date(new Date(rpt.to).getTime() - 1).toLocaleDateString('tr-TR')}`)}
          </div>

          {/* Mutabakat */}
          <div className="card p-0 overflow-x-auto mb-4">
            <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
              <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Dönemsel Mutabakat (kesin değildir)</h2>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs" style={{ color: 'var(--text-s)', background: 'var(--surface2)' }}>
                  {['TEDARİKÇİ', 'SA', 'SA ADET', 'SA TUTAR', 'SAYILAN ADET', 'SAYIM MALİYETİ', 'FATURA', 'FARK (SAYIM−SA)'].map(h =>
                    <th key={h} className="px-4 py-2.5 font-semibold">{h}</th>)}
                </tr>
              </thead>
              <tbody>
                {rpt.lines.map(l => (
                  <tr key={l.supplierId ?? '-'} style={{ borderTop: '1px solid var(--border)' }}>
                    <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{l.supplierTitle || '—'}</td>
                    <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{l.poCount}</td>
                    <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{l.poQuantity}</td>
                    <td className="px-4 py-2 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{tl(l.poAmount)} ₺</td>
                    <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{l.countedQuantity}</td>
                    <td className="px-4 py-2 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{l.countedCost > 0 ? `${tl(l.countedCost)} ₺` : '—'}</td>
                    <td className="px-4 py-2 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{l.invoiceAmount > 0 ? `${tl(l.invoiceAmount)} ₺` : '—'}</td>
                    <td className="px-4 py-2 font-semibold" style={{ color: l.diffQuantity > 0 ? '#b45309' : l.diffQuantity < 0 ? '#b91c1c' : 'var(--text-m)' }}>
                      {l.diffQuantity > 0 ? `+${l.diffQuantity} (fazla gönderim)` : l.diffQuantity < 0 ? `${l.diffQuantity} (eksik)` : '0'}
                      {l.poQuantity > 0 && ` · %${Math.round(Math.abs(l.diffQuantity) / l.poQuantity * 100)}`}
                    </td>
                  </tr>
                ))}
                {rpt.lines.length === 0 && (
                  <tr><td colSpan={8} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Dönemde kayıt yok.</td></tr>
                )}
              </tbody>
            </table>
          </div>

          {/* Satışa girmeyenler */}
          <div className="card p-0 overflow-x-auto">
            <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
              <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
                Satışa Girmeyenler — yerleşti ama sitede yayında değil ({rpt.notOnSale.length}{rpt.notOnSale.length === 100 ? '+' : ''})
              </h2>
              <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                Sebep için ürüne gidin (görsel yok, fiyat 0, kanal kararı…) — Kanal Ürünleri ekranı sebep rozetlerini gösterir.
                Damga 6 saatte bir işler; yeni yerleşenler bir sonraki turda değerlendirilir.
              </p>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs" style={{ color: 'var(--text-s)', background: 'var(--surface2)' }}>
                  {['ÜRÜN', 'SKU', 'ADET', 'YERLEŞME', 'BEKLEME', ''].map(h => <th key={h} className="px-4 py-2.5 font-semibold">{h}</th>)}
                </tr>
              </thead>
              <tbody>
                {rpt.notOnSale.map(r => {
                  const gun = (rpt.loadedAt - new Date(r.placedAt).getTime()) / 86400000
                  return (
                    <tr key={r.entryId} style={{ borderTop: '1px solid var(--border)' }}>
                      <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{r.name}<span className="block text-xs" style={{ color: 'var(--text-s)' }}>{r.productCode}</span></td>
                      <td className="px-4 py-2 font-mono text-xs" style={{ color: 'var(--text-m)' }}>{r.sku}</td>
                      <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{r.quantity}</td>
                      <td className="px-4 py-2 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{new Date(r.placedAt).toLocaleDateString('tr-TR')}</td>
                      <td className="px-4 py-2" style={{ color: gun > 7 ? '#b45309' : 'var(--text-m)' }}>{gun.toFixed(1)} gün</td>
                      <td className="px-4 py-2 text-right">
                        <Link to={`/catalog/products/${r.productCode}`} className="text-xs underline" style={{ color: 'var(--brand)' }}>Ürüne git</Link>
                      </td>
                    </tr>
                  )
                })}
                {rpt.notOnSale.length === 0 && (
                  <tr><td colSpan={6} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Satışa girmeyen yerleşmiş ürün yok 🎉</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  )
}
