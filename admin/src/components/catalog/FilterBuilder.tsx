import { useState, useEffect, useMemo, useCallback, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Plus, X, Tag, ChevronDown, Search } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'

// ── Types ──────────────────────────────────────────────────────────────────────

export interface FilterDef {
  productGroupIds?: string[]
  excludedProductGroupIds?: string[]
  priceMin?: number | null
  priceMax?: number | null
  platformPriceMin?: number | null
  platformPriceMax?: number | null
  discountMinPercent?: number | null
  taxRateMin?: number | null
  taxRateMax?: number | null
  supplierIds?: string[]
  isActive?: boolean | null
  stockMin?: number | null
  stockMax?: number | null
  createdAfterDays?: number | null
  createdAfter?: string | null
  createdBefore?: string | null
  imageUpdatedAfterDays?: number | null
  imageUpdatedAfter?: string | null
  imageUpdatedBefore?: string | null
  tags?: string[]
  attributeFilters?: AttributeFilterItem[]
  // F1 kanal kapsamı kriterleri (yalnız channelScope modunda gösterilir)
  hasChannelPrice?: boolean | null
  minStock?: number | null
  // F5 K6: ürün kaynağı (own | seller | supply) — boş = kanalın izin verdiği tüm kaynaklar
  sourceTypes?: string[]
}

interface AttributeFilterItem { attributeTypeId: string; valueIds: string[] }
interface ProductGroup { id: string; code: string; nameI18n: Record<string, string> }
interface AttributeValue {
  id: string; nameI18n: Record<string, string>
  filterColors?: { code: string; nameI18n: Record<string, string>; hexCode?: string }[]
}
interface AttributeType { id: string; code: string; nameI18n: Record<string, string>; values: AttributeValue[] }
interface Supplier { id: string; title: string; code: string; accountType?: string }

// ── Helpers ────────────────────────────────────────────────────────────────────

function tr(i18n: Record<string, string> | undefined, fallback = ''): string {
  if (!i18n) return fallback
  return i18n['tr'] ?? i18n['en'] ?? i18n[Object.keys(i18n)[0]] ?? fallback
}

function matchesSearch(query: string, ...values: (string | undefined)[]): boolean {
  const normalized = query.trim().toLocaleLowerCase('tr-TR')
  if (!normalized) return true
  return values.some(value => value?.toLocaleLowerCase('tr-TR').includes(normalized))
}

function useCloseOnOutside(open: boolean, onClose: () => void) {
  const rootRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)

  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  useEffect(() => {
    if (!open) return

    function handlePointerDown(event: PointerEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node))
        onCloseRef.current()
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [open])

  return rootRef
}

function buildDescription(
  def: FilterDef,
  refs: { groups: ProductGroup[]; attrTypes: AttributeType[]; suppliers: Supplier[] },
): string {
  const parts: string[] = []

  if (def.productGroupIds?.length)
    parts.push(def.productGroupIds.map(id => tr(refs.groups.find(g => g.id === id)?.nameI18n)).filter(Boolean).join(', ') + ' grubundan')
  if (def.excludedProductGroupIds?.length)
    parts.push(def.excludedProductGroupIds.map(id => tr(refs.groups.find(g => g.id === id)?.nameI18n)).filter(Boolean).join(', ') + ' grubu HARİÇ')

  if (def.supplierIds?.length)
    parts.push('Tedarikçi: ' + def.supplierIds.map(id => refs.suppliers.find(s => s.id === id)?.title ?? '?').join(', '))

  if (def.priceMin != null && def.priceMax != null) parts.push(`Fiyat: ${def.priceMin}₺–${def.priceMax}₺`)
  else if (def.priceMin != null) parts.push(`Min fiyat: ${def.priceMin}₺`)
  else if (def.priceMax != null) parts.push(`Maks fiyat: ${def.priceMax}₺`)

  if (def.platformPriceMin != null || def.platformPriceMax != null)
    parts.push(`Platform fiyatı: ${def.platformPriceMin ?? '?'}₺–${def.platformPriceMax ?? '?'}₺`)

  if (def.discountMinPercent != null) parts.push(`Min %${def.discountMinPercent} indirim`)

  if (def.taxRateMin != null && def.taxRateMax != null) parts.push(`KDV: %${def.taxRateMin}–%${def.taxRateMax}`)
  else if (def.taxRateMin != null) parts.push(`Min KDV: %${def.taxRateMin}`)
  else if (def.taxRateMax != null) parts.push(`Maks KDV: %${def.taxRateMax}`)

  if (def.isActive === true) parts.push('Sadece aktif')
  if (def.isActive === false) parts.push('Sadece pasif')

  if (def.sourceTypes?.length) parts.push('Kaynak: ' + def.sourceTypes.map(s => s === 'own' ? 'Bizim' : s === 'seller' ? 'Satıcı' : 'Dış kaynak').join(', '))
  if (def.hasChannelPrice) parts.push('Bu kanalda fiyatı olanlar')
  if (def.minStock != null && def.minStock > 0) parts.push(`Kanal stok eşiği ≥ ${def.minStock}`)
  if (def.stockMin != null && def.stockMax != null) parts.push(`Stok: ${def.stockMin}–${def.stockMax} adet`)
  else if (def.stockMin != null) parts.push(`Min stok: ${def.stockMin} adet`)
  else if (def.stockMax != null) parts.push(`Maks stok: ${def.stockMax} adet`)

  if (def.createdAfterDays != null) parts.push(`Son ${def.createdAfterDays} günde eklenen`)
  else {
    if (def.createdAfter) parts.push(`Eklendi: ${def.createdAfter.slice(0, 10)} sonrası`)
    if (def.createdBefore) parts.push(`Eklendi: ${def.createdBefore.slice(0, 10)} öncesi`)
  }

  if (def.imageUpdatedAfterDays != null) parts.push(`Son ${def.imageUpdatedAfterDays} günde resmi güncellenen`)
  else {
    if (def.imageUpdatedAfter) parts.push(`Resim güncelleme: ${def.imageUpdatedAfter.slice(0, 10)} sonrası`)
    if (def.imageUpdatedBefore) parts.push(`Resim güncelleme: ${def.imageUpdatedBefore.slice(0, 10)} öncesi`)
  }

  if (def.tags?.length) parts.push(`Etiket: ${def.tags.join(', ')}`)

  if (def.attributeFilters?.length) {
    for (const af of def.attributeFilters) {
      const atype = refs.attrTypes.find(a => a.id === af.attributeTypeId)
      if (!atype) continue
      const valNames = af.valueIds.map(vid => tr(atype.values.find(v => v.id === vid)?.nameI18n)).filter(Boolean)
      if (valNames.length) parts.push(`${tr(atype.nameI18n)}: ${valNames.join(', ')}`)
    }
  }

  return parts.length ? parts.join(' · ') : 'Tüm ürünler'
}

// ── Props ─────────────────────────────────────────────────────────────────────

export interface FilterBuilderProps {
  value: FilterDef
  onChange: (def: FilterDef, description: string) => void
  /** F1: kanal kapsamı bağlamı — "Bu kanalda fiyatı var" ve "Kanal stok eşiği" bölümlerini gösterir. */
  channelScope?: boolean
}

// ── Component ──────────────────────────────────────────────────────────────────

export function FilterBuilder({ value, onChange, channelScope = false }: FilterBuilderProps) {
  const { data: productGroups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups-simple'],
    queryFn: async () => { const { data } = await api.get('/catalog/product-groups'); return data.data },
    staleTime: 60_000,
  })
  const { data: attrTypes = [] } = useQuery<AttributeType[]>({
    queryKey: ['attr-types-with-values', 'no-counts'],
    queryFn: async () => { const { data } = await api.get('/catalog/attribute-types?includeCounts=false'); return data.data },
    staleTime: 60_000,
  })
  const { data: suppliers = [] } = useQuery<Supplier[]>({
    queryKey: ['accounts-suppliers'],
    queryFn: async () => {
      const { data } = await api.get('/accounts?pageSize=200')
      const all: Supplier[] = data.data?.items ?? data.data ?? []
      return all.filter(a => !a.accountType || a.accountType === 'supplier' || a.accountType === 'both')
    },
    staleTime: 60_000,
  })
  const { data: allTags = [] } = useQuery<string[]>({
    queryKey: ['product-tags'],
    queryFn: async () => { const { data } = await api.get('/catalog/tags'); return data.data },
    staleTime: 60_000,
  })

  // ── State ─────────────────────────────────────────────────────────────────

  const [def, setDef] = useState<FilterDef>(() => value)
  const defRef = useRef<FilterDef>(value)
  const [groupOpen, setGroupOpen] = useState(false)
  const [exGroupOpen, setExGroupOpen] = useState(false)
  const [suppOpen, setSuppOpen] = useState(false)
  const [groupSearch, setGroupSearch] = useState('')
  const [exGroupSearch, setExGroupSearch] = useState('')
  const [suppSearch, setSuppSearch] = useState('')
  const [tagInput, setTagInput] = useState('')
  const [tagFocused, setTagFocused] = useState(false)
  const tagInputRef = useRef<HTMLInputElement>(null)
  const [activeAttrTypeId, setActiveAttrTypeId] = useState('')
  const [attrTypeOpen, setAttrTypeOpen] = useState(false)
  const [attrValueOpen, setAttrValueOpen] = useState(false)
  const [attrTypeSearch, setAttrTypeSearch] = useState('')
  const [attrValueSearch, setAttrValueSearch] = useState('')

  const refs = useMemo(() => ({ groups: productGroups, attrTypes, suppliers }), [productGroups, attrTypes, suppliers])

  // ── Emit ──────────────────────────────────────────────────────────────────

  const emitDef = useCallback((next: FilterDef) => {
    onChange(next, buildDescription(next, refs))
  }, [onChange, refs])

  const update = useCallback((patch: Partial<FilterDef>) => {
    const next = { ...defRef.current, ...patch }
    defRef.current = next
    setDef(next)
    emitDef(next)
  }, [emitDef])

  useEffect(() => {
    if (productGroups.length || attrTypes.length || suppliers.length)
      emitDef(defRef.current)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [productGroups.length, attrTypes.length, suppliers.length])

  // ── Helpers ──────────────────────────────────────────────────────────────

  function toggleMultiId(key: 'productGroupIds' | 'excludedProductGroupIds' | 'supplierIds', id: string) {
    const list = (def[key] ?? []) as string[]
    const next = list.includes(id) ? list.filter(x => x !== id) : [...list, id]
    update({ [key]: next.length ? next : undefined })
  }

  function addTag(tag: string) {
    const trimmed = tag.trim()
    if (!trimmed) return
    const current = def.tags ?? []
    if (current.includes(trimmed)) return
    update({ tags: [...current, trimmed] })
    setTagInput('')
  }

  function removeTag(tag: string) {
    const next = (def.tags ?? []).filter(t => t !== tag)
    update({ tags: next.length ? next : undefined })
  }

  function selectAttrType(typeId: string) {
    setActiveAttrTypeId(typeId)
    setAttrTypeOpen(false)
    setAttrValueOpen(false)
    setAttrTypeSearch('')
    setAttrValueSearch('')
  }

  function removeAttrFilter(typeId: string) {
    update({ attributeFilters: (defRef.current.attributeFilters ?? []).filter(f => f.attributeTypeId !== typeId) })
  }

  function toggleAttrValue(valueId: string) {
    if (!activeAttrTypeId) return
    const current = defRef.current.attributeFilters ?? []
    const existing = current.find(f => f.attributeTypeId === activeAttrTypeId)
    const values = existing?.valueIds ?? []
    const valueIds = values.includes(valueId) ? values.filter(id => id !== valueId) : [...values, valueId]
    const next = existing
      ? current.map(f => f.attributeTypeId === activeAttrTypeId ? { ...f, valueIds } : f)
      : [...current, { attributeTypeId: activeAttrTypeId, valueIds }]
    update({ attributeFilters: next.filter(f => f.valueIds.length > 0) })
  }

  const description = useMemo(() => buildDescription(def, refs), [def, refs])
  const attrFilters = def.attributeFilters ?? []
  const activeAttrType = attrTypes.find(a => a.id === activeAttrTypeId)
  const activeValueIds = attrFilters.find(f => f.attributeTypeId === activeAttrTypeId)?.valueIds ?? []
  const availableTypes = attrTypes.filter(a => matchesSearch(attrTypeSearch, tr(a.nameI18n), a.code))
  const availableValues = (activeAttrType?.values ?? []).filter(val => matchesSearch(
    attrValueSearch, tr(val.nameI18n),
    ...(val.filterColors ?? []).flatMap(color => [color.code, tr(color.nameI18n)]),
  ))
  const hasFilters = description !== 'Tüm ürünler'
  // Odaklanıldığında tüm mevcut etiketler, yazılınca filtrelenir
  const tagSuggestions = allTags.filter(
    t => !def.tags?.includes(t) && t.toLowerCase().includes(tagInput.toLowerCase()),
  )
  const filteredProductGroups = useMemo(
    () => productGroups.filter(g => matchesSearch(groupSearch, tr(g.nameI18n), g.code)),
    [groupSearch, productGroups],
  )
  const filteredExcludedProductGroups = useMemo(
    () => productGroups.filter(g => matchesSearch(exGroupSearch, tr(g.nameI18n), g.code)),
    [exGroupSearch, productGroups],
  )
  const filteredSuppliers = useMemo(
    () => suppliers.filter(s => matchesSearch(suppSearch, s.title, s.code)),
    [suppSearch, suppliers],
  )

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5">

      {/* Ürün Grupları */}
      <Section title="Ürün Grupları">
        <ChipList items={def.productGroupIds ?? []}
          label={id => tr(productGroups.find(g => g.id === id)?.nameI18n) || '…'}
          onRemove={id => toggleMultiId('productGroupIds', id)} />
        <Dropdown label={def.productGroupIds?.length ? 'Başka grup ekle' : 'Ürün grubu seç'}
          open={groupOpen} onToggle={() => setGroupOpen(o => !o)}
          searchValue={groupSearch} onSearchChange={setGroupSearch} searchPlaceholder="Ürün grubu ara…">
          {filteredProductGroups.map(g => (
            <DropItem key={g.id} selected={(def.productGroupIds ?? []).includes(g.id)}
              onChange={() => toggleMultiId('productGroupIds', g.id)}>
              {tr(g.nameI18n, g.code)}
            </DropItem>
          ))}
          {filteredProductGroups.length === 0 && <EmptySearchResult />}
        </Dropdown>
      </Section>

      {/* Hariç Tutulan Ürün Grupları */}
      <Section title="Hariç Tutulan Ürün Grupları" hint='"Tümü, şu gruplar hariç" için: dahil listesi boş bırakılır, hariçler buraya eklenir'>
        <ChipList items={def.excludedProductGroupIds ?? []}
          label={id => tr(productGroups.find(g => g.id === id)?.nameI18n) || '…'}
          onRemove={id => toggleMultiId('excludedProductGroupIds', id)} />
        <Dropdown label={def.excludedProductGroupIds?.length ? 'Başka grup hariç tut' : 'Hariç tutulacak grubu seç'}
          open={exGroupOpen} onToggle={() => setExGroupOpen(o => !o)}
          searchValue={exGroupSearch} onSearchChange={setExGroupSearch} searchPlaceholder="Hariç tutulacak grubu ara…">
          {filteredExcludedProductGroups.map(g => (
            <DropItem key={g.id} selected={(def.excludedProductGroupIds ?? []).includes(g.id)}
              onChange={() => toggleMultiId('excludedProductGroupIds', g.id)}>
              {tr(g.nameI18n, g.code)}
            </DropItem>
          ))}
          {filteredExcludedProductGroups.length === 0 && <EmptySearchResult />}
        </Dropdown>
      </Section>

      {/* Tedarikçi */}
      <Section title="Tedarikçi">
        <ChipList items={def.supplierIds ?? []}
          label={id => suppliers.find(s => s.id === id)?.title ?? '…'}
          onRemove={id => toggleMultiId('supplierIds', id)} />
        {suppliers.length > 0 ? (
          <Dropdown label={def.supplierIds?.length ? 'Başka tedarikçi ekle' : 'Tedarikçi seç'}
            open={suppOpen} onToggle={() => setSuppOpen(o => !o)}
            searchValue={suppSearch} onSearchChange={setSuppSearch} searchPlaceholder="Tedarikçi ara…">
            {filteredSuppliers.map(s => (
              <DropItem key={s.id} selected={(def.supplierIds ?? []).includes(s.id)}
                onChange={() => toggleMultiId('supplierIds', s.id)}>
                {s.title}
              </DropItem>
            ))}
            {filteredSuppliers.length === 0 && <EmptySearchResult />}
          </Dropdown>
        ) : (
          <span className="text-xs" style={{ color: 'var(--text-s)' }}>Tanımlı tedarikçi yok</span>
        )}
      </Section>

      {/* Temel Fiyat */}
      <Section title="Temel Fiyat" hint="BasePrice aralığı">
        <RangeRow unit="₺"
          minVal={def.priceMin != null ? String(def.priceMin) : ''}
          maxVal={def.priceMax != null ? String(def.priceMax) : ''}
          onMin={v => update({ priceMin: v !== '' ? +v : null })}
          onMax={v => update({ priceMax: v !== '' ? +v : null })}
          onClear={() => update({ priceMin: null, priceMax: null })}
        />
      </Section>

      {/* Platform Fiyatı */}
      <Section title="Platform Fiyatı" hint="Platforma özel satış fiyatı">
        <RangeRow unit="₺"
          minVal={def.platformPriceMin != null ? String(def.platformPriceMin) : ''}
          maxVal={def.platformPriceMax != null ? String(def.platformPriceMax) : ''}
          onMin={v => update({ platformPriceMin: v !== '' ? +v : null })}
          onMax={v => update({ platformPriceMax: v !== '' ? +v : null })}
          onClear={() => update({ platformPriceMin: null, platformPriceMax: null })}
        />
      </Section>

      {/* İndirim + KDV */}
      <div className="grid grid-cols-2 gap-4">
        <Section title="Min. İndirim">
          <div className="flex items-center gap-2">
            <NumInput value={def.discountMinPercent != null ? String(def.discountMinPercent) : ''} unit="%"
              onChange={v => update({ discountMinPercent: v !== '' ? +v : null })} />
            {def.discountMinPercent != null && (
              <ClearBtn onClick={() => update({ discountMinPercent: null })} />
            )}
          </div>
        </Section>
        <Section title="KDV Oranı" hint="aralık">
          <RangeRow unit="%" placeholder="0/100"
            minVal={def.taxRateMin != null ? String(def.taxRateMin) : ''}
            maxVal={def.taxRateMax != null ? String(def.taxRateMax) : ''}
            onMin={v => update({ taxRateMin: v !== '' ? +v : null })}
            onMax={v => update({ taxRateMax: v !== '' ? +v : null })}
            onClear={() => update({ taxRateMin: null, taxRateMax: null })}
          />
        </Section>
      </div>

      {/* Durum */}
      <Section title="Ürün Durumu">
        <ThreeWayToggle value={def.isActive} labels={['Tümü', 'Sadece Aktif', 'Sadece Pasif']}
          onChange={v => update({ isActive: v })} />
      </Section>

      {/* Stok Miktarı */}
      <Section title="Stok Miktarı" hint="Tüm depolar toplamı (adet)">
        <RangeRow unit="adet" placeholder="0/∞"
          minVal={def.stockMin != null ? String(def.stockMin) : ''}
          maxVal={def.stockMax != null ? String(def.stockMax) : ''}
          onMin={v => update({ stockMin: v !== '' ? +v : null })}
          onMax={v => update({ stockMax: v !== '' ? +v : null })}
          onClear={() => update({ stockMin: null, stockMax: null })}
        />
      </Section>

      {/* F1 Kanal kapsamı kriterleri */}
      {channelScope && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Section title="Ürün Kaynağı" hint="Boş = kanalın izin verdiği tüm kaynaklar; kanalın kapalı olduğu kaynak seçilse de kapsama girmez">
            <div className="flex flex-wrap gap-3">
              {[
                { v: 'own', l: 'Bizim ürünlerimiz' },
                { v: 'seller', l: 'Satıcı ürünleri' },
                { v: 'supply', l: 'Dış tedarik kaynağı' },
              ].map(o => (
                <label key={o.v} className="flex items-center gap-1.5 cursor-pointer text-sm" style={{ color: 'var(--text)' }}>
                  <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
                    checked={(def.sourceTypes ?? []).includes(o.v)}
                    onChange={e => {
                      const cur = def.sourceTypes ?? []
                      const next = e.target.checked ? [...cur, o.v] : cur.filter(x => x !== o.v)
                      update({ sourceTypes: next.length ? next : undefined })
                    }} />
                  {o.l}
                </label>
              ))}
            </div>
          </Section>
          <Section title="Kanal Fiyatı" hint="Bu kanalda fiyatı (kanal fiyatı > 0) olan ürünler">
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
                checked={def.hasChannelPrice === true}
                onChange={e => update({ hasChannelPrice: e.target.checked ? true : null })} />
              <span className="text-sm" style={{ color: 'var(--text)' }}>Yalnız bu kanalda fiyatı olanlar</span>
            </label>
          </Section>
          <Section title="Kanal Stok Eşiği" hint="net stok ≥ eşik olan en az bir varyant (kanala verilen adet = net − eşik + 1)">
            <div className="flex items-center gap-2">
              <div className="relative w-32">
                <input type="number" className="inp !pr-10" placeholder="örn. 3" min={1}
                  value={def.minStock != null ? String(def.minStock) : ''}
                  onChange={e => update({ minStock: e.target.value !== '' ? Math.max(1, +e.target.value) : null })} />
                <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>adet</span>
              </div>
              {def.minStock != null && <ClearBtn onClick={() => update({ minStock: null })} />}
            </div>
          </Section>
        </div>
      )}

      {/* Oluşturma Tarihi */}
      <Section title="Oluşturma Tarihi" hint="Yeni eklenen ürünler">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <div className="relative w-32">
              <input type="number" className="inp !pr-8" placeholder="Gün sayısı" min={1}
                value={def.createdAfterDays != null ? String(def.createdAfterDays) : ''}
                onChange={e => update({
                  createdAfterDays: e.target.value !== '' ? +e.target.value : null,
                  createdAfter: null, createdBefore: null,
                })} />
              <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>gün</span>
            </div>
            <span className="text-sm" style={{ color: 'var(--text-s)' }}>
              {def.createdAfterDays ? `Son ${def.createdAfterDays} günde eklenmiş` : 'veya tarih aralığı:'}
            </span>
            {def.createdAfterDays && <ClearBtn onClick={() => update({ createdAfterDays: null })} />}
          </div>
          {!def.createdAfterDays && (
            <DateRange
              after={def.createdAfter ?? ''} before={def.createdBefore ?? ''}
              onAfter={v => update({ createdAfter: v || null })}
              onBefore={v => update({ createdBefore: v || null })}
              onClear={() => update({ createdAfter: null, createdBefore: null })}
            />
          )}
        </div>
      </Section>

      {/* Resim Güncelleme Tarihi */}
      <Section title="Resim Güncelleme Tarihi" hint="Fotoğrafı güncellenen ürünler">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <div className="relative w-32">
              <input type="number" className="inp !pr-8" placeholder="Gün sayısı" min={1}
                value={def.imageUpdatedAfterDays != null ? String(def.imageUpdatedAfterDays) : ''}
                onChange={e => update({
                  imageUpdatedAfterDays: e.target.value !== '' ? +e.target.value : null,
                  imageUpdatedAfter: null, imageUpdatedBefore: null,
                })} />
              <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>gün</span>
            </div>
            <span className="text-sm" style={{ color: 'var(--text-s)' }}>
              {def.imageUpdatedAfterDays ? `Son ${def.imageUpdatedAfterDays} günde resim değişmiş` : 'veya tarih aralığı:'}
            </span>
            {def.imageUpdatedAfterDays && <ClearBtn onClick={() => update({ imageUpdatedAfterDays: null })} />}
          </div>
          {!def.imageUpdatedAfterDays && (
            <DateRange
              after={def.imageUpdatedAfter ?? ''} before={def.imageUpdatedBefore ?? ''}
              onAfter={v => update({ imageUpdatedAfter: v || null })}
              onBefore={v => update({ imageUpdatedBefore: v || null })}
              onClear={() => update({ imageUpdatedAfter: null, imageUpdatedBefore: null })}
            />
          )}
        </div>
      </Section>

      {/* Etiketler */}
      <Section title="Etiketler" hint="En az biri eşleşmeli">
        {(def.tags ?? []).length > 0 && (
          <div className="flex flex-wrap gap-1.5 mb-2">
            {(def.tags ?? []).map(t => (
              <Chip key={t} onRemove={() => removeTag(t)}>#{t}</Chip>
            ))}
          </div>
        )}
        <div className="relative">
          <input
            ref={tagInputRef}
            className="inp text-sm"
            placeholder="Etiket yaz veya listeden seç…"
            value={tagInput}
            onChange={e => setTagInput(e.target.value)}
            onFocus={() => setTagFocused(true)}
            onBlur={() => setTimeout(() => setTagFocused(false), 150)}
            onKeyDown={e => {
              if (e.key === 'Enter' || e.key === ',') { e.preventDefault(); addTag(tagInput) }
              if (e.key === 'Escape') setTagFocused(false)
            }}
          />

          {/* Sağ tarafa "+ ekle" butonu — özel etiket için */}
          {tagInput.trim() && !tagSuggestions.find(t => t === tagInput.trim()) && (
            <button type="button"
              className="absolute right-2 top-1/2 -translate-y-1/2 text-xs px-2 py-0.5 rounded-full"
              style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}
              onMouseDown={e => { e.preventDefault(); addTag(tagInput) }}>
              + ekle
            </button>
          )}

          {/* Açılır liste: focus olunca tüm etiketler, yazınca filtrelenir */}
          {tagFocused && (tagSuggestions.length > 0 || tagInput.trim()) && (
            <div className="absolute z-20 mt-1 w-full rounded-xl shadow-lg overflow-y-auto max-h-48"
              style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
              {tagSuggestions.length === 0 && tagInput.trim() && (
                <div className="px-3 py-2 text-sm" style={{ color: 'var(--text-s)' }}>
                  Eşleşen etiket yok — Enter ile ekle
                </div>
              )}
              {tagSuggestions.map(t => (
                <button key={t} type="button"
                  className="w-full text-left px-3 py-1.5 text-sm hover:bg-[var(--surface2)] transition-colors"
                  style={{ color: 'var(--text)' }}
                  onMouseDown={e => { e.preventDefault(); addTag(t) }}>
                  <span style={{ color: 'var(--text-s)' }}>#</span>{t}
                </button>
              ))}
              {tagSuggestions.length === 0 && !tagInput.trim() && (
                <div className="px-3 py-2 text-sm" style={{ color: 'var(--text-s)' }}>
                  Henüz hiç etiket yok — yazmaya başlayın
                </div>
              )}
            </div>
          )}
        </div>
        <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
          Listeden seç veya yaz + Enter ile özel etiket ekle.
        </p>
      </Section>

      {/* Özellik Filtreleri */}
      <Section title="Özellik Filtreleri" hint="Renk, beden, cinsiyet vb.">
        <div className="p-3 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
          <div className="grid grid-cols-1 gap-2 md:grid-cols-2">
            <SearchSelect
              label={activeAttrType ? tr(activeAttrType.nameI18n, activeAttrType.code) : 'Özellik seçin'}
              open={attrTypeOpen}
              onToggle={() => { setAttrTypeOpen(open => !open); setAttrValueOpen(false) }}
              searchValue={attrTypeSearch} onSearchChange={setAttrTypeSearch}
              searchPlaceholder="Özellik ara…">
              {availableTypes.map(item => (
                <button key={item.id} type="button"
                  className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-[var(--surface2)]"
                  style={{ color: item.id === activeAttrTypeId ? 'var(--brand)' : 'var(--text)' }}
                  onClick={() => selectAttrType(item.id)}>
                  <span className="min-w-0 flex-1 truncate">{tr(item.nameI18n, item.code)}</span>
                  {attrFilters.some(f => f.attributeTypeId === item.id) && <span className="text-xs">Filtre eklendi</span>}
                </button>
              ))}
              {availableTypes.length === 0 && <EmptySearchResult />}
            </SearchSelect>
            <SearchSelect
              label={!activeAttrType ? 'Önce özellik seçin' : activeValueIds.length ? `${activeValueIds.length} değer seçildi` : 'Değer seçin'}
              open={attrValueOpen} disabled={!activeAttrType}
              onToggle={() => { setAttrValueOpen(open => !open); setAttrTypeOpen(false) }}
              searchValue={attrValueSearch} onSearchChange={setAttrValueSearch}
              searchPlaceholder="Değer ara…">
              {availableValues.map(val => {
                const hex = val.filterColors?.[0]?.hexCode
                return (
                  <DropItem key={val.id} selected={activeValueIds.includes(val.id)} onChange={() => toggleAttrValue(val.id)}>
                    <span className="flex min-w-0 items-center gap-2">
                      {hex && <span className="h-3 w-3 flex-shrink-0 rounded-full border border-black/10" style={{ background: hex }} />}
                      <span className="truncate">{tr(val.nameI18n)}</span>
                    </span>
                  </DropItem>
                )
              })}
              {activeAttrType && !activeAttrType.values.length && (
                <div className="px-3 py-3 text-sm" style={{ color: 'var(--text-s)' }}>Bu özellik için değer yok</div>
              )}
              {!!activeAttrType?.values.length && availableValues.length === 0 && <EmptySearchResult />}
            </SearchSelect>
          </div>
          {attrFilters.length > 0 ? (
            <div className="mt-3 flex flex-wrap gap-2" aria-label="Seçilen özellik filtreleri">
              {attrFilters.map(filter => {
                const type = attrTypes.find(a => a.id === filter.attributeTypeId)
                const name = type ? tr(type.nameI18n, type.code) : 'Özellik'
                const summary = filter.valueIds.map(id => tr(type?.values.find(v => v.id === id)?.nameI18n, id)).join(', ')
                return (
                  <span key={filter.attributeTypeId}
                    className="inline-flex max-w-full items-center rounded-full border text-xs"
                    style={{ background: 'var(--brand-bg)', color: 'var(--brand)', borderColor: 'var(--brand)' }}>
                    <button type="button" title={`${name}: ${summary}`}
                      aria-label={`${name} filtresini düzenle: ${summary}`}
                      aria-pressed={activeAttrTypeId === filter.attributeTypeId}
                      className="min-w-0 rounded-l-full px-3 py-1.5 text-left hover:underline"
                      onClick={() => { selectAttrType(filter.attributeTypeId); setAttrValueOpen(true) }}>
                      <span className="block max-w-72 truncate"><strong>{name}:</strong> {summary}</span>
                    </button>
                    <button type="button" aria-label={`${name} filtresini kaldır`}
                      className="mr-1 rounded-full p-1 hover:bg-[var(--surface)]"
                      onClick={() => removeAttrFilter(filter.attributeTypeId)}>
                      <X size={12} />
                    </button>
                  </span>
                )
              })}
            </div>
          ) : (
            <p className="mt-2 text-xs" style={{ color: 'var(--text-s)' }}>Özellik ve değer seçerek filtre ekleyin.</p>
          )}
        </div>
      </Section>

      {/* Otomatik açıklama */}
      <div className="flex items-start gap-2 p-3 rounded-xl text-sm"
        style={{ background: hasFilters ? 'var(--brand-bg)' : 'var(--surface2)', border: '1px solid var(--border)' }}>
        <Tag size={14} className="flex-shrink-0 mt-0.5" style={{ color: 'var(--brand)' }} />
        <div>
          <span className="text-xs font-medium block mb-0.5" style={{ color: 'var(--text-s)' }}>Otomatik açıklama</span>
          <span style={{ color: hasFilters ? 'var(--brand)' : 'var(--text-m)' }}>{description}</span>
        </div>
      </div>
    </div>
  )
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function Section({ title, hint, children }: { title: string; hint?: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="flex items-baseline gap-2 mb-2">
        <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{title}</span>
        {hint && <span className="text-xs" style={{ color: 'var(--text-s)' }}>{hint}</span>}
      </div>
      {children}
    </div>
  )
}

function Chip({ children, onRemove }: { children: React.ReactNode; onRemove: () => void }) {
  return (
    <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium"
      style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand)' }}>
      {children}
      <button type="button" onClick={onRemove} className="ml-0.5 opacity-60 hover:opacity-100"><X size={10} /></button>
    </span>
  )
}

function ChipList({ items, label, onRemove }: { items: string[]; label: (id: string) => string; onRemove: (id: string) => void }) {
  if (!items.length) return null
  return (
    <div className="flex flex-wrap gap-1.5 mb-2">
      {items.map(id => <Chip key={id} onRemove={() => onRemove(id)}>{label(id)}</Chip>)}
    </div>
  )
}

function Dropdown({ label, open, onToggle, searchValue, onSearchChange, searchPlaceholder, children }: {
  label: string
  open: boolean
  onToggle: () => void
  searchValue: string
  onSearchChange: (value: string) => void
  searchPlaceholder: string
  children: React.ReactNode
}) {
  const rootRef = useCloseOnOutside(open, onToggle)

  return (
    <div ref={rootRef} className="relative">
      <button type="button"
        className="flex items-center gap-2 text-sm px-3 py-1.5 rounded-lg border transition-colors"
        style={{ borderColor: 'var(--border)', color: 'var(--text-m)', background: 'var(--surface2)' }}
        onClick={onToggle}>
        <Plus size={12} /> {label} <ChevronDown size={12} className={cn('transition-transform', open && 'rotate-180')} />
      </button>
      {open && (
        <div className="absolute z-20 mt-1 w-72 rounded-xl shadow-lg overflow-hidden"
          style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          <div className="p-2" style={{ borderBottom: '1px solid var(--border)' }}>
            <div className="relative">
              <Search size={14} className="absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none"
                style={{ color: 'var(--text-s)' }} />
              <input
                autoFocus
                type="search"
                className="inp w-full !pl-8 !pr-8 text-sm"
                placeholder={searchPlaceholder}
                value={searchValue}
                onChange={event => onSearchChange(event.target.value)}
                onKeyDown={event => { if (event.key === 'Escape') onToggle() }}
              />
              {searchValue && (
                <button type="button" aria-label="Aramayı temizle"
                  className="absolute right-2.5 top-1/2 -translate-y-1/2"
                  style={{ color: 'var(--text-s)' }}
                  onClick={() => onSearchChange('')}>
                  <X size={13} />
                </button>
              )}
            </div>
          </div>
          <div className="py-1 overflow-y-auto max-h-56">{children}</div>
        </div>
      )}
    </div>
  )
}

function DropItem({ selected, onChange, children }: { selected: boolean; onChange: () => void; children: React.ReactNode }) {
  return (
    <label
      className="flex w-full cursor-pointer items-center gap-2.5 px-3 py-2 text-sm transition-colors hover:bg-[var(--surface2)]"
      style={{ color: selected ? 'var(--brand)' : 'var(--text)' }}
    >
      <input type="checkbox" checked={selected} onChange={onChange}
        className="h-4 w-4 flex-shrink-0 rounded accent-[var(--brand)]" />
      <span className="min-w-0 flex-1 truncate">{children}</span>
    </label>
  )
}

function EmptySearchResult() {
  return <div className="px-3 py-3 text-sm" style={{ color: 'var(--text-s)' }}>Eşleşen kayıt bulunamadı</div>
}

function SearchSelect({ label, open, disabled = false, onToggle, searchValue, onSearchChange, searchPlaceholder, children }: {
  label: string
  open: boolean
  disabled?: boolean
  onToggle: () => void
  searchValue: string
  onSearchChange: (value: string) => void
  searchPlaceholder: string
  children: React.ReactNode
}) {
  const rootRef = useCloseOnOutside(open, onToggle)

  return (
    <div ref={rootRef} className="relative min-w-0">
      <button type="button" disabled={disabled} aria-expanded={open}
        className="inp flex w-full items-center justify-between gap-2 text-left text-sm disabled:cursor-not-allowed disabled:opacity-60"
        onClick={onToggle}>
        <span className="min-w-0 flex-1 truncate">{label}</span>
        <ChevronDown size={14} className={cn('flex-shrink-0 transition-transform', open && 'rotate-180')} />
      </button>
      {open && !disabled && (
        <div className="absolute left-0 right-0 z-30 mt-1 min-w-64 overflow-hidden rounded-xl shadow-lg"
          style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          <div className="p-2" style={{ borderBottom: '1px solid var(--border)' }}>
            <div className="relative">
              <Search size={14} className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2"
                style={{ color: 'var(--text-s)' }} />
              <input autoFocus type="search" className="inp w-full !pl-8 !pr-8 text-sm"
                placeholder={searchPlaceholder} value={searchValue}
                onChange={event => onSearchChange(event.target.value)}
                onKeyDown={event => { if (event.key === 'Escape') onToggle() }} />
              {searchValue && (
                <button type="button" aria-label="Aramayı temizle"
                  className="absolute right-2.5 top-1/2 -translate-y-1/2"
                  style={{ color: 'var(--text-s)' }} onClick={() => onSearchChange('')}>
                  <X size={13} />
                </button>
              )}
            </div>
          </div>
          <div className="max-h-56 overflow-y-auto py-1">{children}</div>
        </div>
      )}
    </div>
  )
}

function RangeRow({ minVal, maxVal, unit, placeholder, onMin, onMax, onClear }: {
  minVal: string; maxVal: string; unit: string; placeholder?: string
  onMin: (v: string) => void; onMax: (v: string) => void; onClear: () => void
}) {
  return (
    <div className="flex items-center gap-2">
      <div className="relative flex-1">
        <input type="number" className="inp !pr-8" placeholder={placeholder?.split('/')[0] ?? 'Min'}
          value={minVal} onChange={e => onMin(e.target.value)} min={0} />
        <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>{unit}</span>
      </div>
      <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span>
      <div className="relative flex-1">
        <input type="number" className="inp !pr-8" placeholder={placeholder?.split('/')[1] ?? 'Maks'}
          value={maxVal} onChange={e => onMax(e.target.value)} min={0} />
        <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>{unit}</span>
      </div>
      {(minVal || maxVal) && <ClearBtn onClick={onClear} />}
    </div>
  )
}

function DateRange({ after, before, onAfter, onBefore, onClear }: {
  after: string; before: string; onAfter: (v: string) => void; onBefore: (v: string) => void; onClear: () => void
}) {
  return (
    <div className="flex items-center gap-2">
      <div className="flex-1">
        <input type="date" className="inp text-sm" value={after} onChange={e => onAfter(e.target.value)} />
      </div>
      <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
      <div className="flex-1">
        <input type="date" className="inp text-sm" value={before} onChange={e => onBefore(e.target.value)} />
      </div>
      {(after || before) && <ClearBtn onClick={onClear} />}
    </div>
  )
}

function NumInput({ value, unit, placeholder, onChange }: { value: string; unit: string; placeholder?: string; onChange: (v: string) => void }) {
  return (
    <div className="relative w-28">
      <input type="number" className="inp !pr-6" placeholder={placeholder ?? '0'} value={value} onChange={e => onChange(e.target.value)} min={0} />
      <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>{unit}</span>
    </div>
  )
}

function ClearBtn({ onClick }: { onClick: () => void }) {
  return <button type="button" onClick={onClick} style={{ color: 'var(--text-s)' }}><X size={14} /></button>
}

function ThreeWayToggle({ value, labels, onChange }: {
  value: boolean | null | undefined; labels: [string, string, string]; onChange: (v: boolean | null) => void
}) {
  const opts: [string, boolean | null][] = [[labels[0], null], [labels[1], true], [labels[2], false]]
  return (
    <div className="flex rounded-lg overflow-hidden text-xs" style={{ border: '1px solid var(--border)' }}>
      {opts.map(([lbl, val]) => {
        const active = (value ?? null) === val
        return (
          <button key={String(val)} type="button"
            className="flex-1 px-2 py-1.5 text-center transition-colors"
            style={{ background: active ? 'var(--brand)' : 'var(--surface2)', color: active ? '#fff' : 'var(--text-m)' }}
            onClick={() => onChange(val)}>
            {lbl}
          </button>
        )
      })}
    </div>
  )
}
