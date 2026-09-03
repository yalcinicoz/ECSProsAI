/**
 * T4 revizyonu — Etiket Basımı (yetkili personel): TOPLU ve KEYFİ, sayım üretmez.
 * Yığın listesi kurulur (ürün + deste adedi), "Tümünü Yazdır" tek sekmede tüm desteleri art arda basar;
 * her yığının destesi kendi ürünüyledir. Eksik çıkarsa satırdan ek basılır. Gerçek sayım: Sayım/Teslim ekranı.
 */
import { useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Printer, Search, Trash2, AlertTriangle } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { SearchableSelect } from '@/components/ui/SearchableSelect'

interface Cand { variantId: string; productCode: string; name: string; sku: string; barcode: string | null; color: string | null; size: string | null; price: number; exact: boolean }
interface Row extends Cand { count: number }
interface Tpl { id: string; name: string; isDefault: boolean }

const num = (s: string) => parseInt(s.replace(/\D/g, '')) || 0

export function LabelsPage() {
  const [tplId, setTplId] = useState('')
  const [term, setTerm] = useState('')
  const [cands, setCands] = useState<Cand[] | null>(null)
  const [sel, setSel] = useState<Cand | null>(null)
  const [cnt, setCnt] = useState('')
  const [rows, setRows] = useState<Row[]>([])
  const searchRef = useRef<HTMLInputElement>(null)
  const cntRef = useRef<HTMLInputElement>(null)

  const { data: templates = [] } = useQuery<Tpl[]>({
    queryKey: ['label-templates-product'],
    queryFn: async () => (await api.get('/core/label-templates?targetType=product&activeOnly=true')).data.data,
  })
  const selectedTplId = tplId || (templates.find(template => template.isDefault) ?? templates[0])?.id || ''

  const doSearch = async () => {
    setSel(null)
    const t = term.trim(); if (t.length < 2) return
    const { data } = await api.get(`/procurement/sorting/lookup?term=${encodeURIComponent(t)}`)
    const list: Cand[] = data.data
    setCands(list)
    if (list.length === 1 && list[0].exact) { setSel(list[0]); setTimeout(() => cntRef.current?.focus(), 50) }
  }
  const addRow = () => {
    if (!sel || num(cnt) <= 0) return
    setRows(prev => {
      const i = prev.findIndex(r => r.variantId === sel.variantId)
      if (i >= 0) return prev.map((r, j) => j === i ? { ...r, count: r.count + num(cnt) } : r)
      return [...prev, { ...sel, count: num(cnt) }]
    })
    setSel(null); setCands(null); setTerm(''); setCnt(''); searchRef.current?.focus()
  }
  const printItems = (items: Row[]) => {
    if (!selectedTplId || items.length === 0) return
    const q = items.map(r => `${r.variantId}:${r.count}`).join(',')
    window.open(`/yazdir/etiket?templateId=${selectedTplId}&items=${q}`, '_blank')
  }
  const toplam = rows.reduce((s, r) => s + r.count, 0)

  return (
    <div className="p-6">
      <div className="mb-5">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Etiket Basımı</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Ayrıştırılan yığınlar için desteler halinde etiket basın (beklenen ya da göz kararı adetle) — basım
          <b> sayım üretmez</b>; gerçek sayım depoya teslim okutmasıdır. Kendi etiketi olan (markalı) ürünler
          için basım gerekmez. Eksik çıkarsa satırdan ek basın; fazlası çöpe atılır.
        </p>
      </div>

      <div className="card mb-4 flex flex-wrap items-end gap-3">
        <div className="min-w-[240px]">
          <label className="flbl mb-1.5">Etiket şablonu</label>
          <SearchableSelect value={selectedTplId} onChange={v => setTplId(v ?? '')}
            options={templates.map(t => ({ value: t.id, label: t.name + (t.isDefault ? ' (varsayılan)' : '') }))}
            placeholder={templates.length ? 'Şablon seçin…' : 'Şablon yok — önce Etiket Şablonları'} hasValue={!!selectedTplId} />
        </div>
      </div>

      <div className="card mb-4 space-y-3">
        <div className="flex gap-2">
          <input ref={searchRef} autoFocus className="inp flex-1 font-mono" value={term}
            onChange={e => setTerm(e.target.value)} onKeyDown={e => e.key === 'Enter' && doSearch()}
            placeholder="Barkod okutun ya da SKU / ürün kodu / ad yazıp Enter…" />
          <Button variant="secondary" onClick={doSearch}><Search size={14} /> Ara</Button>
        </div>
        {cands !== null && cands.length === 0 && (
          <div className="flex items-center gap-2 rounded-lg px-3 py-2" style={{ background: '#fef3c7' }}>
            <AlertTriangle size={15} style={{ color: '#92400e' }} />
            <span className="text-sm" style={{ color: '#92400e' }}>"{term.trim()}" katalogda bulunamadı — kart açılmadan etiket basılamaz; Sayım/Teslim ekranından "Kart Eksik" bildirin.</span>
          </div>
        )}
        {cands !== null && cands.length > 0 && !sel && (
          <ul className="divide-y rounded-lg overflow-hidden" style={{ border: '1px solid var(--border)', borderColor: 'var(--border)' }}>
            {cands.map(c => (
              <li key={c.variantId} className="px-3 py-2 text-sm cursor-pointer hover:opacity-80"
                style={{ color: 'var(--text)' }} onClick={() => { setSel(c); setTimeout(() => cntRef.current?.focus(), 50) }}>
                {c.name} — {[c.color, c.size].filter(Boolean).join(' / ')} <code className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>{c.sku}</code>
              </li>
            ))}
          </ul>
        )}
        {sel && (
          <div className="flex flex-wrap items-end gap-3 rounded-lg px-3 py-3" style={{ background: 'var(--surface2)' }}>
            <div className="min-w-[220px] flex-1">
              <p className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{sel.name}</p>
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>{[sel.color, sel.size].filter(Boolean).join(' / ')} · {sel.sku}</p>
            </div>
            <div className="w-28"><label className="flbl mb-1">Deste adedi *</label>
              <input ref={cntRef} className="inp" value={cnt} onChange={e => setCnt(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && addRow()} /></div>
            <Button onClick={addRow} disabled={num(cnt) <= 0}>Listeye Ekle</Button>
          </div>
        )}
      </div>

      <div className="card p-0 overflow-x-auto">
        <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Basılacak desteler — {rows.length} yığın · {toplam} etiket</h2>
          <div className="flex gap-2">
            {rows.length > 0 && <Button size="sm" variant="secondary" onClick={() => setRows([])}>Temizle</Button>}
            <Button size="sm" disabled={!selectedTplId || rows.length === 0} onClick={() => printItems(rows)}>
              <Printer size={14} /> Tümünü Yazdır
            </Button>
          </div>
        </div>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs" style={{ color: 'var(--text-s)', background: 'var(--surface2)' }}>
              {['ÜRÜN', 'SKU', 'DESTE', ''].map(h => <th key={h} className="px-4 py-2.5 font-semibold">{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={r.variantId} style={{ borderTop: '1px solid var(--border)' }}>
                <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{r.name} <span className="text-xs" style={{ color: 'var(--text-s)' }}>{[r.color, r.size].filter(Boolean).join(' / ')}</span></td>
                <td className="px-4 py-2 font-mono text-xs" style={{ color: 'var(--text-m)' }}>{r.sku}</td>
                <td className="px-4 py-2">
                  <input className="inp w-20 !py-1" value={r.count}
                    onChange={e => setRows(p => p.map((x, j) => j === i ? { ...x, count: num(e.target.value) } : x))} />
                </td>
                <td className="px-4 py-2 text-right whitespace-nowrap">
                  <button className="text-xs underline mr-3" style={{ color: 'var(--brand)' }} disabled={!selectedTplId}
                    onClick={() => printItems([r])}>Bu desteyi bas</button>
                  <button className="p-1 rounded hover:opacity-70" onClick={() => setRows(p => p.filter((_, j) => j !== i))}>
                    <Trash2 size={14} style={{ color: 'var(--text-s)' }} />
                  </button>
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr><td colSpan={4} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Liste boş — ürün arayıp deste adedi girin.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
