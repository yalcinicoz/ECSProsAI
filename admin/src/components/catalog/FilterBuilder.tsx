import { useState, useEffect, useMemo, useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Plus, X, Tag, ChevronDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'

// ── Types ──────────────────────────────────────────────────────────────────────

export interface FilterDef {
  productGroupIds?: string[]
  priceMin?: number | null
  priceMax?: number | null
  platformPriceMin?: number | null
  platformPriceMax?: number | null
  discountMinPercent?: number | null
  taxRateMin?: number | null
  taxRateMax?: number | null
  supplierIds?: string[]
  categoryIds?: string[]
  isActive?: boolean | null
  hasStock?: boolean | null
  createdAfter?: string | null
  createdBefore?: string | null
  attributeFilters?: AttributeFilterItem[]
}

interface AttributeFilterItem {
  attributeTypeId: string
  valueIds: string[]
}

interface ProductGroup { id: string; code: string; nameI18n: Record<string, string> }
interface AttributeValue {
  id: string; nameI18n: Record<string, string>
  filterColors: { code: string; nameI18n: Record<string, string>; hexCode?: string }[]
}
interface AttributeType { id: string; code: string; nameI18n: Record<string, string>; values: AttributeValue[] }
interface Supplier { id: string; title: string; code: string }
interface CategoryItem { id: string; code: string; nameI18n: Record<string, string>; parentId: string | null }

// ── Helpers ────────────────────────────────────────────────────────────────────

function tr(i18n: Record<string, string> | undefined, fallback = ''): string {
  if (!i18n) return fallback
  return i18n['tr'] ?? i18n['en'] ?? i18n[Object.keys(i18n)[0]] ?? fallback
}

/** Tüm filtre seçimlerini okunabilir metne çevirir. */
export function buildDescription(
  def: FilterDef,
  refs: {
    groups: ProductGroup[]
    attrTypes: AttributeType[]
    suppliers: Supplier[]
    categories: CategoryItem[]
  },
): string {
  const parts: string[] = []

  if (def.productGroupIds?.length) {
    const names = def.productGroupIds.map(id => tr(refs.groups.find(g => g.id === id)?.nameI18n)).filter(Boolean)
    if (names.length) parts.push(names.join(', ') + (names.length > 1 ? ' gruplarından' : ' grubundan'))
  }

  if (def.categoryIds?.length) {
    const names = def.categoryIds.map(id => tr(refs.categories.find(c => c.id === id)?.nameI18n)).filter(Boolean)
    if (names.length) parts.push('Kategori: ' + names.join(', '))
  }

  if (def.supplierIds?.length) {
    const names = def.supplierIds.map(id => refs.suppliers.find(s => s.id === id)?.title ?? '?').filter(Boolean)
    if (names.length) parts.push('Tedarikçi: ' + names.join(', '))
  }

  if (def.priceMin != null && def.priceMax != null) parts.push(`Fiyat: ${def.priceMin}₺–${def.priceMax}₺`)
  else if (def.priceMin != null) parts.push(`Min fiyat: ${def.priceMin}₺`)
  else if (def.priceMax != null) parts.push(`Maks fiyat: ${def.priceMax}₺`)

  if (def.platformPriceMin != null && def.platformPriceMax != null) parts.push(`Platform fiyatı: ${def.platformPriceMin}₺–${def.platformPriceMax}₺`)
  else if (def.platformPriceMin != null) parts.push(`Min platform fiyatı: ${def.platformPriceMin}₺`)
  else if (def.platformPriceMax != null) parts.push(`Maks platform fiyatı: ${def.platformPriceMax}₺`)

  if (def.discountMinPercent != null) parts.push(`Min %${def.discountMinPercent} indirim`)
  if (def.taxRateMin != null && def.taxRateMax != null) parts.push(`KDV: %${def.taxRateMin}–%${def.taxRateMax}`)
  else if (def.taxRateMin != null) parts.push(`Min KDV: %${def.taxRateMin}`)
  else if (def.taxRateMax != null) parts.push(`Maks KDV: %${def.taxRateMax}`)

  if (def.isActive === true) parts.push('Sadece aktif ürünler')
  if (def.isActive === false) parts.push('Sadece pasif ürünler')
  if (def.hasStock === true) parts.push('Stokta var')
  if (def.hasStock === false) parts.push('Stok yok')

  if (def.createdAfter) parts.push(`${def.createdAfter.slice(0, 10)} tarihinden sonra`)
  if (def.createdBefore) parts.push(`${def.createdBefore.slice(0, 10)} tarihinden önce`)

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

interface FilterBuilderProps {
  value: FilterDef
  onChange: (def: FilterDef, description: string) => void
}

// ── Component ──────────────────────────────────────────────────────────────────

export function FilterBuilder({ value, onChange }: FilterBuilderProps) {
  const { data: productGroups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups-simple'],
    queryFn: async () => { const { data } = await api.get('/catalog/product-groups'); return data.data },
    staleTime: 60_000,
  })
  const { data: attrTypes = [] } = useQuery<AttributeType[]>({
    queryKey: ['attr-types-with-values'],
    queryFn: async () => { const { data } = await api.get('/catalog/attribute-types'); return data.data },
    staleTime: 60_000,
  })
  const { data: suppliers = [] } = useQuery<Supplier[]>({
    queryKey: ['accounts-suppliers'],
    queryFn: async () => {
      const { data } = await api.get('/accounts?pageSize=200')
      return (data.data?.items ?? data.data ?? []).filter((a: Supplier & { accountType?: string }) =>
        !a.accountType || a.accountType === 'supplier' || a.accountType === 'both')
    },
    staleTime: 60_000,
  })
  const { data: allCategories = [] } = useQuery<CategoryItem[]>({
    queryKey: ['categories-flat'],
    queryFn: async () => { const { data } = await api.get('/catalog/categories?activeOnly=false'); return data.data },
    staleTime: 60_000,
  })

  // ── State ─────────────────────────────────────────────────────────────────

  const [selGroups,      setSelGroups]      = useState<string[]>(value.productGroupIds ?? [])
  const [priceMin,       setPriceMin]       = useState(value.priceMin != null ? String(value.priceMin) : '')
  const [priceMax,       setPriceMax]       = useState(value.priceMax != null ? String(value.priceMax) : '')
  const [platPriceMin,   setPlatPriceMin]   = useState(value.platformPriceMin != null ? String(value.platformPriceMin) : '')
  const [platPriceMax,   setPlatPriceMax]   = useState(value.platformPriceMax != null ? String(value.platformPriceMax) : '')
  const [discount,       setDiscount]       = useState(value.discountMinPercent != null ? String(value.discountMinPercent) : '')
  const [taxMin,         setTaxMin]         = useState(value.taxRateMin != null ? String(value.taxRateMin) : '')
  const [taxMax,         setTaxMax]         = useState(value.taxRateMax != null ? String(value.taxRateMax) : '')
  const [selSuppliers,   setSelSuppliers]   = useState<string[]>(value.supplierIds ?? [])
  const [selCategories,  setSelCategories]  = useState<string[]>(value.categoryIds ?? [])
  const [isActive,       setIsActive]       = useState<boolean | null | undefined>(value.isActive)
  const [hasStock,       setHasStock]       = useState<boolean | null | undefined>(value.hasStock)
  const [createdAfter,   setCreatedAfter]   = useState(value.createdAfter?.slice(0, 10) ?? '')
  const [createdBefore,  setCreatedBefore]  = useState(value.createdBefore?.slice(0, 10) ?? '')
  const [attrFilters,    setAttrFilters]    = useState<AttributeFilterItem[]>(value.attributeFilters ?? [])
  const [groupOpen,      setGroupOpen]      = useState(false)
  const [suppOpen,       setSuppOpen]       = useState(false)
  const [catOpen,        setCatOpen]        = useState(false)

  const refs = useMemo(() => ({ groups: productGroups, attrTypes, suppliers, categories: allCategories }), [productGroups, attrTypes, suppliers, allCategories])

  // ── Emit ──────────────────────────────────────────────────────────────────

  const emit = useCallback((patch: Partial<{
    groups: string[]; min: string; max: string; pMin: string; pMax: string
    disc: string; tMin: string; tMax: string; supps: string[]; cats: string[]
    active: boolean | null | undefined; stock: boolean | null | undefined
    after: string; before: string; attrs: AttributeFilterItem[]
  }>) => {
    const g = patch.groups ?? selGroups
    const mn = patch.min ?? priceMin; const mx = patch.max ?? priceMax
    const pm = patch.pMin ?? platPriceMin; const px = patch.pMax ?? platPriceMax
    const dc = patch.disc ?? discount; const tm = patch.tMin ?? taxMin; const tx = patch.tMax ?? taxMax
    const sp = patch.supps ?? selSuppliers; const ca = patch.cats ?? selCategories
    const ac = 'active' in patch ? patch.active : isActive
    const hs = 'stock' in patch ? patch.stock : hasStock
    const af = patch.after ?? createdAfter; const bf = patch.before ?? createdBefore
    const at = patch.attrs ?? attrFilters

    const def: FilterDef = {
      productGroupIds:   g.length ? g : undefined,
      priceMin:          mn !== '' ? +mn : null,
      priceMax:          mx !== '' ? +mx : null,
      platformPriceMin:  pm !== '' ? +pm : null,
      platformPriceMax:  px !== '' ? +px : null,
      discountMinPercent: dc !== '' ? +dc : null,
      taxRateMin:        tm !== '' ? +tm : null,
      taxRateMax:        tx !== '' ? +tx : null,
      supplierIds:       sp.length ? sp : undefined,
      categoryIds:       ca.length ? ca : undefined,
      isActive:          ac ?? null,
      hasStock:          hs ?? null,
      createdAfter:      af || null,
      createdBefore:     bf || null,
      attributeFilters:  at.filter(f => f.valueIds.length > 0),
    }
    onChange(def, buildDescription(def, refs))
  }, [selGroups, priceMin, priceMax, platPriceMin, platPriceMax, discount, taxMin, taxMax,
      selSuppliers, selCategories, isActive, hasStock, createdAfter, createdBefore, attrFilters, refs, onChange])

  useEffect(() => {
    if (productGroups.length || attrTypes.length || suppliers.length)
      emit({})
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [productGroups.length, attrTypes.length, suppliers.length])

  // ── Handlers ──────────────────────────────────────────────────────────────

  function toggleItem(list: string[], setList: (v: string[]) => void, id: string, emitKey: string) {
    const next = list.includes(id) ? list.filter(x => x !== id) : [...list, id]
    setList(next)
    emit({ [emitKey]: next })
  }

  function addAttrFilter() {
    const usedTypeIds = attrFilters.map(f => f.attributeTypeId)
    const first = attrTypes.find(a => !usedTypeIds.includes(a.id))
    if (!first) return
    const next = [...attrFilters, { attributeTypeId: first.id, valueIds: [] }]
    setAttrFilters(next); emit({ attrs: next })
  }
  function removeAttrFilter(idx: number) {
    const next = attrFilters.filter((_, i) => i !== idx)
    setAttrFilters(next); emit({ attrs: next })
  }
  function changeAttrType(idx: number, typeId: string) {
    const next = attrFilters.map((f, i) => i === idx ? { attributeTypeId: typeId, valueIds: [] } : f)
    setAttrFilters(next); emit({ attrs: next })
  }
  function toggleAttrValue(idx: number, valueId: string) {
    const next = attrFilters.map((f, i) => {
      if (i !== idx) return f
      const valueIds = f.valueIds.includes(valueId) ? f.valueIds.filter(v => v !== valueId) : [...f.valueIds, valueId]
      return { ...f, valueIds }
    })
    setAttrFilters(next); emit({ attrs: next })
  }

  const usedTypeIds = attrFilters.map(f => f.attributeTypeId)
  const description = useMemo(() => buildDescription({
    productGroupIds: selGroups, priceMin: priceMin !== '' ? +priceMin : null, priceMax: priceMax !== '' ? +priceMax : null,
    platformPriceMin: platPriceMin !== '' ? +platPriceMin : null, platformPriceMax: platPriceMax !== '' ? +platPriceMax : null,
    discountMinPercent: discount !== '' ? +discount : null, taxRateMin: taxMin !== '' ? +taxMin : null,
    taxRateMax: taxMax !== '' ? +taxMax : null, supplierIds: selSuppliers, categoryIds: selCategories,
    isActive: isActive ?? null, hasStock: hasStock ?? null,
    createdAfter: createdAfter || null, createdBefore: createdBefore || null, attributeFilters: attrFilters,
  }, refs), [selGroups, priceMin, priceMax, platPriceMin, platPriceMax, discount, taxMin, taxMax,
             selSuppliers, selCategories, isActive, hasStock, createdAfter, createdBefore, attrFilters, refs])

  const hasFilters = description !== 'Tüm ürünler'

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5">

      {/* Ürün Grupları */}
      <Section title="Ürün Grupları" hint="Hangi ürün gruplarından?">
        {selGroups.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mb-2">
            {selGroups.map(id => {
              const g = productGroups.find(pg => pg.id === id)
              return <Chip key={id} onRemove={() => toggleItem(selGroups, setSelGroups, id, 'groups')}>{g ? tr(g.nameI18n, g.code) : '…'}</Chip>
            })}
          </div>
        )}
        <Dropdown label={selGroups.length ? 'Başka grup ekle' : 'Ürün grubu seç'} open={groupOpen} onToggle={() => setGroupOpen(o => !o)}>
          {productGroups.map(g => (
            <DropItem key={g.id} selected={selGroups.includes(g.id)}
              onClick={() => { toggleItem(selGroups, setSelGroups, g.id, 'groups'); setGroupOpen(false) }}>
              {tr(g.nameI18n, g.code)}
            </DropItem>
          ))}
        </Dropdown>
      </Section>

      {/* Tedarikçi */}
      <Section title="Tedarikçi" hint="Ürünün tedarikçisi">
        {selSuppliers.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mb-2">
            {selSuppliers.map(id => {
              const s = suppliers.find(x => x.id === id)
              return <Chip key={id} onRemove={() => toggleItem(selSuppliers, setSelSuppliers, id, 'supps')}>{s?.title ?? '…'}</Chip>
            })}
          </div>
        )}
        {suppliers.length > 0 ? (
          <Dropdown label={selSuppliers.length ? 'Başka tedarikçi ekle' : 'Tedarikçi seç'} open={suppOpen} onToggle={() => setSuppOpen(o => !o)}>
            {suppliers.map(s => (
              <DropItem key={s.id} selected={selSuppliers.includes(s.id)}
                onClick={() => { toggleItem(selSuppliers, setSelSuppliers, s.id, 'supps'); setSuppOpen(false) }}>
                {s.title}
              </DropItem>
            ))}
          </Dropdown>
        ) : (
          <span className="text-xs" style={{ color: 'var(--text-s)' }}>Tanımlı tedarikçi yok</span>
        )}
      </Section>

      {/* Kategori filtresi */}
      <Section title="Kategoriler" hint="Ürün şu kategorilerde olmalı">
        {selCategories.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mb-2">
            {selCategories.map(id => {
              const c = allCategories.find(x => x.id === id)
              return <Chip key={id} onRemove={() => toggleItem(selCategories, setSelCategories, id, 'cats')}>{c ? tr(c.nameI18n, c.code) : '…'}</Chip>
            })}
          </div>
        )}
        <Dropdown label={selCategories.length ? 'Başka kategori ekle' : 'Kategori seç'} open={catOpen} onToggle={() => setCatOpen(o => !o)}>
          {allCategories.map(c => (
            <DropItem key={c.id} selected={selCategories.includes(c.id)}
              onClick={() => { toggleItem(selCategories, setSelCategories, c.id, 'cats'); setCatOpen(false) }}>
              {c.parentId ? `  ${tr(c.nameI18n, c.code)}` : tr(c.nameI18n, c.code)}
            </DropItem>
          ))}
        </Dropdown>
      </Section>

      {/* Fiyat */}
      <Section title="Temel Fiyat" hint="Ürünün baz fiyatına göre (BasePrice)">
        <RangeRow
          minVal={priceMin} maxVal={priceMax} unit="₺"
          onMin={v => { setPriceMin(v); emit({ min: v }) }}
          onMax={v => { setPriceMax(v); emit({ max: v }) }}
          onClear={() => { setPriceMin(''); setPriceMax(''); emit({ min: '', max: '' }) }}
        />
      </Section>

      {/* Platform Fiyatı */}
      <Section title="Platform Fiyatı" hint="Platforma özel satış fiyatına göre">
        <RangeRow
          minVal={platPriceMin} maxVal={platPriceMax} unit="₺"
          onMin={v => { setPlatPriceMin(v); emit({ pMin: v }) }}
          onMax={v => { setPlatPriceMax(v); emit({ pMax: v }) }}
          onClear={() => { setPlatPriceMin(''); setPlatPriceMax(''); emit({ pMin: '', pMax: '' }) }}
        />
      </Section>

      {/* İndirim + KDV */}
      <div className="grid grid-cols-2 gap-4">
        <Section title="Min. İndirim">
          <div className="flex items-center gap-2">
            <NumInput value={discount} unit="%" placeholder="0" onChange={v => { setDiscount(v); emit({ disc: v }) }} />
            {discount && <ClearBtn onClick={() => { setDiscount(''); emit({ disc: '' }) }} />}
          </div>
        </Section>
        <Section title="KDV Oranı" hint="aralık">
          <RangeRow
            minVal={taxMin} maxVal={taxMax} unit="%" placeholder="0/100"
            onMin={v => { setTaxMin(v); emit({ tMin: v }) }}
            onMax={v => { setTaxMax(v); emit({ tMax: v }) }}
            onClear={() => { setTaxMin(''); setTaxMax(''); emit({ tMin: '', tMax: '' }) }}
          />
        </Section>
      </div>

      {/* Durum + Stok */}
      <div className="grid grid-cols-2 gap-4">
        <Section title="Ürün Durumu">
          <ThreeWayToggle
            value={isActive}
            labels={['Tümü', 'Sadece Aktif', 'Sadece Pasif']}
            onChange={v => { setIsActive(v); emit({ active: v }) }}
          />
        </Section>
        <Section title="Stok Durumu">
          <ThreeWayToggle
            value={hasStock}
            labels={['Tümü', 'Stokta Var', 'Stok Yok']}
            onChange={v => { setHasStock(v); emit({ stock: v }) }}
          />
        </Section>
      </div>

      {/* Tarih */}
      <Section title="Oluşturma Tarihi" hint="Yeni ürün filtresi">
        <div className="flex items-center gap-2">
          <div className="flex-1">
            <label className="text-xs mb-1 block" style={{ color: 'var(--text-s)' }}>Başlangıç</label>
            <input type="date" className="inp text-sm" value={createdAfter}
              onChange={e => { setCreatedAfter(e.target.value); emit({ after: e.target.value }) }} />
          </div>
          <span className="text-sm mt-4" style={{ color: 'var(--text-s)' }}>—</span>
          <div className="flex-1">
            <label className="text-xs mb-1 block" style={{ color: 'var(--text-s)' }}>Bitiş</label>
            <input type="date" className="inp text-sm" value={createdBefore}
              onChange={e => { setCreatedBefore(e.target.value); emit({ before: e.target.value }) }} />
          </div>
          {(createdAfter || createdBefore) && (
            <button type="button" className="mt-5" style={{ color: 'var(--text-s)' }}
              onClick={() => { setCreatedAfter(''); setCreatedBefore(''); emit({ after: '', before: '' }) }}>
              <X size={14} />
            </button>
          )}
        </div>
      </Section>

      {/* Özellik Filtreleri */}
      <Section title="Özellik Filtreleri" hint="Renk, beden, cinsiyet vb.">
        {attrFilters.map((af, idx) => {
          const atype = attrTypes.find(a => a.id === af.attributeTypeId)
          return (
            <div key={idx} className="mb-3 p-3 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
              <div className="flex items-center gap-2 mb-2">
                <select className="inp text-sm flex-1" value={af.attributeTypeId}
                  onChange={e => changeAttrType(idx, e.target.value)}>
                  {attrTypes.filter(a => a.id === af.attributeTypeId || !usedTypeIds.includes(a.id))
                    .map(a => <option key={a.id} value={a.id}>{tr(a.nameI18n, a.code)}</option>)}
                </select>
                <button type="button" onClick={() => removeAttrFilter(idx)}
                  className="p-1 rounded-lg hover:bg-red-50 flex-shrink-0" style={{ color: '#ef4444' }}>
                  <X size={14} />
                </button>
              </div>
              {atype && (
                <div className="flex flex-wrap gap-1.5">
                  {atype.values.map(val => {
                    const selected = af.valueIds.includes(val.id)
                    const hex = val.filterColors?.[0]?.hexCode
                    return (
                      <button key={val.id} type="button" onClick={() => toggleAttrValue(idx, val.id)}
                        className={cn('inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border transition-all',
                          selected ? 'border-[var(--brand)]' : 'border-transparent hover:border-[var(--border)]')}
                        style={{ background: selected ? 'var(--brand-bg)' : 'var(--surface)', color: selected ? 'var(--brand)' : 'var(--text-m)' }}>
                        {hex && <span className="w-3 h-3 rounded-full border border-white/30 flex-shrink-0" style={{ background: hex }} />}
                        {tr(val.nameI18n)}
                        {selected && <span className="ml-0.5 opacity-60">×</span>}
                      </button>
                    )
                  })}
                  {atype.values.length === 0 && <span className="text-xs" style={{ color: 'var(--text-s)' }}>Değer yok</span>}
                </div>
              )}
            </div>
          )
        })}
        {attrTypes.length > usedTypeIds.length && (
          <button type="button" onClick={addAttrFilter}
            className="flex items-center gap-1.5 text-sm px-3 py-1.5 rounded-lg border border-dashed transition-colors"
            style={{ borderColor: 'var(--border)', color: 'var(--text-m)' }}>
            <Plus size={13} /> Özellik filtresi ekle
          </button>
        )}
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

function Dropdown({ label, open, onToggle, children }: {
  label: string; open: boolean; onToggle: () => void; children: React.ReactNode
}) {
  return (
    <div className="relative">
      <button type="button"
        className="flex items-center gap-2 text-sm px-3 py-1.5 rounded-lg border transition-colors"
        style={{ borderColor: 'var(--border)', color: 'var(--text-m)', background: 'var(--surface2)' }}
        onClick={onToggle}>
        <Plus size={12} /> {label} <ChevronDown size={12} className={cn('transition-transform', open && 'rotate-180')} />
      </button>
      {open && (
        <div className="absolute z-20 mt-1 w-60 rounded-xl shadow-lg py-1 overflow-y-auto max-h-52"
          style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          {children}
        </div>
      )}
    </div>
  )
}

function DropItem({ selected, onClick, children }: { selected: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button type="button"
      className={cn('w-full text-left px-3 py-2 text-sm transition-colors', !selected && 'hover:bg-[var(--surface2)]')}
      style={{ color: selected ? 'var(--brand)' : 'var(--text)' }}
      onClick={onClick}>
      {selected && <span className="mr-1">✓</span>}{children}
    </button>
  )
}

function RangeRow({ minVal, maxVal, unit, placeholder, onMin, onMax, onClear }: {
  minVal: string; maxVal: string; unit: string; placeholder?: string
  onMin: (v: string) => void; onMax: (v: string) => void; onClear: () => void
}) {
  return (
    <div className="flex items-center gap-2">
      <div className="relative flex-1">
        <input type="number" className="inp pr-6" placeholder={placeholder ? placeholder.split('/')[0] : 'Min'}
          value={minVal} onChange={e => onMin(e.target.value)} min={0} />
        <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>{unit}</span>
      </div>
      <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span>
      <div className="relative flex-1">
        <input type="number" className="inp pr-6" placeholder={placeholder ? placeholder.split('/')[1] : 'Maks'}
          value={maxVal} onChange={e => onMax(e.target.value)} min={0} />
        <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>{unit}</span>
      </div>
      {(minVal || maxVal) && <ClearBtn onClick={onClear} />}
    </div>
  )
}

function NumInput({ value, unit, placeholder, onChange }: { value: string; unit: string; placeholder?: string; onChange: (v: string) => void }) {
  return (
    <div className="relative w-28">
      <input type="number" className="inp pr-6" placeholder={placeholder} value={value} onChange={e => onChange(e.target.value)} min={0} max={100} />
      <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>{unit}</span>
    </div>
  )
}

function ClearBtn({ onClick }: { onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} style={{ color: 'var(--text-s)' }}><X size={14} /></button>
  )
}

function ThreeWayToggle({ value, labels, onChange }: {
  value: boolean | null | undefined
  labels: [string, string, string]
  onChange: (v: boolean | null) => void
}) {
  const opts: [string, boolean | null][] = [[labels[0], null], [labels[1], true], [labels[2], false]]
  return (
    <div className="flex rounded-lg overflow-hidden text-xs" style={{ border: '1px solid var(--border)' }}>
      {opts.map(([label, val]) => {
        const active = value === val
        return (
          <button key={String(val)} type="button"
            className="flex-1 px-2 py-1.5 text-center transition-colors"
            style={{ background: active ? 'var(--brand)' : 'var(--surface2)', color: active ? '#fff' : 'var(--text-m)' }}
            onClick={() => onChange(val)}>
            {label}
          </button>
        )
      })}
    </div>
  )
}
