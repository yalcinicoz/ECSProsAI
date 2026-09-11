import { useEffect, useMemo, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Plus, Package } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { Badge } from '@/components/ui/Badge'
import { errText } from '@/components/ui/DataTable.utils'
import { DataGrid, FilterAccordion, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'

// Ürünler — DataGrid göçü (docs/datagrid-standardi-plani.md F4). Tümü/Satışta anahtarı `?tab=` ile URL'de (activeOnly named parametresi
// korunur); filtre/arama/sıralama/sayfa URL'de, kolon tercihleri localStorage'da. Sunucu beyaz listesi: ProductGrid.Schema.

// ── Types ─────────────────────────────────────────────────────────────────────

interface ProductListItem {
  id: string
  code: string
  nameI18n: Record<string, string>
  productGroupId: string
  isActive: boolean
  variantCount: number
  // 2026-09-11 kapsamlı filtre kolonları (mv_product_stats, 5 dk tazelik)
  imageState?: 'none' | 'partial' | 'full' | string
  imageCount?: number
  hasVideo?: boolean
  stockQuantity?: number
  stockAvailable?: number
}

interface PagedResult {
  items: ProductListItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
}

function getName(item: { nameI18n: Record<string, string>; code: string }): string {
  return item.nameI18n['tr'] ?? item.nameI18n[Object.keys(item.nameI18n)[0]] ?? item.code
}

const SOURCE_TYPE_OPTIONS = [
  { value: 'own', label: 'Kendi' },
  { value: 'seller', label: 'Satıcı' },
  { value: 'supply', label: 'Dış tedarik' },
]
// Resim durumu RENK (varyant) bazlı: Yok = hiçbir rengin görseli yok, Var = tüm renklerde var, Kısmi = bazı renklerde var
const IMAGE_STATE_OPTIONS = [
  { value: 'none', label: 'Yok' },
  { value: 'full', label: 'Var' },
  { value: 'partial', label: 'Kısmi' },
]
// Video durumu: modelde en az 1 aktif video → Var
const VIDEO_STATE_OPTIONS = [
  { value: 'var', label: 'Var' },
  { value: 'yok', label: 'Yok' },
]
const IMAGE_STATE_BADGE: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  full: { label: 'Var', variant: 'success' }, partial: { label: 'Kısmi', variant: 'warning' }, none: { label: 'Yok', variant: 'danger' },
}

// ── Özellik filtresi (eski /urun/urun-yonetim "Özellikler / Değerler / Seçilen Özellikler", 2026-09-11) ─────────────
// Grid filtresi 'attrs' = "tipId:degerId,degerId;tipId2:degerId" — aynı özellikte VEYA, özellikler arasında VE (sunucu ProductGrid).
interface AttrValue { id: string; nameI18n: Record<string, string>; isActive: boolean; sortOrder: number }
interface AttrType { id: string; code: string; nameI18n: Record<string, string>; isActive: boolean; values: AttrValue[] }
const attrsParse = (v: string): Record<string, string[]> => {
  const out: Record<string, string[]> = {}
  for (const g of (v || '').split(';')) { const [t, vals] = g.split(':'); if (t && vals) out[t] = vals.split(',').filter(Boolean) }
  return out
}
const attrsStringify = (m: Record<string, string[]>) => Object.entries(m).filter(([, vs]) => vs.length).map(([t, vs]) => `${t}:${vs.join(',')}`).join(';')

// Aranabilir çoklu seçim (Değerler): arama kutusu + onay kutulu liste; dışarı tıklama/Esc kapatır (2026-09-11 kullanıcı isteği)
function MultiSearchSelect({ options, value, onChange, disabled, placeholder }: {
  options: { value: string; label: string }[]; value: string[]; onChange: (v: string[]) => void; disabled?: boolean; placeholder?: string
}) {
  const [open, setOpen] = useState(false)
  const [q, setQ] = useState('')
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey) }
  }, [open])
  const sel = new Set(value)
  const gorunen = options.filter(o => !q || o.label.toLocaleLowerCase('tr').includes(q.toLocaleLowerCase('tr')))
  const toggle = (v: string) => { const n = new Set(sel); if (n.has(v)) n.delete(v); else n.add(v); onChange(Array.from(n)) }
  const label = sel.size === 0 ? (placeholder ?? 'Seçin') : sel.size === 1 ? (options.find(o => sel.has(o.value))?.label ?? '1 seçili') : `${sel.size} değer seçili`
  return (
    <div className="relative" ref={ref}>
      <button type="button" disabled={disabled} onClick={() => setOpen(o => !o)} aria-haspopup="listbox" aria-expanded={open}
        className="inp text-sm !py-1.5 !px-2 !h-auto !inline-flex items-center justify-between w-full disabled:opacity-50"
        style={{ color: sel.size ? 'var(--text)' : 'var(--text-s)' }}>
        <span className="truncate">{label}</span><span className="text-xs">▾</span>
      </button>
      {open && (
        <div role="listbox" aria-multiselectable className="absolute left-0 right-0 mt-1 rounded-xl shadow-lg z-40" style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          <div className="p-2" style={{ borderBottom: '1px solid var(--border)' }}>
            <input autoFocus className="inp text-sm !py-1.5 w-full" placeholder="Değer ara…" value={q} onChange={e => setQ(e.target.value)} />
            <div className="flex gap-3 mt-1 text-xs">
              <button type="button" className="underline" style={{ color: 'var(--brand)' }} onClick={() => onChange(Array.from(new Set([...value, ...gorunen.map(o => o.value)])))}>Görünenleri seç</button>
              <button type="button" className="underline" style={{ color: 'var(--text-s)' }} onClick={() => onChange([])}>Tümünü kaldır</button>
            </div>
          </div>
          <div className="max-h-56 overflow-y-auto thin-scroll p-1">
            {gorunen.map(o => (
              <label key={o.value} className="flex items-center gap-2 px-2 py-1.5 rounded-lg text-sm cursor-pointer hover:bg-[var(--surface2)]" style={{ color: 'var(--text)' }}>
                <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={sel.has(o.value)} onChange={() => toggle(o.value)} />
                {o.label}
              </label>
            ))}
            {gorunen.length === 0 && <div className="px-2 py-2 text-xs" style={{ color: 'var(--text-s)' }}>Eşleşen değer yok.</div>}
          </div>
        </div>
      )}
    </div>
  )
}

function AttributeFilter({ types, value, onChange }: { types: AttrType[]; value: string; onChange: (v: string) => void }) {
  const [typeId, setTypeId] = useState<string | null>(null)
  const [picked, setPicked] = useState<string[]>([])
  const [q, setQ] = useState('')
  const secim = useMemo(() => attrsParse(value), [value])
  const tip = types.find(t => t.id === typeId)
  const degerler = (tip?.values ?? []).filter(v => v.isActive).sort((a, b) => a.sortOrder - b.sortOrder)
  const ekle = () => {
    if (!typeId || picked.length === 0) return
    const n = { ...secim, [typeId]: Array.from(new Set([...(secim[typeId] ?? []), ...picked])) }
    onChange(attrsStringify(n)); setPicked([])
  }
  const sil = (t: string, v: string) => { const n = { ...secim, [t]: (secim[t] ?? []).filter(x => x !== v) }; onChange(attrsStringify(n)) }
  const satirlar = Object.entries(secim).flatMap(([t, vs]) => vs.map(v => {
    const tt = types.find(x => x.id === t); const vv = tt?.values.find(x => x.id === v)
    return { t, v, tipAd: tt ? getName(tt) : t, degerAd: vv ? getName({ nameI18n: vv.nameI18n, code: v }) : v }
  })).filter(r => !q || `${r.tipAd} ${r.degerAd}`.toLocaleLowerCase('tr').includes(q.toLocaleLowerCase('tr')))
  return (
    <div className="grid gap-3" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))' }}>
      <div className="space-y-2">
        <div><span className="flbl">Özellikler</span>
          <SearchableSelect value={typeId} onChange={v => { setTypeId(v); setPicked([]) }} placeholder="Özellik seçin" clearable portal
            options={types.filter(t => t.isActive && t.values.length > 0).map(t => ({ value: t.id, label: getName(t) }))} /></div>
        <div>
          <span className="flbl">Değerler</span>
          <div className="flex items-start gap-2">
            <div className="flex-1 min-w-0">
              <MultiSearchSelect disabled={!tip} value={picked} onChange={setPicked} placeholder={tip ? 'Değer seçin' : 'Önce özellik seçin'}
                options={degerler.map(v => ({ value: v.id, label: getName({ nameI18n: v.nameI18n, code: v.id }) }))} />
            </div>
            <Button size="sm" variant="secondary" disabled={!typeId || picked.length === 0} onClick={ekle}>Ekle</Button>
          </div>
        </div>
      </div>
      <div>
        <div className="flex items-center justify-between mb-1"><span className="flbl">Seçilen Özellikler</span>
          <input className="inp text-xs py-1 w-32" placeholder="Ara…" value={q} onChange={e => setQ(e.target.value)} /></div>
        <div className="rounded-lg overflow-y-auto thin-scroll" style={{ border: '1px solid var(--border)', maxHeight: 200 }}>
          <table className="w-full text-sm">
            <thead className="sticky top-0"><tr style={{ background: 'var(--surface2)' }}>
              <th className="px-2 py-1.5 text-left text-xs font-semibold" style={{ color: 'var(--text-s)' }}>FİLTRE</th>
              <th className="px-2 py-1.5 text-left text-xs font-semibold" style={{ color: 'var(--text-s)' }}>SEÇİLEN DEĞER</th>
              <th className="px-2 py-1.5 w-10"></th>
            </tr></thead>
            <tbody>
              {satirlar.map(r => (
                <tr key={r.t + r.v} style={{ borderTop: '1px solid var(--border)' }}>
                  <td className="px-2 py-1" style={{ color: 'var(--text-m)' }}>{r.tipAd}</td>
                  <td className="px-2 py-1" style={{ color: 'var(--text)' }}>{r.degerAd}</td>
                  <td className="px-2 py-1 text-right"><button type="button" aria-label="Sil" className="text-xs" style={{ color: '#b91c1c' }} onClick={() => sil(r.t, r.v)}>✕</button></td>
                </tr>
              ))}
              {satirlar.length === 0 && <tr><td colSpan={3} className="px-2 py-3 text-center text-xs" style={{ color: 'var(--text-s)' }}>Seçilen özellik yok. Aynı özellikteki değerler VEYA, farklı özellikler VE ile birleşir.</td></tr>}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

// ── Component ─────────────────────────────────────────────────────────────────

export function ProductsPage() {
  const navigate = useNavigate()
  const grid = useGridState('products', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const activeOnly = sp.get('tab') === 'active'

  const { data: groups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups', false],
    queryFn: async () => (await api.get('/catalog/product-groups?activeOnly=false')).data.data,
    staleTime: 5 * 60 * 1000,
  })

  const groupMap = useMemo(() => {
    const m = new Map<string, string>()
    groups.forEach((g) => m.set(g.id, getName(g)))
    return m
  }, [groups])

  const groupOptions = useMemo(() => groups.map(g => ({ value: g.id, label: getName(g) })).sort((a, b) => a.label.localeCompare(b.label, 'tr')), [groups])

  const named = { activeOnly: String(activeOnly) }   // sıralama grid'den (varsayılan createdAt desc = eski 'newest'); named sort artık gönderilmez (2026-09-08: 'Geçersiz sıralama alanı: newest' düzeltmesi)

  const { data: result, isLoading, isFetching, error } = useQuery<PagedResult>({
    queryKey: ['products', activeOnly, ...grid.queryKey],
    queryFn: async () => (await api.get(`/catalog/products?${grid.toParams(named)}`)).data.data,
    placeholderData: (prev) => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const items = result?.items ?? []
  const totalCount = result?.totalCount ?? 0

  const extraFilters: GridFilterField[] = useMemo(() => [
    { key: 'productGroupId', label: 'Ürün grubu', type: 'enum', options: groupOptions, quick: true },
  ], [groupOptions])

  // Kapsamlı (gelişmiş) filtre — eski panel /urun/urun-yonetim'in filtre setinin karşılığı (2026-09-11):
  // kod/ad/tedarikçi kodu/barkod, grup/kaynak, satış durumu, görsel durumu (var/kısmi/yok), stok min-maks (fiziksel + satılabilir),
  // fiyat, varyant sayısı, görsel sayısı, oluşturma ve son görsel tarihi. Görsel/stok değerleri 5 dk'da bir yenilenir.
  const advancedFields: GridFilterField[] = useMemo(() => [
    { key: 'code', label: 'Ürün kodu', type: 'text' },
    { key: 'name', label: 'Ürün adı', type: 'text' },
    { key: 'supplierProductCode', label: 'Tedarikçi ürün kodu', type: 'text' },
    { key: 'barcode', label: 'Varyant barkodu', type: 'text', ops: ['eq', 'contains'] },
    { key: 'productGroupId', label: 'Ürün Grubu', type: 'enum', options: groupOptions, searchable: true },
    { key: 'sourceType', label: 'Kaynak', type: 'enum', multiple: true, options: SOURCE_TYPE_OPTIONS },
    { key: 'isSaleOpen', label: 'Satışta', type: 'boolean' },
    { key: 'imageState', label: 'Resim Durumu', type: 'enum', options: IMAGE_STATE_OPTIONS },
    { key: 'videoState', label: 'Video Durumu', type: 'enum', options: VIDEO_STATE_OPTIONS },
    { key: 'stock', label: 'Stok adedi (fiziksel)', type: 'number' },
    { key: 'stockAvailable', label: 'Satılabilir stok', type: 'number' },
    { key: 'basePrice', label: 'Liste fiyatı', type: 'number' },
    { key: 'variantCount', label: 'Varyant sayısı', type: 'number' },
    { key: 'createdAt', label: 'Oluşturma tarihi', type: 'date' },
    { key: 'lastImageAt', label: 'Son görsel tarihi', type: 'date' },
  ], [groupOptions])


  // Özellik filtresi: tipler + değerleri (tam liste ucu; FilterBuilder da aynı ucu kullanır)
  const { data: attrTypes = [] } = useQuery<AttrType[]>({
    queryKey: ['attribute-types', 'all-with-values'],
    queryFn: async () => (await api.get('/catalog/attribute-types?activeOnly=true&includeCounts=false')).data.data,
    staleTime: 5 * 60 * 1000,
  })
  const attrsValue = grid.state.filters.find(f => f.field === 'attrs')?.value ?? ''
  const attrsChip = (v: string) => {
    const m = attrsParse(v)
    return 'Özellikler: ' + Object.entries(m).map(([t, vs]) => {
      const tt = attrTypes.find(x => x.id === t)
      const names = vs.map(id => { const vv = tt?.values.find(x => x.id === id); return vv ? getName({ nameI18n: vv.nameI18n, code: id }) : id.slice(0, 6) })
      return `${tt ? getName(tt) : t.slice(0, 6)} = ${names.join(' / ')}`
    }).join(' · ')
  }


  // Çip/mobil liste için özellik filtresi alanı (akordeonda özel bileşenle çizilir; FieldRow listesine GİRMEZ)
  const attrsField: GridFilterField = useMemo(() => ({ key: 'attrs', label: 'Özellikler', type: 'text', chipText: attrsChip }), [attrTypes]) // eslint-disable-line react-hooks/exhaustive-deps

  // Görsel/stok istatistiklerinin tazeliği + "Şimdi yenile"
  const queryClient = useQueryClient()
  const { data: statsInfo } = useQuery<{ refreshedAt: string | null; intervalMinutes: number }>({
    queryKey: ['product-stats-info'],
    queryFn: async () => (await api.get('/catalog/products/stats')).data.data,
    staleTime: 60_000,
  })
  const refreshStats = useMutation({
    mutationFn: async () => (await api.post('/catalog/products/stats/refresh', {})).data.data as { refreshed: boolean },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['product-stats-info'] }); queryClient.invalidateQueries({ queryKey: ['products'] }) },
  })
  const statsNote = (
    <span>
      Görsel/stok bilgisi her {statsInfo?.intervalMinutes ?? 5} dk'da bir yenilenir
      {statsInfo?.refreshedAt ? ` · son: ${new Date(statsInfo.refreshedAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}` : ''}.
      {' '}<button type="button" className="underline" style={{ color: 'var(--brand)' }} disabled={refreshStats.isPending}
        onClick={() => refreshStats.mutate()}>{refreshStats.isPending ? 'Yenileniyor…' : 'Şimdi yenile'}</button>
    </span>
  )

  const columns: GridColumn<ProductListItem>[] = [
    { key: 'code', header: 'ÜRÜN', filters: [{ field: 'name', label: 'Ürün adı', type: 'text' }, { field: 'supplierProductCode', label: 'Tedarikçi ürün kodu', type: 'text' }, { field: 'basePrice', label: 'Liste fiyatı', type: 'number' }, { field: 'taxRate', label: 'KDV %', type: 'number' }, { field: 'createdAt', label: 'Oluşturma', type: 'date' }], frozen: true, lockVisible: true, sortable: true, minWidth: 220, filter: { type: 'text', label: 'Ürün kodu' },
      cell: (item) => (
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl flex-shrink-0 flex items-center justify-center" style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}>
            <Package size={14} />
          </div>
          <div>
            <div className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{getName(item)}</div>
            <code className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{item.code}</code>
          </div>
        </div>
      ) },
    { key: 'group', header: 'GRUP', sortable: true, filter: { type: 'enum', label: 'Grup', field: 'productGroupId', options: groupOptions }, filters: [{ field: 'sourceType', label: 'Kaynak', type: 'enum', multiple: true, options: SOURCE_TYPE_OPTIONS }], priority: 2, cell: (item) => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{groupMap.get(item.productGroupId) ?? '—'}</span> },
    { key: 'variantCount', header: 'VARYANT', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Varyant sayısı' }, cell: (item) => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{item.variantCount}</span> },
    { key: 'imageState', header: 'RESİM', priority: 2, align: 'center', sortable: true, filter: { type: 'enum', label: 'Resim Durumu', options: IMAGE_STATE_OPTIONS }, filters: [{ field: 'videoState', label: 'Video Durumu', type: 'enum', options: VIDEO_STATE_OPTIONS }, { field: 'lastImageAt', label: 'Son görsel tarihi', type: 'date' }],
      cell: (item) => { const b = IMAGE_STATE_BADGE[item.imageState ?? 'none'] ?? IMAGE_STATE_BADGE.none; return <span className="inline-flex items-center gap-1"><Badge variant={b.variant}>{b.label}</Badge>{item.hasVideo && <span className="text-xs" title="Video var" style={{ color: 'var(--text-s)' }}>▶</span>}</span> } },
    { key: 'stock', header: 'STOK', priority: 2, align: 'right', sortable: true, filter: { type: 'number', label: 'Stok adedi (fiziksel)' }, filters: [{ field: 'stockAvailable', label: 'Satılabilir stok', type: 'number' }],
      cell: (item) => <span className="text-sm tabular-nums" style={{ color: (item.stockQuantity ?? 0) > 0 ? 'var(--text)' : 'var(--text-s)' }} title={`Satılabilir: ${item.stockAvailable ?? 0}`}>{item.stockQuantity ?? 0}</span> },
    { key: 'isSaleOpen', header: 'DURUM', priority: 1, align: 'center', sortable: true, lockVisible: true, filter: { type: 'boolean', label: 'Satışta' },
      cell: (item) => <Badge variant={item.isActive ? 'success' : 'neutral'}>{item.isActive ? 'Satışta' : 'Satış Kapalı'}</Badge> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      {/* ── Page header ── */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürünler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {result ? `${totalCount.toLocaleString('tr-TR')} ürün${grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}` : '…'}
          </p>
        </div>

        <div className="flex items-center gap-3">
          {/* Tümü / Satışta anahtarı — URL ?tab=active */}
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            <button
              onClick={() => grid.mutate(n => n.delete('tab'))}
              className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all', !activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
              style={!activeOnly ? { color: 'var(--text)' } : {}}
            >Tümü</button>
            <button
              onClick={() => grid.mutate(n => n.set('tab', 'active'))}
              className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all', activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
              style={activeOnly ? { color: 'var(--text)' } : {}}
            >Satışta</button>
          </div>

          <Button onClick={() => navigate('/catalog/products/new')}>
            <Plus size={14} /> Yeni Ürün
          </Button>
        </div>
      </div>

      {/* 2026-09-11 (kullanıcı): filtre, eski panel /urun/urun-yonetim gibi listenin ÜSTÜNDE tam satır akordeon — başlangıçta kapalı */}
      <FilterAccordion grid={grid} fields={advancedFields} storageKey="grid:products:adv" title="Filtrele" note={statsNote}
        extra={<AttributeFilter types={attrTypes} value={attrsValue} onChange={v => grid.setFilter({ field: 'attrs', op: 'eq', value: v })} />} />

      <DataGrid<ProductListItem>
        gridId="products"
        views
        grid={grid}
        columns={columns}
        extraFilters={extraFilters}
        advancedFilters={{ fields: [...advancedFields, attrsField], layout: 'external' }}
        search={{ placeholder: 'Ürün adı, kod, tedarikçi ürün kodu…' }}
        rows={items}
        totalCount={totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={(item) => navigate(`/catalog/products/${item.code}`)}
        empty={grid.state.search ? `"${grid.state.search}" için ürün bulunamadı` : 'Henüz ürün eklenmemiş'}
        minWidth={860}
        export={{ endpoint: '/catalog/products/export', named: () => ({ activeOnly: String(activeOnly) }), fallbackFileName: 'urunler.xlsx' }}
        compact={{
          title: item => item.code,
          subtitle: item => getName(item),
          right: item => `${item.variantCount} varyant`,
          badge: item => <Badge variant={item.isActive ? 'success' : 'neutral'}>{item.isActive ? 'Satışta' : 'Kapalı'}</Badge>,
        }}
      />
    </div>
  )
}
