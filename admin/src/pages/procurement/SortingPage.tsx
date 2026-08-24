/**
 * T4 revizyonu — Sayım / Depoya Teslim: GERÇEK SAYIM buradaki okutmadır (etiket basımı ayrı ve keyfî).
 * İki mod: OKUTMA (her okutma +1 — ürünler tek tek okutularak teslim edilir) ve ADET (barkod bir kez + adet —
 * yüksek adetli/markalı, kendi etiketli ürünler). Sayım varyant başına bekleyen kayıtta birikir; yerleştirme
 * (rafa atama + stok) T5. K9: katalogda olmayan ürün için kart AÇILMAZ — "kart eksik" bildirimi düşülür.
 */
import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Search, Trash2, AlertTriangle } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'

interface Cand { variantId: string; productCode: string; name: string; sku: string; barcode: string | null; color: string | null; size: string | null; price: number; exact: boolean }
interface Entry { id: string; receiptBatchId: string | null; variantId: string; productCode: string; name: string; sku: string; barcode: string | null; quantity: number; unitCost: number | null; putawayStatus: string; createdAt: string }
interface Notice { id: string; descriptionText: string; createdAt: string }
interface BatchRow { id: string; code: string; status: string }
interface BinCand { binId: string; code: string; barcode: string | null; section: string; warehouse: string; warehouseId: string; sellableOnline: boolean; exact: boolean }

const num = (s: string) => { const v = parseFloat(s.replace(/\./g, '').replace(',', '.')); return isNaN(v) ? parseFloat(s.replace(',', '.')) : v }

export function SortingPage() {
  const qc = useQueryClient()
  const [batchId, setBatchId] = useState<string>(() => sessionStorage.getItem('sorting.batchId') ?? '')
  useEffect(() => { sessionStorage.setItem('sorting.batchId', batchId) }, [batchId])
  const [tab, setTab] = useState<'sayim' | 'yerlestirme'>('sayim')
  const [mode, setMode] = useState<'scan' | 'qty'>('scan')
  // Yerleştirme sekmesi durumu
  const [binTerm, setBinTerm] = useState('')
  const [binCands, setBinCands] = useState<BinCand[] | null>(null)
  const [bin, setBin] = useState<BinCand | null>(null)
  const [placeSel, setPlaceSel] = useState<Set<string>>(new Set())
  const [placeQty, setPlaceQty] = useState<Record<string, string>>({})
  const binRef = useRef<HTMLInputElement>(null)
  const [term, setTerm] = useState('')
  const [cands, setCands] = useState<Cand[] | null>(null)
  const [sel, setSel] = useState<Cand | null>(null)
  const [qty, setQty] = useState('')
  const [cost, setCost] = useState('')
  const [last, setLast] = useState<string | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const qtyRef = useRef<HTMLInputElement>(null)

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

  const scanMut = useMutation({
    mutationFn: async (v: { variantId: string; quantity: number; unitCost?: number | null; label: string }) =>
      ({ res: (await api.post('/procurement/sorting/scan', { batchId: batchId || null, variantId: v.variantId, quantity: v.quantity, unitCost: v.unitCost ?? null })).data.data, label: v.label }),
    onSuccess: (d) => {
      setLast(`${d.label} → toplam ${d.res.quantity}`)
      setMsg(null); invalidate()
    },
    onError: onErr,
  })

  const doSearch = async () => {
    setMsg(null)
    const t = term.trim(); if (t.length < 2) return
    const { data } = await api.get(`/procurement/sorting/lookup?term=${encodeURIComponent(t)}`)
    const list: Cand[] = data.data
    if (list.length === 1 && list[0].exact) {
      const c = list[0]
      if (mode === 'scan') {
        // OKUTMA: anında +1, imleç aramada kalır
        scanMut.mutate({ variantId: c.variantId, quantity: 1, label: `${c.sku}` })
        setTerm(''); setCands(null); setSel(null); searchRef.current?.focus()
      } else {
        setSel(c); setCands(null); setTimeout(() => qtyRef.current?.focus(), 50)
      }
      return
    }
    setCands(list); setSel(null)
  }
  const pickCand = (c: Cand) => {
    if (mode === 'scan') {
      scanMut.mutate({ variantId: c.variantId, quantity: 1, label: c.sku })
      setTerm(''); setCands(null); searchRef.current?.focus()
    } else { setSel(c); setCands(null); setTimeout(() => qtyRef.current?.focus(), 50) }
  }
  const submitQty = () => {
    if (!sel || !(num(qty) > 0)) return
    scanMut.mutate({ variantId: sel.variantId, quantity: num(qty), unitCost: cost ? num(cost) : null, label: `${sel.sku} × ${qty}` })
    setSel(null); setTerm(''); setQty(''); setCost(''); searchRef.current?.focus()
  }

  const noticeMut = useMutation({
    mutationFn: async () => api.post('/procurement/sorting/missing-cards', {
      batchId: batchId || null, descriptionText: `Bulunamadı: "${term.trim()}"`,
    }),
    onSuccess: () => { setMsg('Kart eksik bildirimi düşüldü.'); setCands(null); setTerm(''); invalidate(); searchRef.current?.focus() },
    onError: onErr,
  })
  const resolveMut = useMutation({
    mutationFn: async (id: string) => api.post(`/procurement/sorting/missing-cards/${id}/resolve`),
    onSuccess: invalidate, onError: onErr,
  })
  const delMut = useMutation({
    mutationFn: async (id: string) => api.delete(`/procurement/sorting/entries/${id}`),
    onSuccess: invalidate, onError: onErr,
  })
  const editQtyMut = useMutation({
    mutationFn: async (v: { id: string; quantity: number; unitCost: number | null }) =>
      api.put(`/procurement/sorting/entries/${v.id}`, { quantity: v.quantity, unitCost: v.unitCost }),
    onSuccess: invalidate, onError: onErr,
  })

  const searchBin = async () => {
    setMsg(null)
    const t = binTerm.trim(); if (t.length < 2) return
    const { data } = await api.get(`/procurement/sorting/bins?term=${encodeURIComponent(t)}`)
    const list: BinCand[] = data.data
    if (list.length === 1 && list[0].exact) { setBin(list[0]); setBinCands(null); setBinTerm('') }
    else setBinCands(list)
  }
  const placeMut = useMutation({
    mutationFn: async (v: { id: string; quantity: number | null }) =>
      api.post(`/procurement/sorting/entries/${v.id}/place`, { binId: bin!.binId, quantity: v.quantity }),
    onError: onErr,
  })
  const placeSelected = async () => {
    if (!bin) return
    for (const id of Array.from(placeSel)) {
      const e = entries.find(x => x.id === id); if (!e) continue
      const raw = placeQty[id]; const q = raw ? num(raw) : null
      try { await placeMut.mutateAsync({ id, quantity: q && q > 0 && q < e.quantity ? q : null }) } catch { break }
    }
    setPlaceSel(new Set()); setPlaceQty({}); invalidate()
  }

  const entries = entriesData?.items ?? []
  const toplam = entries.reduce((s, e) => s + e.quantity, 0)
  const pendingEntries = entries.filter(e => e.putawayStatus !== 'placed')

  return (
    <div className="p-6">
      <div className="mb-5">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Sayım / Depoya Teslim</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Etiketleme biten ürünler depoya teslim için okutularak sayılır — <b>gerçek sayım budur</b>; stok girişi
          Yerleştirme adımında bu kayıtlardan yapılır. Okutma modunda her okutma +1; Adet modunda barkod bir kez
          okutulup adet girilir (markalı / kendi etiketli, yüksek adetli ürünler için).
        </p>
      </div>

      <div className="card mb-4 flex flex-wrap items-end gap-3">
        <div className="min-w-[260px]">
          <label className="flbl mb-1.5">Parti</label>
          <SearchableSelect value={batchId} onChange={v => setBatchId(v ?? '')}
            options={[{ value: '', label: '— Partisiz sayım —' }, ...batches.map(b => ({ value: b.id, label: b.code }))]}
            placeholder="— Partisiz sayım —" hasValue={!!batchId} />
        </div>
        <div>
          <label className="flbl mb-1.5">Sayım modu</label>
          <div className="flex rounded-xl p-1 gap-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {([['scan', 'Okutma (+1)'], ['qty', 'Adet girişi']] as const).map(([m, l]) => (
              <button key={m} onClick={() => { setMode(m); setSel(null); setCands(null); searchRef.current?.focus() }}
                className="px-3 py-1 rounded-lg text-sm font-medium"
                style={mode === m ? { background: 'var(--brand)', color: '#fff' } : { color: 'var(--text-m)' }}>{l}</button>
            ))}
          </div>
        </div>
        {last && <span className="text-sm pb-2" style={{ color: 'var(--text-m)' }}>Son: <b>{last}</b></span>}
      </div>

      <div className="tab-scroll mb-4">
        <button className={tab === 'sayim' ? 'stab active' : 'stab'} onClick={() => setTab('sayim')}>Sayım</button>
        <button className={tab === 'yerlestirme' ? 'stab active' : 'stab'} onClick={() => setTab('yerlestirme')}>
          Yerleştirme{pendingEntries.length > 0 ? ` (${pendingEntries.length})` : ''}
        </button>
      </div>

      {tab === 'yerlestirme' && (
        <div className="card mb-4 space-y-3">
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            Birim (raf) barkodunu okutun → yerleştirilecek sayımları seçin → <b>Yerleştir</b>: stok seçilen birime girer
            (hareket: satın alma, belge: sayım kaydı). Kısmi adet girilirse kalan bekleyen kayıtta kalır.
          </p>
          <div className="flex gap-2">
            <input ref={binRef} className="inp flex-1 font-mono" value={binTerm}
              onChange={e => setBinTerm(e.target.value)} onKeyDown={e => e.key === 'Enter' && searchBin()}
              placeholder="Birim (raf) barkodu okutun ya da kodunu yazın…" />
            <Button variant="secondary" onClick={searchBin}><Search size={14} /> Bul</Button>
          </div>
          {binCands !== null && binCands.length === 0 && (
            <p className="text-sm" style={{ color: '#92400e' }}>Birim bulunamadı.</p>
          )}
          {binCands !== null && binCands.length > 0 && (
            <ul className="divide-y rounded-lg overflow-hidden" style={{ border: '1px solid var(--border)', borderColor: 'var(--border)' }}>
              {binCands.map(c => (
                <li key={c.binId} className="px-3 py-2 text-sm cursor-pointer hover:opacity-80"
                  style={{ color: 'var(--text)' }} onClick={() => { setBin(c); setBinCands(null); setBinTerm('') }}>
                  <b>{c.code}</b> — {c.section} / {c.warehouse}{c.sellableOnline ? '' : ' (satışa kapalı kısım)'}
                </li>
              ))}
            </ul>
          )}
          {bin && (
            <div className="flex flex-wrap items-center gap-3 rounded-lg px-3 py-2.5" style={{ background: 'var(--surface2)' }}>
              <span className="text-sm" style={{ color: 'var(--text)' }}>
                Seçili birim: <b>{bin.code}</b> — {bin.section} / {bin.warehouse}
                {!bin.sellableOnline && <span style={{ color: '#92400e' }}> · satışa kapalı kısım (ürün sitede satılmaz)</span>}
              </span>
              <button className="text-xs underline" style={{ color: 'var(--text-s)' }} onClick={() => setBin(null)}>değiştir</button>
              <div className="flex-1" />
              <Button size="sm" disabled={placeSel.size === 0} loading={placeMut.isPending} onClick={placeSelected}>
                Seçilenleri Yerleştir ({placeSel.size})
              </Button>
            </div>
          )}
        </div>
      )}

      {tab === 'sayim' && (
      <div className="card mb-4 space-y-3">
        <div className="flex gap-2">
          <input ref={searchRef} autoFocus className="inp flex-1 font-mono" value={term}
            onChange={e => setTerm(e.target.value)} onKeyDown={e => e.key === 'Enter' && doSearch()}
            placeholder={mode === 'scan' ? 'Barkod okutun — her okutma +1 sayar…' : 'Barkod okutun ya da arayın; sonra adet girin…'} />
          <Button variant="secondary" onClick={doSearch}><Search size={14} /> Ara</Button>
        </div>

        {cands !== null && cands.length === 0 && (
          <div className="flex flex-wrap items-center gap-3 rounded-lg px-3 py-2.5" style={{ background: '#fef3c7' }}>
            <AlertTriangle size={16} style={{ color: '#92400e' }} />
            <span className="text-sm" style={{ color: '#92400e' }}>"{term.trim()}" katalogda bulunamadı. Kart AÇMAYIN — bildirin:</span>
            <Button size="sm" variant="secondary" loading={noticeMut.isPending} onClick={() => noticeMut.mutate()}>Kart Eksik Bildir</Button>
          </div>
        )}
        {cands !== null && cands.length > 0 && (
          <ul className="divide-y rounded-lg overflow-hidden" style={{ border: '1px solid var(--border)', borderColor: 'var(--border)' }}>
            {cands.map(c => (
              <li key={c.variantId} className="px-3 py-2 text-sm cursor-pointer hover:opacity-80 flex justify-between"
                style={{ color: 'var(--text)' }} onClick={() => pickCand(c)}>
                <span>{c.name} — {[c.color, c.size].filter(Boolean).join(' / ')} <code className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>{c.sku}</code></span>
                <span className="text-xs" style={{ color: 'var(--text-s)' }}>{mode === 'scan' ? '+1 say' : 'seç'}</span>
              </li>
            ))}
          </ul>
        )}
        {mode === 'qty' && sel && (
          <div className="flex flex-wrap items-end gap-3 rounded-lg px-3 py-3" style={{ background: 'var(--surface2)' }}>
            <div className="min-w-[220px] flex-1">
              <p className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{sel.name}</p>
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>{[sel.color, sel.size].filter(Boolean).join(' / ')} · {sel.sku} · {sel.barcode ?? 'barkodsuz'}</p>
            </div>
            <div className="w-24"><label className="flbl mb-1">Adet *</label>
              <input ref={qtyRef} className="inp" value={qty} onChange={e => setQty(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && submitQty()} /></div>
            <div className="w-28"><label className="flbl mb-1">Maliyet (ops.)</label>
              <input className="inp" value={cost} onChange={e => setCost(e.target.value)} /></div>
            <Button loading={scanMut.isPending} disabled={!(num(qty) > 0)} onClick={submitQty}>Say</Button>
          </div>
        )}
        {msg && <p className="text-sm" style={{ color: '#ef4444' }}>{msg}</p>}
      </div>
      )}
      {tab === 'yerlestirme' && msg && <p className="text-sm mb-3" style={{ color: '#ef4444' }}>{msg}</p>}

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

      <div className="card p-0 overflow-x-auto">
        <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
            {batchId ? 'Bu partinin sayımı' : 'Partisiz sayım'} — {entriesData?.totalCount ?? 0} ürün · {toplam} adet
          </h2>
        </div>
        {entriesLoading ? <div className="py-8"><PageSpinner /></div> : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs" style={{ color: 'var(--text-s)', background: 'var(--surface2)' }}>
                {tab === 'yerlestirme' && <th className="px-3 py-2.5 w-10"></th>}
                {['ÜRÜN', 'SKU / BARKOD', 'ADET', 'MALİYET', 'YERLEŞTİRME', 'SON', ''].map(h => <th key={h} className="px-4 py-2.5 font-semibold">{h}</th>)}
              </tr>
            </thead>
            <tbody>
              {entries.map(e => (
                <tr key={e.id} style={{ borderTop: '1px solid var(--border)' }}>
                  {tab === 'yerlestirme' && (
                    <td className="px-3 py-2">
                      {e.putawayStatus !== 'placed' && (
                        <input type="checkbox" checked={placeSel.has(e.id)}
                          onChange={() => setPlaceSel(prev => { const n = new Set(prev); n.has(e.id) ? n.delete(e.id) : n.add(e.id); return n })} />
                      )}
                    </td>
                  )}
                  <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{e.name}<span className="block text-xs" style={{ color: 'var(--text-s)' }}>{e.productCode}</span></td>
                  <td className="px-4 py-2 font-mono text-xs" style={{ color: 'var(--text-m)' }}>{e.sku}<span className="block">{e.barcode ?? ''}</span></td>
                  <td className="px-4 py-2">
                    {e.putawayStatus === 'placed' ? e.quantity
                      : tab === 'yerlestirme' ? (
                        <span className="inline-flex items-center gap-1.5">
                          {e.quantity}
                          {placeSel.has(e.id) && (
                            <input className="inp w-16 !py-1" placeholder="kısmi" value={placeQty[e.id] ?? ''}
                              onChange={ev => setPlaceQty(pq => ({ ...pq, [e.id]: ev.target.value }))} title="Boş = tamamı" />
                          )}
                        </span>
                      ) : (
                        <input className="inp w-20 !py-1" defaultValue={e.quantity} key={`${e.id}-${e.quantity}`}
                          onBlur={ev => { const v = num(ev.target.value); if (v > 0 && v !== e.quantity) editQtyMut.mutate({ id: e.id, quantity: v, unitCost: e.unitCost }) }} />
                      )}
                  </td>
                  <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{e.unitCost != null ? `${e.unitCost.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺` : '—'}</td>
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
                <tr><td colSpan={tab === 'yerlestirme' ? 8 : 7} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz sayım yok — barkod okutarak başlayın.</td></tr>
              )}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
