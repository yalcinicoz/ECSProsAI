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
  discountMinPercent?: number | null
  attributeFilters?: AttributeFilterItem[]
}

interface AttributeFilterItem {
  attributeTypeId: string
  valueIds: string[]
}

interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
}

interface AttributeValue {
  id: string
  nameI18n: Record<string, string>
  filterColors: { code: string; nameI18n: Record<string, string>; hexCode?: string }[]
}

interface AttributeType {
  id: string
  code: string
  nameI18n: Record<string, string>
  values: AttributeValue[]
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function tr(i18n: Record<string, string> | undefined, fallback = ''): string {
  if (!i18n) return fallback
  return i18n['tr'] ?? i18n['en'] ?? i18n[Object.keys(i18n)[0]] ?? fallback
}

/** Filtre tanımından insan dili açıklaması üretir. */
export function buildDescription(
  def: FilterDef,
  groups: ProductGroup[],
  attrTypes: AttributeType[],
): string {
  const parts: string[] = []

  if (def.productGroupIds?.length) {
    const names = def.productGroupIds
      .map(id => tr(groups.find(g => g.id === id)?.nameI18n))
      .filter(Boolean)
    if (names.length) parts.push(names.join(', ') + ' grub' + (names.length > 1 ? 'undan' : 'undan'))
  }

  if (def.priceMin != null && def.priceMax != null)
    parts.push(`Fiyat: ${def.priceMin}₺ – ${def.priceMax}₺`)
  else if (def.priceMin != null)
    parts.push(`Min fiyat: ${def.priceMin}₺`)
  else if (def.priceMax != null)
    parts.push(`Maks fiyat: ${def.priceMax}₺`)

  if (def.discountMinPercent != null)
    parts.push(`Min %${def.discountMinPercent} indirim`)

  if (def.attributeFilters?.length) {
    for (const af of def.attributeFilters) {
      const atype = attrTypes.find(a => a.id === af.attributeTypeId)
      if (!atype) continue
      const valNames = af.valueIds
        .map(vid => tr(atype.values.find(v => v.id === vid)?.nameI18n))
        .filter(Boolean)
      if (valNames.length)
        parts.push(`${tr(atype.nameI18n)}: ${valNames.join(', ')}`)
    }
  }

  if (!parts.length) return 'Tüm ürünler'
  return parts.join(' · ')
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface FilterBuilderProps {
  value: FilterDef
  onChange: (def: FilterDef, description: string) => void
}

// ── Component ──────────────────────────────────────────────────────────────────

export function FilterBuilder({ value, onChange }: FilterBuilderProps) {
  // ── Remote data ──────────────────────────────────────────────────────────

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

  // ── Local state (mirrors FilterDef) ──────────────────────────────────────

  const [selectedGroups, setSelectedGroups] = useState<string[]>(value.productGroupIds ?? [])
  const [priceMin, setPriceMin] = useState<string>(value.priceMin != null ? String(value.priceMin) : '')
  const [priceMax, setPriceMax] = useState<string>(value.priceMax != null ? String(value.priceMax) : '')
  const [discount, setDiscount] = useState<string>(value.discountMinPercent != null ? String(value.discountMinPercent) : '')
  const [attrFilters, setAttrFilters] = useState<AttributeFilterItem[]>(value.attributeFilters ?? [])
  const [groupOpen, setGroupOpen] = useState(false)

  // ── Emit changes ──────────────────────────────────────────────────────────

  const emit = useCallback((
    groups: string[],
    min: string,
    max: string,
    disc: string,
    filters: AttributeFilterItem[],
  ) => {
    const def: FilterDef = {
      productGroupIds: groups.length ? groups : undefined,
      priceMin: min !== '' && !isNaN(+min) ? +min : null,
      priceMax: max !== '' && !isNaN(+max) ? +max : null,
      discountMinPercent: disc !== '' && !isNaN(+disc) ? +disc : null,
      attributeFilters: filters.filter(f => f.valueIds.length > 0),
    }
    const desc = buildDescription(def, productGroups, attrTypes)
    onChange(def, desc)
  }, [productGroups, attrTypes, onChange])

  // Re-emit when remote data arrives (to refresh description)
  useEffect(() => {
    if (productGroups.length || attrTypes.length)
      emit(selectedGroups, priceMin, priceMax, discount, attrFilters)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [productGroups.length, attrTypes.length])

  // ── Product group helpers ──────────────────────────────────────────────────

  function toggleGroup(id: string) {
    const next = selectedGroups.includes(id)
      ? selectedGroups.filter(g => g !== id)
      : [...selectedGroups, id]
    setSelectedGroups(next)
    emit(next, priceMin, priceMax, discount, attrFilters)
  }

  // ── Attribute filter helpers ──────────────────────────────────────────────

  function addAttrFilter() {
    const usedTypeIds = attrFilters.map(f => f.attributeTypeId)
    const firstUnused = attrTypes.find(a => !usedTypeIds.includes(a.id))
    if (!firstUnused) return
    const next = [...attrFilters, { attributeTypeId: firstUnused.id, valueIds: [] }]
    setAttrFilters(next)
    emit(selectedGroups, priceMin, priceMax, discount, next)
  }

  function removeAttrFilter(idx: number) {
    const next = attrFilters.filter((_, i) => i !== idx)
    setAttrFilters(next)
    emit(selectedGroups, priceMin, priceMax, discount, next)
  }

  function changeAttrType(idx: number, typeId: string) {
    const next = attrFilters.map((f, i) =>
      i === idx ? { attributeTypeId: typeId, valueIds: [] } : f)
    setAttrFilters(next)
    emit(selectedGroups, priceMin, priceMax, discount, next)
  }

  function toggleAttrValue(idx: number, valueId: string) {
    const next = attrFilters.map((f, i) => {
      if (i !== idx) return f
      const valueIds = f.valueIds.includes(valueId)
        ? f.valueIds.filter(v => v !== valueId)
        : [...f.valueIds, valueId]
      return { ...f, valueIds }
    })
    setAttrFilters(next)
    emit(selectedGroups, priceMin, priceMax, discount, next)
  }

  // ── Computed ──────────────────────────────────────────────────────────────

  const description = useMemo(
    () => buildDescription({ productGroupIds: selectedGroups,
      priceMin: priceMin !== '' ? +priceMin : null,
      priceMax: priceMax !== '' ? +priceMax : null,
      discountMinPercent: discount !== '' ? +discount : null,
      attributeFilters: attrFilters,
    }, productGroups, attrTypes),
    [selectedGroups, priceMin, priceMax, discount, attrFilters, productGroups, attrTypes],
  )

  const usedTypeIds = attrFilters.map(f => f.attributeTypeId)
  const hasFilters = selectedGroups.length > 0 || priceMin || priceMax || discount || attrFilters.some(f => f.valueIds.length)

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5">

      {/* ── Ürün Grupları ─────────────────────────────────────────────────── */}
      <Section title="Ürün Grupları" hint="Hangi ürün gruplarından?">
        {/* Seçili gruplar */}
        {selectedGroups.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mb-2">
            {selectedGroups.map(id => {
              const g = productGroups.find(pg => pg.id === id)
              return (
                <Chip key={id} onRemove={() => toggleGroup(id)}>
                  {g ? tr(g.nameI18n, g.code) : id.slice(0, 8)}
                </Chip>
              )
            })}
          </div>
        )}

        {/* Dropdown */}
        <div className="relative">
          <button
            type="button"
            className="flex items-center gap-2 text-sm px-3 py-1.5 rounded-lg border transition-colors"
            style={{ borderColor: 'var(--border)', color: 'var(--text-m)', background: 'var(--surface2)' }}
            onClick={() => setGroupOpen(o => !o)}
          >
            <Plus size={12} />
            {selectedGroups.length === 0 ? 'Ürün grubu seç' : 'Başka grup ekle'}
            <ChevronDown size={12} className={cn('transition-transform', groupOpen && 'rotate-180')} />
          </button>

          {groupOpen && (
            <div
              className="absolute z-20 mt-1 w-64 rounded-xl shadow-lg py-1 overflow-y-auto max-h-52"
              style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
            >
              {productGroups.length === 0 && (
                <p className="px-3 py-2 text-xs" style={{ color: 'var(--text-s)' }}>Ürün grubu yok</p>
              )}
              {productGroups.map(g => (
                <button
                  key={g.id}
                  type="button"
                  className={cn(
                    'w-full text-left px-3 py-2 text-sm transition-colors',
                    selectedGroups.includes(g.id)
                      ? 'font-medium'
                      : 'hover:bg-[var(--surface2)]',
                  )}
                  style={{ color: selectedGroups.includes(g.id) ? 'var(--brand)' : 'var(--text)' }}
                  onClick={() => { toggleGroup(g.id); setGroupOpen(false) }}
                >
                  {selectedGroups.includes(g.id) && <span className="mr-1">✓</span>}
                  {tr(g.nameI18n, g.code)}
                </button>
              ))}
            </div>
          )}
        </div>
      </Section>

      {/* ── Fiyat Aralığı ─────────────────────────────────────────────────── */}
      <Section title="Fiyat Aralığı" hint="Boş bırakılırsa sınır yok">
        <div className="flex items-center gap-2">
          <div className="relative flex-1">
            <input
              type="number"
              className="inp pr-6"
              placeholder="Min"
              value={priceMin}
              onChange={e => { setPriceMin(e.target.value); emit(selectedGroups, e.target.value, priceMax, discount, attrFilters) }}
              min={0}
            />
            <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>₺</span>
          </div>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span>
          <div className="relative flex-1">
            <input
              type="number"
              className="inp pr-6"
              placeholder="Maks"
              value={priceMax}
              onChange={e => { setPriceMax(e.target.value); emit(selectedGroups, priceMin, e.target.value, discount, attrFilters) }}
              min={0}
            />
            <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>₺</span>
          </div>
          {(priceMin || priceMax) && (
            <button
              type="button"
              onClick={() => { setPriceMin(''); setPriceMax(''); emit(selectedGroups, '', '', discount, attrFilters) }}
              style={{ color: 'var(--text-s)' }}
            ><X size={14} /></button>
          )}
        </div>
      </Section>

      {/* ── Minimum İndirim ───────────────────────────────────────────────── */}
      <Section title="Minimum İndirim" hint="Yalnızca indirimli ürünler için">
        <div className="flex items-center gap-2">
          <div className="relative w-28">
            <input
              type="number"
              className="inp pr-6"
              placeholder="0"
              value={discount}
              onChange={e => { setDiscount(e.target.value); emit(selectedGroups, priceMin, priceMax, e.target.value, attrFilters) }}
              min={0}
              max={100}
            />
            <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs pointer-events-none" style={{ color: 'var(--text-s)' }}>%</span>
          </div>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>
            {discount ? `%${discount} ve üzeri indirimli` : 'indirim filtresi yok'}
          </span>
          {discount && (
            <button
              type="button"
              onClick={() => { setDiscount(''); emit(selectedGroups, priceMin, priceMax, '', attrFilters) }}
              style={{ color: 'var(--text-s)' }}
            ><X size={14} /></button>
          )}
        </div>
      </Section>

      {/* ── Özellik Filtreleri ────────────────────────────────────────────── */}
      <Section title="Özellik Filtreleri" hint="Renk, beden, cinsiyet vb.">
        {attrFilters.map((af, idx) => {
          const atype = attrTypes.find(a => a.id === af.attributeTypeId)

          return (
            <div key={idx} className="mb-3 p-3 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
              {/* Özellik tipi seçici */}
              <div className="flex items-center gap-2 mb-2">
                <select
                  className="inp text-sm flex-1"
                  value={af.attributeTypeId}
                  onChange={e => changeAttrType(idx, e.target.value)}
                >
                  {attrTypes
                    .filter(a => a.id === af.attributeTypeId || !usedTypeIds.includes(a.id))
                    .map(a => (
                      <option key={a.id} value={a.id}>{tr(a.nameI18n, a.code)}</option>
                    ))}
                </select>
                <button
                  type="button"
                  onClick={() => removeAttrFilter(idx)}
                  className="p-1 rounded-lg hover:bg-red-50 transition-colors flex-shrink-0"
                  style={{ color: '#ef4444' }}
                  title="Kaldır"
                ><X size={14} /></button>
              </div>

              {/* Değer seçici — chip'ler */}
              {atype && (
                <div className="flex flex-wrap gap-1.5">
                  {atype.values.map(val => {
                    const selected = af.valueIds.includes(val.id)
                    const hex = val.filterColors?.[0]?.hexCode
                    return (
                      <button
                        key={val.id}
                        type="button"
                        onClick={() => toggleAttrValue(idx, val.id)}
                        className={cn(
                          'inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border transition-all',
                          selected
                            ? 'border-[var(--brand)]'
                            : 'border-transparent hover:border-[var(--border)]',
                        )}
                        style={{
                          background: selected ? 'var(--brand-bg)' : 'var(--surface)',
                          color: selected ? 'var(--brand)' : 'var(--text-m)',
                        }}
                      >
                        {hex && (
                          <span
                            className="w-3 h-3 rounded-full border border-white/30 flex-shrink-0"
                            style={{ background: hex }}
                          />
                        )}
                        {tr(val.nameI18n)}
                        {selected && <span className="ml-0.5 opacity-60">×</span>}
                      </button>
                    )
                  })}
                  {atype.values.length === 0 && (
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>Bu özellik tipi için değer yok</span>
                  )}
                </div>
              )}
            </div>
          )
        })}

        {/* Özellik ekle */}
        {attrTypes.length > usedTypeIds.length && (
          <button
            type="button"
            onClick={addAttrFilter}
            className="flex items-center gap-1.5 text-sm px-3 py-1.5 rounded-lg border border-dashed transition-colors"
            style={{ borderColor: 'var(--border)', color: 'var(--text-m)' }}
          >
            <Plus size={13} /> Özellik filtresi ekle
          </button>
        )}
      </Section>

      {/* ── Otomatik açıklama ──────────────────────────────────────────────── */}
      <div
        className="flex items-start gap-2 p-3 rounded-xl text-sm"
        style={{ background: hasFilters ? 'var(--brand-bg)' : 'var(--surface2)', border: '1px solid var(--border)' }}
      >
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

function Section({ title, hint, children }: {
  title: string
  hint?: string
  children: React.ReactNode
}) {
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
    <span
      className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium"
      style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand)' }}
    >
      {children}
      <button type="button" onClick={onRemove} className="ml-0.5 opacity-60 hover:opacity-100">
        <X size={10} />
      </button>
    </span>
  )
}
