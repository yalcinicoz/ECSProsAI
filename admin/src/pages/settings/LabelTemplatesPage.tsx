/**
 * T3 (K7) — Etiket Şablonları: sabit format YOK; kullanıcı kağıt ölçüsünü ve elemanları kendisi tasarlar.
 * Sol: şablon listesi. Sağ: düzenleyici — ölçü, eleman listesi, canlı önizleme (4px/mm, örnek veriyle),
 * önizlemede sürükleyerek konumlandırma, kağıt dışına taşma uyarısı, test yazdırma (/yazdir/etiket).
 */
import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Printer, Trash2 } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { PageSpinner } from '@/components/ui/Spinner'

interface El { type: 'barcode' | 'field' | 'text' | 'price'; field?: string; text?: string; x: number; y: number; w: number; h: number; fontPt: number; align: 'left' | 'center' | 'right'; bold: boolean }
interface Tpl { id: string; code: string; name: string; targetType: 'product' | 'bin'; widthMm: number; heightMm: number; elementsJson: string; isDefault: boolean; isActive: boolean }

const SCALE = 4 // px per mm (önizleme)
const SAMPLE: Record<string, string> = {
  name: 'Basic Pamuklu Tişört', sku: 'TSH-SYH-M', barcode: '8680000000017', color: 'Siyah', size: 'M',
  price: '349,90 ₺', code: 'P-00012345', section: 'Reyon A', warehouse: 'Merkez Depo',
}
const PRODUCT_FIELDS = [
  { v: 'name', l: 'Ürün adı' }, { v: 'color', l: 'Renk' }, { v: 'size', l: 'Beden' },
  { v: 'sku', l: 'SKU' }, { v: 'barcode', l: 'Barkod değeri' }, { v: 'code', l: 'Ürün kodu' },
]
const BIN_FIELDS = [
  { v: 'code', l: 'Birim kodu' }, { v: 'barcode', l: 'Barkod değeri' }, { v: 'section', l: 'Kısım' }, { v: 'warehouse', l: 'Depo' },
]
const newEl = (type: El['type']): El => ({
  type, field: type === 'field' ? 'name' : type === 'barcode' ? 'barcode' : undefined,
  text: type === 'text' ? 'Serbest metin' : undefined,
  x: 2, y: 2, w: type === 'barcode' ? 30 : 25, h: type === 'barcode' ? 12 : 6, fontPt: 8, align: 'left', bold: false,
})

export function LabelTemplatesPage() {
  const qc = useQueryClient()
  const { data: templates = [], isLoading } = useQuery<Tpl[]>({
    queryKey: ['label-templates'],
    queryFn: async () => (await api.get('/core/label-templates')).data.data,
  })

  const [selId, setSelId] = useState<string | 'new' | null>(null)
  const [form, setForm] = useState({ name: '', targetType: 'product' as 'product' | 'bin', widthMm: 40, heightMm: 30, isDefault: false, isActive: true })
  const [els, setEls] = useState<El[]>([])
  const [selEl, setSelEl] = useState<number | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const [testCode, setTestCode] = useState('')

  const load = (t: Tpl | null) => {
    setMsg(null); setSelEl(null)
    if (!t) { setSelId('new'); setForm({ name: '', targetType: 'product', widthMm: 40, heightMm: 30, isDefault: templates.length === 0, isActive: true }); setEls([newEl('barcode'), { ...newEl('field'), y: 16, field: 'name' }]) }
    else { setSelId(t.id); setForm({ name: t.name, targetType: t.targetType, widthMm: t.widthMm, heightMm: t.heightMm, isDefault: t.isDefault, isActive: t.isActive }); setEls(JSON.parse(t.elementsJson || '[]')) }
  }
  useEffect(() => { if (selId === null && templates.length > 0) load(templates[0]) }, [templates])  // eslint-disable-line

  const saveMut = useMutation({
    mutationFn: async () => (await api.post('/core/label-templates', {
      id: selId === 'new' ? null : selId, ...form, elementsJson: JSON.stringify(els),
    })).data.data,
    onSuccess: (d: { id: string }) => { setMsg('Kaydedildi.'); setSelId(d.id); qc.invalidateQueries({ queryKey: ['label-templates'] }) },
    onError: (e: any) => setMsg(e?.response?.data?.error ?? 'Kaydedilemedi.'),
  })
  const delMut = useMutation({
    mutationFn: async (id: string) => api.delete(`/core/label-templates/${id}`),
    onSuccess: () => { setSelId(null); qc.invalidateQueries({ queryKey: ['label-templates'] }) },
    onError: (e: any) => setMsg(e?.response?.data?.error ?? 'Silinemedi.'),
  })

  // Önizlemede sürükleme
  const dragRef = useRef<{ idx: number; startX: number; startY: number; elX: number; elY: number } | null>(null)
  const onDragMove = (ev: PointerEvent) => {
    const d = dragRef.current; if (!d) return
    setEls(prev => prev.map((e, i) => i === d.idx
      ? { ...e, x: Math.round((d.elX + (ev.clientX - d.startX) / SCALE) * 2) / 2, y: Math.round((d.elY + (ev.clientY - d.startY) / SCALE) * 2) / 2 }
      : e))
  }
  const onDragEnd = () => { dragRef.current = null; window.removeEventListener('pointermove', onDragMove); window.removeEventListener('pointerup', onDragEnd) }
  const startDrag = (idx: number, ev: React.PointerEvent) => {
    setSelEl(idx)
    dragRef.current = { idx, startX: ev.clientX, startY: ev.clientY, elX: els[idx].x, elY: els[idx].y }
    window.addEventListener('pointermove', onDragMove); window.addEventListener('pointerup', onDragEnd)
  }

  const overflow = useMemo(() => els.some(e => e.x < 0 || e.y < 0 || e.x + e.w > form.widthMm || e.y + e.h > form.heightMm), [els, form])
  const fields = form.targetType === 'product' ? PRODUCT_FIELDS : BIN_FIELDS
  const elValue = (e: El) => e.type === 'text' ? (e.text ?? '') : e.type === 'price' ? SAMPLE.price : (SAMPLE[e.field ?? ''] ?? '')

  const testPrint = async () => {
    setMsg(null)
    if (selId === 'new' || !selId) { setMsg('Önce kaydedin.'); return }
    if (form.targetType === 'bin') { setMsg('Birim etiketi testi: Depolar → kısım/birim ekranından basılacak (T5).'); return }
    try {
      const { data } = await api.get(`/catalog/products/${encodeURIComponent(testCode.trim())}`)
      const v = data?.data?.variants?.[0]
      if (!v?.id) { setMsg('Ürünün varyantı bulunamadı.'); return }
      window.open(`/yazdir/etiket?templateId=${selId}&variantId=${v.id}&count=3`, '_blank')
    } catch { setMsg(`"${testCode}" kodlu ürün bulunamadı.`) }
  }

  if (isLoading) return <PageSpinner />
  const sel = selEl != null ? els[selEl] : null

  return (
    <div className="p-6">
      <div className="mb-6">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Etiket Şablonları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Ürün ve birim/raf etiketlerini kendiniz tasarlayın: kağıt ölçüsü + elemanlar (barkod, alan, serbest metin, fiyat).
          Ayrıştırma ekranı varsayılan şablonla basar.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[260px_1fr] gap-4">
        {/* Şablon listesi */}
        <div className="card p-0 overflow-hidden self-start">
          <div className="flex items-center justify-between px-3 py-2.5" style={{ borderBottom: '1px solid var(--border)' }}>
            <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>ŞABLONLAR</span>
            <Button size="sm" variant="secondary" onClick={() => load(null)}><Plus size={13} /> Yeni</Button>
          </div>
          <ul>
            {templates.map(t => (
              <li key={t.id}>
                <button className="w-full text-left px-3 py-2 text-sm hover:opacity-80"
                  style={{ background: selId === t.id ? 'var(--surface2)' : 'transparent', color: 'var(--text)', borderBottom: '1px solid var(--border)' }}
                  onClick={() => load(t)}>
                  {t.name}
                  <span className="block text-xs" style={{ color: 'var(--text-s)' }}>
                    {t.targetType === 'product' ? 'Ürün' : 'Birim/Raf'} · {t.widthMm}×{t.heightMm} mm
                    {t.isDefault && ' · varsayılan'}{!t.isActive && ' · pasif'}
                  </span>
                </button>
              </li>
            ))}
            {templates.length === 0 && <li className="px-3 py-4 text-sm" style={{ color: 'var(--text-s)' }}>Henüz şablon yok.</li>}
          </ul>
        </div>

        {/* Düzenleyici */}
        {selId === null ? <div /> : (
          <div className="space-y-4">
            <div className="card flex flex-wrap items-end gap-3">
              <div className="min-w-[200px] flex-1">
                <label className="flbl mb-1">Şablon adı</label>
                <input className="inp" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="örn. Ürün etiketi 40×30" />
              </div>
              <div>
                <label className="flbl mb-1">Hedef</label>
                <select className="inp" value={form.targetType} onChange={e => setForm(f => ({ ...f, targetType: e.target.value as any }))}>
                  <option value="product">Ürün</option><option value="bin">Birim / Raf</option>
                </select>
              </div>
              <div className="w-24"><label className="flbl mb-1">En (mm)</label>
                <input type="number" min={10} max={500} className="inp" value={form.widthMm} onChange={e => setForm(f => ({ ...f, widthMm: +e.target.value || 0 }))} /></div>
              <div className="w-24"><label className="flbl mb-1">Boy (mm)</label>
                <input type="number" min={10} max={500} className="inp" value={form.heightMm} onChange={e => setForm(f => ({ ...f, heightMm: +e.target.value || 0 }))} /></div>
              <label className="flex items-center gap-1.5 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
                <input type="checkbox" className="w-4 h-4 accent-[var(--brand)]" checked={form.isDefault} onChange={e => setForm(f => ({ ...f, isDefault: e.target.checked }))} /> Varsayılan
              </label>
              <label className="flex items-center gap-1.5 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
                <input type="checkbox" className="w-4 h-4 accent-[var(--brand)]" checked={form.isActive} onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))} /> Aktif
              </label>
              <div className="flex-1" />
              <Button onClick={() => saveMut.mutate()} loading={saveMut.isPending} disabled={!form.name.trim()}>Kaydet</Button>
              {selId !== 'new' && (
                <Button variant="secondary" onClick={() => delMut.mutate(selId as string)} loading={delMut.isPending}><Trash2 size={14} /> Sil</Button>
              )}
            </div>
            {overflow && <p className="text-sm px-1" style={{ color: '#d97706' }}>⚠ Bir ya da daha çok eleman kağıt sınırının dışına taşıyor.</p>}
            {msg && <p className="text-sm px-1" style={{ color: 'var(--text-s)' }}>{msg}</p>}

            <div className="grid grid-cols-1 xl:grid-cols-[1fr_340px] gap-4 items-start">
              {/* Önizleme */}
              <div className="card overflow-auto">
                <p className="text-xs mb-2" style={{ color: 'var(--text-s)' }}>Önizleme (örnek veri) — elemanları sürükleyerek yerleştirin, tıklayıp sağdan düzenleyin.</p>
                <div className="relative mx-auto" style={{
                  width: form.widthMm * SCALE, height: form.heightMm * SCALE,
                  background: '#fff', border: '1px dashed #999', boxShadow: '0 1px 6px rgba(0,0,0,.15)',
                }}>
                  {els.map((e, i) => (
                    <div key={i} onPointerDown={ev => startDrag(i, ev)}
                      className="absolute cursor-move select-none overflow-hidden"
                      style={{
                        left: e.x * SCALE, top: e.y * SCALE, width: e.w * SCALE, height: e.h * SCALE,
                        outline: selEl === i ? '2px solid var(--brand)' : '1px dotted #bbb',
                        fontSize: e.fontPt * SCALE / 2.835, textAlign: e.align, fontWeight: e.bold ? 700 : 400,
                        color: '#111', lineHeight: 1.15, background: e.type === 'barcode' ? 'repeating-linear-gradient(90deg,#111 0 2px,#fff 2px 5px)' : 'transparent',
                      }}>
                      {e.type === 'barcode'
                        ? <span style={{ background: '#fff', fontSize: 10, position: 'absolute', bottom: 0, left: 0, right: 0, textAlign: 'center' }}>{elValue(e)}</span>
                        : elValue(e)}
                    </div>
                  ))}
                </div>
              </div>

              {/* Eleman paneli */}
              <div className="card space-y-3">
                <div className="flex flex-wrap gap-2">
                  {(['barcode', 'field', 'text', 'price'] as const).map(t => (
                    <Button key={t} size="sm" variant="secondary" onClick={() => { setEls(p => [...p, newEl(t)]); setSelEl(els.length) }}>
                      <Plus size={13} /> {t === 'barcode' ? 'Barkod' : t === 'field' ? 'Alan' : t === 'text' ? 'Metin' : 'Fiyat'}
                    </Button>
                  ))}
                </div>
                <ul className="text-sm divide-y" style={{ borderColor: 'var(--border)' }}>
                  {els.map((e, i) => (
                    <li key={i} className="flex items-center justify-between py-1.5 cursor-pointer" onClick={() => setSelEl(i)}
                      style={{ color: selEl === i ? 'var(--brand)' : 'var(--text)' }}>
                      <span>{e.type === 'barcode' ? 'Barkod' : e.type === 'price' ? 'Fiyat' : e.type === 'text' ? `Metin: ${e.text}` : `Alan: ${fields.find(f => f.v === e.field)?.l ?? e.field}`}</span>
                      <button className="p-1 hover:opacity-70" onClick={ev => { ev.stopPropagation(); setEls(p => p.filter((_, j) => j !== i)); setSelEl(null) }}>
                        <Trash2 size={13} style={{ color: 'var(--text-s)' }} />
                      </button>
                    </li>
                  ))}
                  {els.length === 0 && <li className="py-2 text-xs" style={{ color: 'var(--text-s)' }}>Eleman yok — yukarıdan ekleyin.</li>}
                </ul>
                {sel && (
                  <div className="space-y-2 pt-2" style={{ borderTop: '1px solid var(--border)' }}>
                    {sel.type === 'field' && (
                      <div><label className="flbl mb-1">Veri alanı</label>
                        <select className="inp" value={sel.field} onChange={e => setEls(p => p.map((x, i) => i === selEl ? { ...x, field: e.target.value } : x))}>
                          {fields.map(f => <option key={f.v} value={f.v}>{f.l}</option>)}
                        </select></div>
                    )}
                    {sel.type === 'text' && (
                      <div><label className="flbl mb-1">Metin</label>
                        <input className="inp" value={sel.text ?? ''} onChange={e => setEls(p => p.map((x, i) => i === selEl ? { ...x, text: e.target.value } : x))} /></div>
                    )}
                    <div className="grid grid-cols-4 gap-2">
                      {(['x', 'y', 'w', 'h'] as const).map(k => (
                        <div key={k}><label className="flbl mb-1">{k.toUpperCase()} mm</label>
                          <input type="number" step="0.5" className="inp" value={sel[k]}
                            onChange={e => setEls(p => p.map((x, i) => i === selEl ? { ...x, [k]: +e.target.value || 0 } : x))} /></div>
                      ))}
                    </div>
                    <div className="flex items-end gap-2">
                      <div className="w-24"><label className="flbl mb-1">Yazı (pt)</label>
                        <input type="number" step="0.5" className="inp" value={sel.fontPt}
                          onChange={e => setEls(p => p.map((x, i) => i === selEl ? { ...x, fontPt: +e.target.value || 6 } : x))} /></div>
                      <div><label className="flbl mb-1">Hiza</label>
                        <select className="inp" value={sel.align} onChange={e => setEls(p => p.map((x, i) => i === selEl ? { ...x, align: e.target.value as any } : x))}>
                          <option value="left">Sol</option><option value="center">Orta</option><option value="right">Sağ</option>
                        </select></div>
                      <label className="flex items-center gap-1.5 text-sm cursor-pointer pb-2" style={{ color: 'var(--text)' }}>
                        <input type="checkbox" className="w-4 h-4 accent-[var(--brand)]" checked={sel.bold}
                          onChange={e => setEls(p => p.map((x, i) => i === selEl ? { ...x, bold: e.target.checked } : x))} /> Kalın
                      </label>
                    </div>
                  </div>
                )}
                {/* Test yazdırma */}
                <div className="pt-2 space-y-2" style={{ borderTop: '1px solid var(--border)' }}>
                  <label className="flbl">Test yazdır (ürün kodu)</label>
                  <div className="flex gap-2">
                    <input className="inp flex-1" value={testCode} onChange={e => setTestCode(e.target.value)} placeholder="örn. P-00012345"
                      onKeyDown={e => e.key === 'Enter' && testPrint()} />
                    <Button size="sm" variant="secondary" onClick={testPrint} disabled={!testCode.trim() && form.targetType === 'product'}>
                      <Printer size={14} /> Yazdır
                    </Button>
                  </div>
                  <p className="text-xs" style={{ color: 'var(--text-s)' }}>İlk varyantla 3 kopya basılır; kaydedilmemiş değişiklikler basıma yansımaz.</p>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
