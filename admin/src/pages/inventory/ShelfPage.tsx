import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useSearchParams, Link } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { errText } from '@/components/ui/DataTable.utils'
import { basariSesi, hataSesi } from '@/lib/sesler'
import { useAuthStore } from '@/store/auth'
import { cn } from '@/lib/utils'

/**
 * FAZ 15.3 — Raf / Göz İşlemleri (tablet okutma ekran ailesi; docs/raf-operasyon-ekranlari-plani.md).
 * Tek okutma kutusu: sunucu barkodun GÖZ mü ÜRÜN mü olduğunu söyler (/inventory/shelf/resolve).
 * Kipler: içerik · yerleştir · taşı · iadeden rafa · sayım · mağaza reyon.
 * ★ Stok otoritesi eskideyken (Legacy:Sync:Stock) yazan işlemler kapalıdır (sarı şerit) — sunucu da 409 döner.
 */

type Mode = 'contents' | 'place' | 'move' | 'return' | 'count' | 'store'
const MODES: { key: Mode; label: string; hint: string }[] = [
  { key: 'contents', label: 'Raf İçeriği', hint: 'Göz okut → içindekiler. Ürün okut → hangi gözlerde.' },
  { key: 'place', label: 'Rafa Yerleştir', hint: 'Önce hedef gözü, sonra ürünleri okut. Her okutma +1 (adet kutusuyla toplu).' },
  { key: 'move', label: 'Raftan Rafa', hint: 'Kaynak gözü, sonra hedef gözü okut; ürün okutarak tek tek ya da "Tümünü Taşı".' },
  { key: 'return', label: 'İadeden Rafa', hint: 'Ürünü okut (iade/defo kısmındaki gözü bulunur), sonra hedef gözü okut.' },
  { key: 'count', label: 'Raf Sayım', hint: 'Gözü okut → sayım açılır; ürünleri okut; Bitir → fark; Uygula (yetkili).' },
  { key: 'store', label: 'Mağaza Reyon', hint: 'Kaynak ve hedef depoyu seç, ürünü okut → anında taşınır.' },
]

interface Warehouse { id: string; code: string; nameI18n: Record<string, string>; warehouseType: string; isActive: boolean }
interface BinItem { variantId: string; quantity: number; reservedQuantity: number; availableQuantity: number; productCode?: string | null; productName?: string | null; optionsText?: string | null; imageUrl?: string | null; sku?: string | null }
interface BinContents {
  binId: string; binCode: string; binBarcode: string; binName?: string | null; binActive: boolean
  sectionId: string; sectionCode: string; sectionName: string; sectionSellableOnline: boolean
  warehouseId: string; warehouseCode: string; warehouseName: string
  totalQuantity: number; totalReserved: number; items: BinItem[]
}
interface VariantBin { warehouseId: string; warehouseCode: string; sectionId: string; sectionCode: string; sectionSellableOnline: boolean; binId: string; binCode: string; binBarcode: string; quantity: number; reservedQuantity: number }
interface VariantInfo { variantId: string; productId: string; productCode: string; sku: string; productName: string; optionsText?: string | null; imageUrl?: string | null }
type Resolved = { kind: 'bin'; bin: BinContents } | { kind: 'variant'; variant: VariantInfo; bins: VariantBin[] } | { kind: 'none' }
interface CountLine { id: string; variantId: string; expected: number; counted: number; diff: number; productCode?: string | null; productName?: string | null; optionsText?: string | null }
interface CountDetail { header: { id: string; status: string; binCode: string; expectedTotal: number; countedTotal: number; diffTotal: number }; lines: CountLine[] }
interface Authority { authority: 'legacy' | 'panel'; legacyOwnsStock: boolean; message?: string | null }
interface LogRow { t: number; ok: boolean; text: string }

const wName = (w: Warehouse) => w.nameI18n?.tr ?? w.code

function UrunSatir({ i }: { i: { productName?: string | null; productCode?: string | null; optionsText?: string | null; imageUrl?: string | null; sku?: string | null } }) {
  return (
    <div className="flex items-center gap-3 min-w-0">
      {i.imageUrl ? <img src={i.imageUrl} alt="" className="w-10 h-12 object-cover rounded-md shrink-0" style={{ border: '1px solid var(--border)' }} />
        : <div className="w-10 h-12 rounded-md shrink-0" style={{ background: 'var(--surface2)' }} />}
      <div className="min-w-0">
        <div className="text-sm font-medium truncate" style={{ color: 'var(--text)' }}>{i.productName ?? i.productCode ?? '—'}</div>
        <div className="text-xs truncate" style={{ color: 'var(--text-s)' }}>{[i.productCode, i.optionsText].filter(Boolean).join(' · ')}</div>
      </div>
    </div>
  )
}

export function ShelfPage() {
  const [sp, setSp] = useSearchParams()
  const mode = (MODES.find(m => m.key === sp.get('mode'))?.key ?? 'contents') as Mode
  const setMode = (m: Mode) => { setSp(p => { const n = new URLSearchParams(p); n.set('mode', m); return n }, { replace: true }); reset() }
  const qc = useQueryClient()
  const hasPermission = useAuthStore(s => s.hasPermission)
  const canApply = hasPermission('inventory.count.apply')

  const inputRef = useRef<HTMLInputElement>(null)
  const [deger, setDeger] = useState('')
  const [qty, setQty] = useState(1)
  const [notes, setNotes] = useState('')
  const [hata, setHata] = useState('')
  const [log, setLog] = useState<LogRow[]>([])
  const [busy, setBusy] = useState(false)

  // kip durumu
  const [bin, setBin] = useState<BinContents | null>(null)          // içerik / hedef göz (place) / kaynak göz (move, count)
  const [targetBin, setTargetBin] = useState<BinContents | null>(null)
  const [variant, setVariant] = useState<{ v: VariantInfo; bins: VariantBin[] } | null>(null)
  const [countId, setCountId] = useState<string | null>(null)
  const [count, setCount] = useState<CountDetail | null>(null)
  const [fromWh, setFromWh] = useState(''); const [toWh, setToWh] = useState('')

  const { data: authority } = useQuery<Authority>({ queryKey: ['stock-authority'], queryFn: async () => (await api.get('/inventory/stock-authority')).data.data, staleTime: 60_000 })
  const legacy = authority?.legacyOwnsStock ?? false
  const { data: warehouses = [] } = useQuery<Warehouse[]>({ queryKey: ['warehouses', true], queryFn: async () => (await api.get('/inventory/warehouses')).data.data, staleTime: 5 * 60_000 })

  useEffect(() => {
    const t = setInterval(() => { if (document.activeElement !== inputRef.current && !(document.activeElement instanceof HTMLTextAreaElement) && !(document.activeElement instanceof HTMLSelectElement)) inputRef.current?.focus() }, 900)
    inputRef.current?.focus()
    return () => clearInterval(t)
  }, [])

  function reset() { setBin(null); setTargetBin(null); setVariant(null); setCountId(null); setCount(null); setHata(''); setLog([]); setDeger('') }
  function ok(text: string) { setHata(''); setLog(l => [{ t: Date.now(), ok: true, text }, ...l].slice(0, 30)); basariSesi() }
  function fail(e: unknown) { const m = typeof e === 'string' ? e : errText(e); setHata(m); setLog(l => [{ t: Date.now(), ok: false, text: m }, ...l].slice(0, 30)); hataSesi() }

  async function resolve(barcode: string): Promise<Resolved> {
    return (await api.get('/inventory/shelf/resolve', { params: { barcode } })).data.data as Resolved
  }
  async function refreshBin(b: BinContents | null, setter: (x: BinContents) => void) {
    if (!b) return
    try { setter((await api.get(`/inventory/shelf/bins/${encodeURIComponent(b.binBarcode)}/contents`)).data.data) } catch { /* yoksay */ }
  }
  async function refreshCount(id: string) {
    setCount((await api.get(`/inventory/bin-counts/${id}`)).data.data)
  }

  const scan = useMutation({
    mutationFn: async (barcode: string) => {
      const r = await resolve(barcode)
      if (r.kind === 'none') throw new Error(`'${barcode}' ne göz ne ürün barkodu.`)
      switch (mode) {
        case 'contents':
          if (r.kind === 'bin') { setBin(r.bin); setVariant(null); ok(`Göz ${r.bin.binCode}: ${r.bin.items.length} ürün, ${r.bin.totalQuantity} adet`) }
          else { setVariant({ v: r.variant, bins: r.bins }); setBin(null); ok(`${r.variant.productName}: ${r.bins.length} gözde`) }
          return
        case 'place':
          if (r.kind === 'bin') { setBin(r.bin); ok(`Hedef göz: ${r.bin.binCode}`); return }
          if (!bin) throw new Error('Önce hedef gözü okutun.')
          if (legacy) throw new Error(authority?.message ?? 'Stok otoritesi eskide.')
          await api.post('/inventory/shelf/place', { binId: bin.binId, variantId: r.variant.variantId, quantity: qty, source: 'free', notes: notes || 'Raf ekranından serbest giriş' })
          ok(`+${qty} ${r.variant.productName} → ${bin.binCode}`)
          await refreshBin(bin, setBin)
          return
        case 'move':
          if (r.kind === 'bin') {
            if (!bin) { setBin(r.bin); ok(`Kaynak göz: ${r.bin.binCode} (${r.bin.totalQuantity} adet)`) }
            else if (r.bin.binId === bin.binId) throw new Error('Kaynakla aynı göz.')
            else { setTargetBin(r.bin); ok(`Hedef göz: ${r.bin.binCode}`) }
            return
          }
          if (!bin || !targetBin) throw new Error('Önce kaynak ve hedef gözü okutun.')
          if (legacy) throw new Error(authority?.message ?? 'Stok otoritesi eskide.')
          await api.post('/inventory/shelf/move', { fromBinId: bin.binId, toBinId: targetBin.binId, moveAll: false, items: [{ variantId: r.variant.variantId, quantity: qty }], notes: notes || null })
          ok(`${qty} × ${r.variant.productName}: ${bin.binCode} → ${targetBin.binCode}`)
          await refreshBin(bin, setBin); await refreshBin(targetBin, setTargetBin)
          return
        case 'return':
          if (r.kind === 'variant') {
            const kapali = r.bins.filter(b => !b.sectionSellableOnline && b.quantity - b.reservedQuantity > 0)
            if (kapali.length === 0) throw new Error(`${r.variant.productName} iade/defo kısmında (satışa kapalı göz) bulunmuyor.`)
            setVariant({ v: r.variant, bins: kapali }); ok(`${r.variant.productName}: kapalı kısımda ${kapali.length} göz — şimdi hedef gözü okutun`)
            return
          }
          if (!variant) throw new Error('Önce ürünü okutun.')
          if (legacy) throw new Error(authority?.message ?? 'Stok otoritesi eskide.')
          { const src = variant.bins[0]
            await api.post('/inventory/shelf/return-to-shelf', { fromBinId: src.binId, toBinId: r.bin.binId, variantId: variant.v.variantId, quantity: qty, notes: notes || null })
            ok(`${qty} × ${variant.v.productName}: ${src.binCode} (iade) → ${r.bin.binCode}`)
            const bins = (await api.get(`/inventory/shelf/variants/${variant.v.variantId}/bins`)).data.data as VariantBin[]
            setVariant({ v: variant.v, bins: bins.filter(b => !b.sectionSellableOnline && b.quantity - b.reservedQuantity > 0) })
          }
          return
        case 'count':
          if (r.kind === 'bin') {
            const id = (await api.post('/inventory/bin-counts/start', { binId: r.bin.binId })).data.data.countId as string
            setBin(r.bin); setCountId(id); await refreshCount(id); ok(`Sayım açıldı: ${r.bin.binCode}`)
            return
          }
          if (!countId) throw new Error('Önce sayılacak gözü okutun.')
          if (qty === 0) throw new Error('Adet 0 olamaz.')
          { const line = (await api.post(`/inventory/bin-counts/${countId}/scan`, { variantId: r.variant.variantId, delta: qty })).data.data as CountLine
            ok(`${r.variant.productName}: sayılan ${line.counted}`)
            await refreshCount(countId) }
          return
        case 'store':
          if (r.kind !== 'variant') throw new Error('Bu kipte ürün barkodu okutulur.')
          if (!fromWh || !toWh) throw new Error('Kaynak ve hedef depoyu seçin.')
          if (legacy) throw new Error(authority?.message ?? 'Stok otoritesi eskide.')
          await api.post('/inventory/shelf/store-move', { fromWarehouseId: fromWh, toWarehouseId: toWh, variantId: r.variant.variantId, quantity: qty, notes: notes || null })
          ok(`${qty} × ${r.variant.productName}: ${warehouses.find(w => w.id === fromWh)?.code} → ${warehouses.find(w => w.id === toWh)?.code}`)
          return
      }
    },
    onError: (e) => fail(e),
    onSettled: () => setBusy(false),
  })

  const okut = () => { const b = deger.trim(); setDeger(''); if (!b || scan.isPending) return; setBusy(true); scan.mutate(b) }

  const moveAll = useMutation({
    mutationFn: async () => { if (!bin || !targetBin) throw new Error('Kaynak ve hedef göz gerekli.'); await api.post('/inventory/shelf/move', { fromBinId: bin.binId, toBinId: targetBin.binId, moveAll: true, notes: notes || null }) },
    onSuccess: async () => { ok(`${bin!.binCode} → ${targetBin!.binCode}: tümü taşındı`); await refreshBin(bin, setBin); await refreshBin(targetBin, setTargetBin) },
    onError: (e) => fail(e),
  })
  const finishCount = useMutation({
    mutationFn: async () => { await api.post(`/inventory/bin-counts/${countId}/finish`, { notes: notes || null }) },
    onSuccess: async () => { ok('Sayım bitirildi — fark tablosu aşağıda'); await refreshCount(countId!); qc.invalidateQueries({ queryKey: ['bin-counts'] }) },
    onError: (e) => fail(e),
  })
  const applyCount = useMutation({
    mutationFn: async () => (await api.post(`/inventory/bin-counts/${countId}/apply`)).data.data.appliedLines as number,
    onSuccess: async (n) => { ok(`Fark uygulandı (${n} satır)`); await refreshCount(countId!); await refreshBin(bin, setBin); qc.invalidateQueries({ queryKey: ['bin-counts'] }) },
    onError: (e) => fail(e),
  })
  const cancelCount = useMutation({
    mutationFn: async () => { await api.post(`/inventory/bin-counts/${countId}/cancel`, { notes: 'Ekrandan iptal' }) },
    onSuccess: () => { ok('Sayım iptal edildi'); setCountId(null); setCount(null); qc.invalidateQueries({ queryKey: ['bin-counts'] }) },
    onError: (e) => fail(e),
  })

  const yazmaKipi = mode !== 'contents'
  const current = MODES.find(m => m.key === mode)!

  return (
    <div className="p-4 max-w-4xl mx-auto pb-16">
      {/* Kip sekmeleri */}
      <div className="tab-scroll flex gap-1 mb-3" style={{ borderBottom: '1px solid var(--border)' }}>
        {MODES.map(m => (
          <button key={m.key} className={cn('stab', mode === m.key && 'active')} onClick={() => setMode(m.key)}>{m.label}</button>
        ))}
        <Link to="/inventory/bin-counts" className="ml-auto text-xs underline self-center" style={{ color: 'var(--brand)' }}>Sayım raporları →</Link>
      </div>

      {legacy && yazmaKipi && (
        <div className="rounded-xl px-4 py-2 mb-3 text-sm" style={{ background: '#fef3c7', color: '#92400e', border: '1px solid #fcd34d' }}>
          <b>Aynalama kipi:</b> {authority?.message} Bu ekranda yalnız görüntüleme ve sayım raporu çalışır.
        </div>
      )}

      {/* Okutma kutusu + adet */}
      <div className="card p-4 mb-3">
        <div className="flex items-center justify-between mb-2 gap-3">
          <label className="flbl !text-base !mb-0">{current.label} — barkod okut</label>
          {mode !== 'contents' && (
            <div className="flex items-center gap-2 text-sm" style={{ color: 'var(--text-m)' }}>
              Adet
              <input type="number" min={mode === 'count' ? -99 : 1} value={qty} onChange={e => setQty(Math.max(mode === 'count' ? -99 : 1, parseInt(e.target.value) || 1))} className="inp !w-20 text-center" />
            </div>
          )}
        </div>
        <input ref={inputRef} value={deger} onChange={e => setDeger(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); okut() } }}
          onBlur={() => setTimeout(() => { if (!(document.activeElement instanceof HTMLTextAreaElement) && !(document.activeElement instanceof HTMLSelectElement) && !(document.activeElement instanceof HTMLInputElement)) inputRef.current?.focus() }, 150)}
          autoFocus autoComplete="off" autoCapitalize="off"
          placeholder={busy ? 'İşleniyor...' : 'Göz ya da ürün barkodu ●'}
          className="inp w-full font-mono text-center !text-2xl !py-4 !rounded-2xl" aria-label="Barkod okutma alanı" />
        <p className="text-xs mt-2" style={{ color: 'var(--text-s)' }}>{current.hint}</p>
        {mode === 'store' && (
          <div className="grid grid-cols-2 gap-2 mt-3">
            <select className="inp" value={fromWh} onChange={e => setFromWh(e.target.value)}>
              <option value="">Kaynak depo</option>{warehouses.map(w => <option key={w.id} value={w.id}>{wName(w)}</option>)}
            </select>
            <select className="inp" value={toWh} onChange={e => setToWh(e.target.value)}>
              <option value="">Hedef depo</option>{warehouses.map(w => <option key={w.id} value={w.id}>{wName(w)}</option>)}
            </select>
          </div>
        )}
        {(mode === 'place' || mode === 'move' || mode === 'return' || mode === 'store') && (
          <input className="inp mt-2" value={notes} onChange={e => setNotes(e.target.value)} placeholder={mode === 'place' ? 'Not (serbest girişte zorunlu — nereden geldi?)' : 'Not (isteğe bağlı)'} />
        )}
      </div>

      {hata && <div className="rounded-xl px-4 py-3 mb-3 text-sm font-medium text-white" style={{ background: '#dc2626' }}>{hata}</div>}

      {/* Bağlam kartları */}
      <div className="grid gap-3 md:grid-cols-2">
        {bin && (
          <div className="card p-4">
            <div className="flex items-center justify-between mb-2">
              <div>
                <div className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>{mode === 'place' ? 'Hedef göz' : mode === 'move' ? 'Kaynak göz' : 'Göz'}</div>
                <div className="text-lg font-bold font-mono" style={{ color: 'var(--text)' }}>{bin.binCode} <span className="text-xs font-normal" style={{ color: 'var(--text-s)' }}>{bin.warehouseCode} / {bin.sectionCode}</span></div>
              </div>
              <div className="text-right">
                <Badge variant={bin.sectionSellableOnline ? 'success' : 'neutral'}>{bin.sectionSellableOnline ? 'satışa açık' : 'kapalı kısım'}</Badge>
                <div className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>{bin.totalQuantity} adet · {bin.totalReserved} rezerve</div>
              </div>
            </div>
            {bin.items.length === 0 ? <p className="text-sm" style={{ color: 'var(--text-s)' }}>Göz boş.</p> : (
              <div className="divide-y" style={{ borderColor: 'var(--border)' }}>
                {bin.items.map(i => (
                  <div key={i.variantId} className="flex items-center justify-between py-1.5 gap-2">
                    <UrunSatir i={i} />
                    <div className="text-right shrink-0">
                      <div className="text-sm font-bold" style={{ color: 'var(--text)' }}>{i.quantity}</div>
                      {i.reservedQuantity > 0 && <div className="text-xs" style={{ color: '#d97706' }}>{i.reservedQuantity} rezerve</div>}
                    </div>
                  </div>
                ))}
              </div>
            )}
            {mode === 'move' && targetBin && bin.items.length > 0 && (
              <Button size="sm" className="mt-3" onClick={() => moveAll.mutate()} loading={moveAll.isPending} disabled={legacy}>Tümünü {targetBin.binCode} gözüne taşı</Button>
            )}
          </div>
        )}
        {targetBin && (
          <div className="card p-4">
            <div className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Hedef göz</div>
            <div className="text-lg font-bold font-mono" style={{ color: 'var(--text)' }}>{targetBin.binCode} <span className="text-xs font-normal" style={{ color: 'var(--text-s)' }}>{targetBin.sectionCode} · {targetBin.totalQuantity} adet</span></div>
          </div>
        )}
        {variant && (
          <div className="card p-4">
            <div className="text-xs font-semibold uppercase mb-2" style={{ color: 'var(--text-s)' }}>Ürün</div>
            <UrunSatir i={variant.v} />
            <div className="mt-3 text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>{mode === 'return' ? 'İade/defo kısmındaki gözler' : 'Bulunduğu gözler'}</div>
            {variant.bins.length === 0 ? <p className="text-sm" style={{ color: 'var(--text-s)' }}>Hiçbir gözde yok.</p> : (
              <table className="w-full text-sm mt-1">
                <tbody>
                  {variant.bins.map(b => (
                    <tr key={b.binId} style={{ borderTop: '1px solid var(--border)' }}>
                      <td className="py-1 font-mono">{b.warehouseCode} / {b.sectionCode} / <b>{b.binCode}</b></td>
                      <td className="py-1 text-right">{b.quantity}{b.reservedQuantity > 0 && <span className="text-xs" style={{ color: '#d97706' }}> ({b.reservedQuantity} rez.)</span>}</td>
                      <td className="py-1 text-right"><Badge variant={b.sectionSellableOnline ? 'success' : 'neutral'}>{b.sectionSellableOnline ? 'açık' : 'kapalı'}</Badge></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}
        {mode === 'count' && count && (
          <div className="card p-4 md:col-span-2">
            <div className="flex flex-wrap items-center gap-3 mb-2">
              <div className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Sayım</div>
              <Badge variant={count.header.status === 'open' ? 'warning' : count.header.status === 'applied' ? 'success' : 'info'}>{count.header.status}</Badge>
              <span className="text-sm" style={{ color: 'var(--text-m)' }}>sayılan <b>{count.header.countedTotal}</b>{count.header.status !== 'open' && <> · beklenen <b>{count.header.expectedTotal}</b> · fark <b>{count.header.diffTotal}</b></>}</span>
              <div className="ml-auto flex gap-2">
                {count.header.status === 'open' && <>
                  <Button size="sm" variant="secondary" onClick={() => cancelCount.mutate()} loading={cancelCount.isPending}>İptal</Button>
                  <Button size="sm" onClick={() => finishCount.mutate()} loading={finishCount.isPending}>Bitir</Button>
                </>}
                {count.header.status === 'finished' && (
                  canApply
                    ? <Button size="sm" onClick={() => applyCount.mutate()} loading={applyCount.isPending} disabled={legacy} title={legacy ? authority?.message ?? '' : ''}>Farkı Uygula</Button>
                    : <span className="text-xs" style={{ color: 'var(--text-s)' }}>Uygulama yetkisi depo sorumlusunda.</span>
                )}
              </div>
            </div>
            <table className="w-full text-sm">
              <thead><tr style={{ borderBottom: '1px solid var(--border)' }}>
                <th className="text-left py-1 text-xs" style={{ color: 'var(--text-s)' }}>ÜRÜN</th>
                {count.header.status !== 'open' && <th className="text-right py-1 text-xs" style={{ color: 'var(--text-s)' }}>BEKLENEN</th>}
                <th className="text-right py-1 text-xs" style={{ color: 'var(--text-s)' }}>SAYILAN</th>
                {count.header.status !== 'open' && <th className="text-right py-1 text-xs" style={{ color: 'var(--text-s)' }}>FARK</th>}
              </tr></thead>
              <tbody>
                {count.lines.map(l => (
                  <tr key={l.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="py-1.5"><UrunSatir i={l} /></td>
                    {count.header.status !== 'open' && <td className="py-1.5 text-right">{l.expected}</td>}
                    <td className="py-1.5 text-right font-bold">{l.counted}</td>
                    {count.header.status !== 'open' && <td className="py-1.5 text-right font-bold" style={{ color: l.diff === 0 ? '#16a34a' : '#dc2626' }}>{l.diff > 0 ? `+${l.diff}` : l.diff}</td>}
                  </tr>
                ))}
              </tbody>
            </table>
            {count.header.status === 'open' && <p className="text-xs mt-2" style={{ color: 'var(--text-s)' }}>Beklenen adetler sayım bitene kadar gizlidir. Yanlış okutmayı geri almak için adet kutusuna -1 yazıp tekrar okutun.</p>}
          </div>
        )}
      </div>

      {/* Oturum günlüğü */}
      {log.length > 0 && (
        <div className="card p-3 mt-3">
          <div className="text-xs font-semibold uppercase mb-1" style={{ color: 'var(--text-s)' }}>Bu oturum</div>
          {log.map(r => (
            <div key={r.t} className="text-xs py-0.5" style={{ color: r.ok ? 'var(--text-m)' : '#dc2626' }}>{r.ok ? '✓' : '✗'} {r.text}</div>
          ))}
        </div>
      )}
    </div>
  )
}
