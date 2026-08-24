/**
 * T4 Ayrıştırma (docs/urun-tedarik-is-akisi.md §3) — SİSTEMİN KALBİ, barkod okuyucu dostu operasyon ekranı.
 * Akış: parti seç (ya da partisiz) → barkod okut / ara → varyantı seç → adet gir → Kaydet (+ Etiket Bas).
 * K9: yalnız MEVCUT kartlar eşlenir; bulunamayan ürün için tek tıkla "kart eksik" bildirimi.
 * Yerleştirme (stok girişi) T5'te — kayıtlar "Bekliyor" rozetiyle listelenir.
 */
import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Printer, Search, Trash2, AlertTriangle, CheckCircle2 } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'

interface Cand { variantId: string; productId: string; productCode: string; name: string; sku: string; barcode: string | null; color: string | null; size: string | null; price: number; exact: boolean }
interface Entry { id: string; receiptBatchId: string | null; variantId: string; productCode: string; name: string; sku: string; barcode: string | null; quantity: number; unitCost: number | null; labelPrinted: boolean; labelCount: number; putawayStatus: string; createdAt: string }
interface Notice { id: string; receiptBatchId: string | null; descriptionText: string; status: string; createdAt: string }
interface BatchRow { id: string; code: string; supplierId: string; status: string }
interface Tpl { id: string; name: string; isDefault: boolean }

const tl = (n: number) => n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const num = (s: string) => { const v = parseFloat(s.replace(/\./g, '').replace(',', '.')); return isNaN(v) ? parseFloat(s.replace(',', '.')) : v }

export function SortingPage() {
  const qc = useQueryClient()
  const [batchId, setBatchId] = useState<string>(() => sessionStorage.getItem('sorting.batchId') ?? '')
  useEffect(() => { sessionStorage.setItem('sorting.batchId', batchId) }, [batchId])
  const [term, setTerm] = useState('')
  const [cands, setCands] = useState<Cand[] | null>(null)
  const [sel, setSel] = useState<Cand | null>(null)
  const [qty, setQty] = useState('')
  const [cost, setCost] = useState('')
  const [labelN, setLabelN] = useState('')
  const [msg, setMsg] = useState<string | null>(null)
  const [tplId, setTplId] = useState('')
  const searchRef = useRef<HTMLInputElement>(null)
  const qtyRef = useRef<HTMLInputElement>(null)

  // Ayrıştırılabilir partiler (received+sorting)
  const { data: batches = [] } = useQuery<BatchRow[]>({
    queryKey: ['sorting-batches'],
    queryFn: async () => {
      const [a, b] = await Promise.all([
        api.get('/procurement/receipts?status=received&pageSize=100'),
        api.get('/procurement/receipts?status=sorting&pageSize=100'),
      ])
      return [...(b.data.data?.items ?? []), ...(a.data.data?.items ?? [])]
    },
  })
  const { data: templates = [] } = useQuery<Tpl[]>({
    queryKey: ['label-templates-product'],
    queryFn: async () => (await api.get('/core/label-templates?targetType=product&activeOnly=true')).data.data,
  })
  useEffect(() => { if (!tplId && templates.length) setTplId((templates.find(t => t.isDefault) ?? templates[0]).id) }, [templates, tplId])

  const entriesKey = ['sorting-entries', batchId]
  const { data: entriesData, isLoading: entriesLoading } = useQuery<{ items: Entry[]; totalCount: number }>({
    queryKey: entriesKey,
    queryFn: async () => {
      const p = new URLSearchParams({ pageSize: '50' })
      if (batchId) p.set('batchId', batchId); else p.set('unbatched', 'true')
      return (await api.get(`/procurement/sorting/entries?${p}`)).data.data
    },
  })
  const { data: notices = [] } = useQuery<Notice[]>({
    queryKey: ['missing-cards', batchId],
    queryFn: async () => {
      const p = new URLSearchParams({ status: 'open' })
      if (batchId) p.set('batchId', batchId)
      return (await api.get(`/procurement/sorting/missing-cards?${p}`)).data.data
    },
  })
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: entriesKey }); qc.invalidateQueries({ queryKey: ['missing-cards', batchId] })
    qc.invalidateQueries({ queryKey: ['sorting-batches'] }); qc.invalidateQueries({ queryKey: ['receipt-batches'] })
  }
  const onErr = (e: any) => setMsg(e?.response?.data?.error ?? 'İşlem başarısız.')

  const doSearch = async () => {
    setMsg(null); setSel(null)
    const t = term.trim()
    if (t.length < 2) return
    const { data } = await api.get(`/procurement/sorting/lookup?term=${encodeURIComponent(t)}`)
    const list: Cand[] = data.data
    setCands(list)
    if (list.length === 1 && list[0].exact) { setSel(list[0]); setTimeout(() => qtyRef.current?.focus(), 50) }
  }

  const createMut = useMutation({
    mutationFn: async (v: { print: boolean }) => {
      const { data } = await api.post('/procurement/sorting/entries', {
        batchId: batchId || null, variantId: sel!.variantId, quantity: num(qty), unitCost: cost ? num(cost) : null,
      })
      return { id: data.data.id as string, print: v.print }
    },
    onSuccess: async (d) => {
      const n = labelN ? parseInt(labelN) : Math.max(1, Math.round(num(qty)))
      if (d.print && tplId && sel) {
        window.open(`/yazdir/etiket?templateId=${tplId}&variantId=${sel.variantId}&count=${n}`, '_blank')
        try { await api.post(`/procurement/sorting/entries/${d.id}/labeled`, { count: n }) } catch { /* sayaç */ }
      }
      setMsg(`Kaydedildi: ${sel?.sku} × ${qty}${d.print ? ` — ${n} etiket` : ''}`)
      setSel(null); setCands(null); setTerm(''); setQty(''); setCost(''); setLabelN('')
      invalidate(); searchRef.current?.focus()
    },
    onError: onErr,
  })
  const delMut = useMutation({
    mutationFn: async (id: string) => api.delete(`/procurement/sorting/entries/${id}`),
    onSuccess: invalidate, onError: onErr,
  })
  const printAgainMut = useMutation({
    mutationFn: async (e: Entry) => {
      const n = Math.max(1, Math.round(e.quantity))
      window.open(`/yazdir/etiket?templateId=${tplId}&variantId=${e.variantId}&count=${n}`, '_blank')
      return api.post(`/procurement/sorting/entries/${e.id}/labeled`, { count: n })
    },
    onSuccess: invalidate, onError: onErr,
  })
  const noticeMut = useMutation({
    mutationFn: async () => api.post('/procurement/sorting/missing-cards', {
      batchId: batchId || null, descriptionText: `Bulunamadı: "${term.trim()}"`,
    }),
    onSuccess: () => { setMsg('Kart eksik bildirimi düşüldü — katalog sorumlusu kartı açınca sayım yapılır.'); setCands(null); setTerm(''); invalidate(); searchRef.current?.focus() },
    onError: onErr,
  })
  const resolveMut = useMutation({
    mutationFn: async (id: string) => api.post(`/procurement/sorting/missing-cards/${id}/resolve`),
    onSuccess: invalidate, onError: onErr,
  })

  const entries = entriesData?.items ?? []
  const toplam = entries.reduce((s, e) => s + e.quantity, 0)

  return (
    <div className="p-6">
      <div className="mb-5">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ayrıştırma</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Barkodu okutun ya da arayın → adet girin → Kaydet + Etiket Bas. Sayım stok girişinin tek kaynağıdır;
          yerleştirme (rafa atama + stok) bir sonraki adımda yapılır. Katalogda olmayan ürün için kart açılmaz — bildirim düşülür.
        </p>
      </div>

      <div className="card mb-4 flex flex-wrap items-end gap-3">
        <div className="min-w-[260px]">
          <label className="flbl mb-1.5">Parti</label>
          <SearchableSelect value={batchId} onChange={v => setBatchId(v ?? '')}
            options={[{ value: '', label: '— Partisiz ayrıştırma —' }, ...batches.map(b => ({ value: b.id, label: `${b.code}` }))]}
            placeholder="— Partisiz ayrıştırma —" hasValue={!!batchId} />
        </div>
        <div className="min-w-[220px]">
          <label className="flbl mb-1.5">Etiket şablonu</label>
          <SearchableSelect value={tplId} onChange={v => setTplId(v ?? '')}
            options={templates.map(t => ({ value: t.id, label: t.name + (t.isDefault ? ' (varsayılan)' : '') }))}
            placeholder={templates.length ? 'Şablon seçin…' : 'Şablon yok — Etiket Şablonları sayfasından oluşturun'} hasValue={!!tplId} />
        </div>
      </div>

      {/* Arama + kayıt formu */}
      <div className="card mb-4 space-y-3">
        <div className="flex gap-2">
          <input ref={searchRef} autoFocus className="inp flex-1 font-mono" value={term}
            onChange={e => setTerm(e.target.value)} onKeyDown={e => e.key === 'Enter' && doSearch()}
            placeholder="Barkod okutun ya da SKU / ürün kodu / ad yazıp Enter…" />
          <Button variant="secondary" onClick={doSearch}><Search size={14} /> Ara</Button>
        </div>

        {cands !== null && cands.length === 0 && (
          <div className="flex flex-wrap items-center gap-3 rounded-lg px-3 py-2.5" style={{ background: '#fef3c7' }}>
            <AlertTriangle size={16} style={{ color: '#92400e' }} />
            <span className="text-sm" style={{ color: '#92400e' }}>"{term.trim()}" katalogda bulunamadı. Kart AÇMAYIN — bildirin:</span>
            <Button size="sm" variant="secondary" loading={noticeMut.isPending} onClick={() => noticeMut.mutate()}>Kart Eksik Bildir</Button>
          </div>
        )}
        {cands !== null && cands.length > 0 && !sel && (
          <ul className="divide-y rounded-lg overflow-hidden" style={{ border: '1px solid var(--border)', borderColor: 'var(--border)' }}>
            {cands.map(c => (
              <li key={c.variantId} className="px-3 py-2 text-sm cursor-pointer hover:opacity-80 flex justify-between"
                style={{ color: 'var(--text)' }} onClick={() => { setSel(c); setTimeout(() => qtyRef.current?.focus(), 50) }}>
                <span>{c.name} — {[c.color, c.size].filter(Boolean).join(' / ')} <code className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>{c.sku}</code></span>
                <span style={{ color: 'var(--text-m)' }}>{tl(c.price)} ₺</span>
              </li>
            ))}
          </ul>
        )}
        {sel && (
          <div className="flex flex-wrap items-end gap-3 rounded-lg px-3 py-3" style={{ background: 'var(--surface2)' }}>
            <div className="min-w-[220px] flex-1">
              <p className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{sel.name}</p>
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>
                {[sel.color, sel.size].filter(Boolean).join(' / ')} · {sel.sku} · {sel.barcode ?? 'barkodsuz'} · {tl(sel.price)} ₺
              </p>
            </div>
            <div className="w-24"><label className="flbl mb-1">Adet *</label>
              <input ref={qtyRef} className="inp" value={qty} onChange={e => setQty(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && num(qty) > 0 && createMut.mutate({ print: true })} /></div>
            <div className="w-28"><label className="flbl mb-1">Maliyet (ops.)</label>
              <input className="inp" value={cost} onChange={e => setCost(e.target.value)} /></div>
            <div className="w-24"><label className="flbl mb-1">Etiket ad.</label>
              <input className="inp" value={labelN} onChange={e => setLabelN(e.target.value)} placeholder="=adet" /></div>
            <Button loading={createMut.isPending} disabled={!(num(qty) > 0) || !tplId}
              onClick={() => createMut.mutate({ print: true })}><Printer size={14} /> Kaydet + Etiket Bas</Button>
            <Button variant="secondary" loading={createMut.isPending} disabled={!(num(qty) > 0)}
              onClick={() => createMut.mutate({ print: false })}>Yalnız Kaydet</Button>
          </div>
        )}
        {msg && <p className="text-sm" style={{ color: 'var(--text-s)' }}>{msg}</p>}
      </div>

      {/* Açık kart-eksik bildirimleri */}
      {notices.length > 0 && (
        <div className="card mb-4">
          <h2 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>Açık "kart eksik" bildirimleri ({notices.length})</h2>
          <ul className="space-y-1">
            {notices.map(n => (
              <li key={n.id} className="flex items-center justify-between text-sm rounded px-2 py-1.5" style={{ background: '#fef3c7' }}>
                <span style={{ color: '#92400e' }}>{n.descriptionText} <span className="text-xs">({new Date(n.createdAt).toLocaleString('tr-TR')})</span></span>
                <button className="text-xs underline" style={{ color: '#92400e' }} onClick={() => resolveMut.mutate(n.id)}>Kart açıldı — çöz</button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Sayım kayıtları */}
      <div className="card p-0 overflow-x-auto">
        <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
            {batchId ? 'Bu partinin sayımları' : 'Partisiz sayımlar'} — {entriesData?.totalCount ?? 0} kayıt · {toplam} adet
          </h2>
        </div>
        {entriesLoading ? <div className="py-8"><PageSpinner /></div> : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs" style={{ color: 'var(--text-s)', background: 'var(--surface2)' }}>
                {['ÜRÜN', 'SKU / BARKOD', 'ADET', 'MALİYET', 'ETİKET', 'YERLEŞTİRME', 'SAAT', ''].map(h => <th key={h} className="px-4 py-2.5 font-semibold">{h}</th>)}
              </tr>
            </thead>
            <tbody>
              {entries.map(e => (
                <tr key={e.id} style={{ borderTop: '1px solid var(--border)' }}>
                  <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{e.name}<span className="block text-xs" style={{ color: 'var(--text-s)' }}>{e.productCode}</span></td>
                  <td className="px-4 py-2 font-mono text-xs" style={{ color: 'var(--text-m)' }}>{e.sku}<span className="block">{e.barcode ?? ''}</span></td>
                  <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{e.quantity}</td>
                  <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{e.unitCost != null ? `${tl(e.unitCost)} ₺` : '—'}</td>
                  <td className="px-4 py-2">
                    {e.labelPrinted ? <span className="inline-flex items-center gap-1 text-xs" style={{ color: 'var(--text-m)' }}><CheckCircle2 size={13} /> {e.labelCount}</span>
                      : <button className="text-xs underline" style={{ color: 'var(--brand)' }} disabled={!tplId} onClick={() => printAgainMut.mutate(e)}>Bas</button>}
                  </td>
                  <td className="px-4 py-2">
                    {e.putawayStatus === 'placed' ? <Badge variant="success">Yerleşti</Badge> : <Badge variant="warning">Bekliyor</Badge>}
                  </td>
                  <td className="px-4 py-2 text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>{new Date(e.createdAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}</td>
                  <td className="px-4 py-2 text-right">
                    {e.putawayStatus !== 'placed' && (
                      <button className="p-1 rounded hover:opacity-70" title="Sayımı sil" onClick={() => delMut.mutate(e.id)}>
                        <Trash2 size={14} style={{ color: 'var(--text-s)' }} />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
              {entries.length === 0 && (
                <tr><td colSpan={8} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz sayım yok — barkod okutarak başlayın.</td></tr>
              )}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
