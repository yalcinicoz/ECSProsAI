import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { ChevronDown, Search, SlidersHorizontal, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { GridStateApi } from './useGridState'
import type { GridBreakpoint, GridFilterValue } from './types'
import { DATE_QUICK, NUMBER_OPS, TEXT_OPS, filterChipText, quickDateRange, type GridFilterField } from './filterUtils'

// FilterBar (plan §2.3, E6/E11): global arama (400 ms debounce, Enter anında) + hızlı filtreler + "Gelişmiş" paneli +
// aktif filtre çipleri (tek tek kaldır) + "Tümünü temizle". Mobilde "Filtreler (n)" düğmesi + bottom sheet; çip satırı kapalıyken de görünür.

interface Props {
  grid: GridStateApi
  fields: GridFilterField[]
  search?: { placeholder?: string } | false
  bp: GridBreakpoint
  /** filtre çubuğunun solunda sabit içerik (örn. sekmeler değil; küçük düğmeler) */
  leading?: ReactNode
}

export function FilterBar({ grid, fields, search, bp, leading }: Props) {
  const { state } = grid
  const quick = fields.filter(f => f.quick)
  const advanced = fields.filter(f => !f.quick)
  const [advOpen, setAdvOpen] = useState(false)
  const [sheetOpen, setSheetOpen] = useState(false)
  const activeAdvanced = advanced.filter(f => state.filters.some(x => x.field === f.key)).length
  const activeCount = state.filters.length

  const chips = (
    <ActiveChips grid={grid} fields={fields} />
  )

  if (bp === 'mobile') {
    return (
      <div className="space-y-2">
        <div className="flex items-center gap-2">
          {leading}
          {search !== false && <SearchBox grid={grid} placeholder={search?.placeholder} className="flex-1 min-w-0" />}
          <button type="button" onClick={() => setSheetOpen(true)} aria-haspopup="dialog"
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm whitespace-nowrap"
            style={{ border: '1px solid var(--border)', color: 'var(--text)', background: activeCount ? 'var(--surface2)' : undefined }}>
            <SlidersHorizontal size={15} /> Filtreler{activeCount > 0 && <span className="text-xs px-1.5 rounded-full" style={{ background: 'var(--brand)', color: '#fff' }}>{activeCount}</span>}
          </button>
        </div>
        {chips}
        {sheetOpen && (
          <BottomSheet title={`Filtreler${activeCount ? ` (${activeCount})` : ''}`} onClose={() => setSheetOpen(false)}>
            <div className="space-y-3">
              {fields.map(f => <FieldRow key={f.key} field={f} grid={grid} stacked />)}
              <div className="flex gap-2 pt-2">
                <button type="button" onClick={() => grid.clearFilters({ keepSearch: true })} disabled={!activeCount}
                  className="px-3 py-2 rounded-lg text-sm disabled:opacity-40" style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Tümünü temizle</button>
                <button type="button" onClick={() => setSheetOpen(false)} className="flex-1 px-3 py-2 rounded-lg text-sm font-medium" style={{ background: 'var(--brand)', color: '#fff' }}>Uygula</button>
              </div>
            </div>
          </BottomSheet>
        )}
      </div>
    )
  }

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        {leading}
        {search !== false && <SearchBox grid={grid} placeholder={search?.placeholder} />}
        {quick.map(f => <FieldRow key={f.key} field={f} grid={grid} />)}
        {advanced.length > 0 && (
          <button type="button" onClick={() => setAdvOpen(o => !o)} aria-expanded={advOpen}
            className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm"
            style={{ border: '1px solid var(--border)', color: 'var(--text)', background: advOpen ? 'var(--surface2)' : undefined }}>
            <SlidersHorizontal size={14} /> Gelişmiş{activeAdvanced > 0 && <span className="text-xs px-1.5 rounded-full" style={{ background: 'var(--brand)', color: '#fff' }}>{activeAdvanced}</span>}
            <ChevronDown size={14} className={cn('transition-transform', advOpen && 'rotate-180')} />
          </button>
        )}
      </div>
      {advOpen && advanced.length > 0 && (
        <div className="card2 p-3 grid gap-3 grid-cols-1 sm:grid-cols-2 lg:grid-cols-3" role="region" aria-label="Gelişmiş filtreler">
          {advanced.map(f => <FieldRow key={f.key} field={f} grid={grid} stacked />)}
        </div>
      )}
      {chips}
    </div>
  )
}

// ── global arama ──
function SearchBox({ grid, placeholder, className }: { grid: GridStateApi; placeholder?: string; className?: string }) {
  const applied = grid.state.search
  // URL'den (geri tuşu / çip silme) değişince kutu eşitlenir — render sırasında durum ayarı (React kalıbı), ref/effect yok
  const [box, setBox] = useState({ applied, val: applied })
  if (box.applied !== applied) setBox({ applied, val: applied })
  const val = box.applied === applied ? box.val : applied
  const setVal = (v: string) => setBox({ applied, val: v })
  const timer = useRef<number | null>(null)
  useEffect(() => () => { if (timer.current) window.clearTimeout(timer.current) }, [])
  const commit = (s: string, replace: boolean) => { if (s.trim() !== applied) grid.setSearch(s, { replace }) }
  return (
    <div className={cn('relative', className)} style={{ minWidth: className ? undefined : 260 }}>
      <Search size={14} className="absolute left-2.5 top-1/2 -translate-y-1/2" style={{ color: 'var(--text-s)' }} />
      <input className="inp text-sm !py-1.5 !pl-8 !pr-7 !h-auto w-full" placeholder={placeholder ?? 'Ara…'} value={val} aria-label="Ara"
        onChange={e => {
          const s = e.target.value; setVal(s)
          if (timer.current) window.clearTimeout(timer.current)
          timer.current = window.setTimeout(() => commit(s, true), 400)   // debounce; yazarken geçmiş kirlenmez
        }}
        onKeyDown={e => { if (e.key === 'Enter') { if (timer.current) window.clearTimeout(timer.current); commit(val, false) } }} />
      {val && (
        <button type="button" aria-label="Aramayı temizle" onClick={() => { setVal(''); if (timer.current) window.clearTimeout(timer.current); commit('', false) }}
          className="absolute right-2 top-1/2 -translate-y-1/2 p-0.5 rounded hover:bg-[var(--surface2)]" style={{ color: 'var(--text-s)' }}><X size={13} /></button>
      )}
    </div>
  )
}

// ── aktif çipler ──
function ActiveChips({ grid, fields }: { grid: GridStateApi; fields: GridFilterField[] }) {
  const { state } = grid
  if (!state.search && state.filters.length === 0) return null
  return (
    <div className="flex flex-wrap items-center gap-1.5" aria-label="Aktif filtreler">
      {state.search && <Chip text={`Arama: "${state.search}"`} onRemove={() => grid.setSearch('')} />}
      {state.filters.map(f => {
        const field = fields.find(x => x.key === f.field)
        const text = field ? filterChipText(field, f) : `${f.field}: ${f.value}`
        return <Chip key={f.field} text={text} onRemove={() => grid.removeFilter(f.field)} />
      })}
      <button type="button" onClick={() => grid.clearFilters()} className="text-xs underline px-1" style={{ color: 'var(--text-s)' }}>Tümünü temizle</button>
    </div>
  )
}

function Chip({ text, onRemove }: { text: string; onRemove: () => void }) {
  return (
    <span className="badge" style={{ background: 'var(--surface2)', color: 'var(--text)', border: '1px solid var(--border)' }}>
      {text}
      <button type="button" aria-label={`Filtreyi kaldır: ${text}`} onClick={onRemove} className="ml-0.5 rounded-full hover:bg-[var(--surface)]" style={{ color: 'var(--text-s)' }}><X size={11} /></button>
    </span>
  )
}

// ── alan girişleri ──
function FieldRow({ field, grid, stacked }: { field: GridFilterField; grid: GridStateApi; stacked?: boolean }) {
  const cur = grid.state.filters.find(f => f.field === field.key)
  const set = (v: Partial<GridFilterValue> & { value: string }) => grid.setFilter({ field: field.key, op: v.op ?? 'auto', value: v.value, quick: v.quick })
  const wrap = (node: ReactNode) => stacked
    ? <label className="block"><span className="flbl">{field.label}</span>{node}</label>
    : <div className="flex items-center gap-1" title={field.label}>{node}</div>

  switch (field.type) {
    case 'boolean':
      return wrap(
        <select className="inp text-sm py-1.5 px-2 h-auto w-auto" value={cur?.value ?? ''} aria-label={field.label}
          onChange={e => set({ op: 'eq', value: e.target.value })}>
          <option value="">{stacked ? 'Tümü' : `${field.label}: Tümü`}</option>
          <option value="true">{stacked ? 'Evet' : `${field.label}: Evet`}</option>
          <option value="false">{stacked ? 'Hayır' : `${field.label}: Hayır`}</option>
        </select>)
    case 'enum':
      if (field.multiple) return wrap(<MultiSelect field={field} value={cur?.value ?? ''} onChange={v => set({ op: 'in', value: v })} stacked={stacked} />)
      return wrap(
        <select className="inp text-sm py-1.5 px-2 h-auto w-auto" value={cur?.value ?? ''} aria-label={field.label}
          onChange={e => set({ op: 'eq', value: e.target.value })}>
          <option value="">{stacked ? 'Tümü' : `${field.label}: Tümü`}</option>
          {field.options?.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>)
    case 'date':
      return wrap(<DateField field={field} cur={cur} onChange={set} stacked={stacked} />)
    case 'number':
      return wrap(<NumberField field={field} cur={cur} onChange={set} />)
    default:
      return wrap(<TextField field={field} cur={cur} onChange={set} />)
  }
}

function MultiSelect({ field, value, onChange, stacked }: { field: GridFilterField; value: string; onChange: (v: string) => void; stacked?: boolean }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const selected = useMemo(() => new Set(value ? value.split(',') : []), [value])
  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    document.addEventListener('mousedown', onDoc); return () => document.removeEventListener('mousedown', onDoc)
  }, [open])
  const toggle = (v: string) => { const s = new Set(selected); if (s.has(v)) s.delete(v); else s.add(v); onChange(Array.from(s).join(',')) }
  const label = selected.size === 0 ? (stacked ? 'Tümü' : `${field.label}: Tümü`)
    : selected.size === 1 ? (field.options?.find(o => selected.has(o.value))?.label ?? value)
      : `${selected.size} seçili`
  return (
    <div className="relative" ref={ref}>
      <button type="button" onClick={() => setOpen(o => !o)} aria-haspopup="listbox" aria-expanded={open} aria-label={field.label}
        className={cn('inp text-sm !py-1.5 !px-2 !h-auto !inline-flex items-center gap-1 whitespace-nowrap', stacked ? 'w-full justify-between' : '!w-auto')}
        style={{ color: selected.size ? 'var(--text)' : undefined }}>
        <span className="truncate">{label}</span><ChevronDown size={13} />
      </button>
      {open && (
        <div role="listbox" aria-multiselectable className="absolute left-0 mt-1 min-w-[200px] max-h-64 overflow-y-auto thin-scroll rounded-xl shadow-lg z-40 p-1"
          style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          {field.options?.map(o => (
            <label key={o.value} className="flex items-center gap-2 px-2 py-1.5 rounded-lg text-sm cursor-pointer hover:bg-[var(--surface2)]" style={{ color: 'var(--text)' }}>
              <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={selected.has(o.value)} onChange={() => toggle(o.value)} />
              {o.label}
            </label>
          ))}
          {selected.size > 0 && (
            <button type="button" onClick={() => onChange('')} className="w-full text-left px-2 py-1.5 text-xs" style={{ color: 'var(--text-s)', borderTop: '1px solid var(--border)' }}>Seçimi temizle</button>
          )}
        </div>
      )}
    </div>
  )
}

function DateField({ field, cur, onChange, stacked }: { field: GridFilterField; cur?: GridFilterValue; onChange: (v: Partial<GridFilterValue> & { value: string }) => void; stacked?: boolean }) {
  const quick = cur ? (cur.quick ?? 'custom') : ''
  const curVal = cur?.value ?? ''
  const [a, b] = curVal.split(',')
  const [box, setBox] = useState<{ v: string; a: string; b: string }>({ v: curVal, a: a ?? '', b: b ?? '' })
  if (box.v !== curVal) setBox({ v: curVal, a: a ?? '', b: b ?? '' })
  const custom = box.v === curVal ? box : { v: curVal, a: a ?? '', b: b ?? '' }
  const setCustom = (x: { a: string; b: string }) => setBox({ v: curVal, ...x })
  const pick = (token: string) => {
    if (!token) return onChange({ value: '' })
    if (token === 'custom') { if (custom.a && custom.b) onChange({ op: 'between', value: `${custom.a},${custom.b}`, quick: 'custom' }); return }
    const range = quickDateRange(token); if (range) onChange({ op: 'between', value: range, quick: token })
  }
  const applyCustom = (na: string, nb: string) => { setCustom({ a: na, b: nb }); if (na && nb) onChange({ op: 'between', value: `${na},${nb}`, quick: 'custom' }) }
  const showCustom = quick === 'custom' || (cur && !cur.quick)
  return (
    <div className={cn('flex items-center gap-1 flex-wrap', stacked && 'w-full')}>
      <select className="inp text-sm py-1.5 px-2 h-auto w-auto" value={quick} aria-label={field.label} onChange={e => pick(e.target.value)}>
        <option value="">{stacked ? 'Tümü' : `${field.label}: Tümü`}</option>
        {DATE_QUICK.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
      {(showCustom || (!cur && false)) && (
        <>
          <input type="date" className="inp text-sm py-1.5 px-2 h-auto w-auto" value={custom.a} aria-label={`${field.label} başlangıç`} onChange={e => applyCustom(e.target.value, custom.b)} />
          <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
          <input type="date" className="inp text-sm py-1.5 px-2 h-auto w-auto" value={custom.b} aria-label={`${field.label} bitiş`} onChange={e => applyCustom(custom.a, e.target.value)} />
        </>
      )}
    </div>
  )
}

function NumberField({ field, cur, onChange }: { field: GridFilterField; cur?: GridFilterValue; onChange: (v: Partial<GridFilterValue> & { value: string }) => void }) {
  const curVal = cur?.value ?? ''
  const curOp = cur?.op && cur.op !== 'auto' ? cur.op : 'eq'
  const [v1, v2] = curVal.split(';')
  const [box, setBox] = useState({ v: curVal, op: curOp, a: v1 ?? '', b: v2 ?? '' })
  if (box.v !== curVal) setBox({ v: curVal, op: curOp, a: v1 ?? '', b: v2 ?? '' })
  const st = box.v === curVal ? box : { v: curVal, op: curOp, a: v1 ?? '', b: v2 ?? '' }
  const op = st.op, a = st.a, b = st.b
  const setOp = (o: string) => setBox({ ...st, op: o })
  const setA = (x: string) => setBox({ ...st, a: x })
  const setB = (x: string) => setBox({ ...st, b: x })
  const apply = (o: string, x: string, y: string) => {
    if (o === 'between') { if (x && y) onChange({ op: o, value: `${x};${y}` }); else if (!x && !y) onChange({ value: '' }) }
    else onChange({ op: o, value: x })
  }
  return (
    <div className="flex items-center gap-1">
      <select className="inp text-sm py-1.5 px-1.5 h-auto w-auto" value={op} aria-label={`${field.label} operatör`} onChange={e => { setOp(e.target.value); apply(e.target.value, a, b) }}>
        {NUMBER_OPS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
      <input type="number" inputMode="decimal" className="inp text-sm py-1.5 px-2 h-auto w-24" value={a} placeholder={op === 'between' ? 'min' : field.label} aria-label={field.label}
        onChange={e => setA(e.target.value)} onBlur={() => apply(op, a, b)} onKeyDown={e => { if (e.key === 'Enter') apply(op, a, b) }} />
      {op === 'between' && (
        <input type="number" inputMode="decimal" className="inp text-sm py-1.5 px-2 h-auto w-24" value={b} placeholder="max" aria-label={`${field.label} max`}
          onChange={e => setB(e.target.value)} onBlur={() => apply(op, a, b)} onKeyDown={e => { if (e.key === 'Enter') apply(op, a, b) }} />
      )}
    </div>
  )
}

function TextField({ field, cur, onChange }: { field: GridFilterField; cur?: GridFilterValue; onChange: (v: Partial<GridFilterValue> & { value: string }) => void }) {
  const ops = field.ops ? TEXT_OPS.filter(o => field.ops!.includes(o.value)) : TEXT_OPS
  const curVal = cur?.value ?? ''
  const curOp = cur?.op && cur.op !== 'auto' ? cur.op : ops[0]?.value ?? 'contains'
  const [box, setBox] = useState({ v: curVal, op: curOp, text: curVal })
  if (box.v !== curVal) setBox({ v: curVal, op: curOp, text: curVal })
  const st = box.v === curVal ? box : { v: curVal, op: curOp, text: curVal }
  const op = st.op, v = st.text
  const setOp = (o: string) => setBox({ ...st, op: o })
  const setV = (x: string) => setBox({ ...st, text: x })
  const timer = useRef<number | null>(null)
  const apply = (o: string, s: string) => { if (timer.current) window.clearTimeout(timer.current); if (s.trim() !== (cur?.value ?? '') || o !== (cur?.op ?? '')) onChange({ op: o, value: s.trim() }) }
  return (
    <div className="flex items-center gap-1">
      {ops.length > 1 && (
        <select className="inp text-sm py-1.5 px-1.5 h-auto w-auto" value={op} aria-label={`${field.label} operatör`} onChange={e => { setOp(e.target.value); if (v) apply(e.target.value, v) }}>
          {ops.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      )}
      <input className="inp text-sm py-1.5 px-2 h-auto w-40" value={v} placeholder={field.label} aria-label={field.label}
        onChange={e => { const s = e.target.value; setV(s); if (timer.current) window.clearTimeout(timer.current); timer.current = window.setTimeout(() => apply(op, s), 400) }}
        onKeyDown={e => { if (e.key === 'Enter') apply(op, v) }} />
    </div>
  )
}

// ── mobil bottom sheet ──
function BottomSheet({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  useEffect(() => {
    const prev = document.body.style.overflow; document.body.style.overflow = 'hidden'
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    return () => { document.body.style.overflow = prev; document.removeEventListener('keydown', onKey) }
  }, [onClose])
  return (
    <div className="fixed inset-0 z-[70]" role="dialog" aria-modal="true" aria-label={title}>
      <div className="absolute inset-0" style={{ background: 'rgba(15,23,42,.5)' }} onClick={onClose} />
      <div className="absolute left-0 right-0 bottom-0 max-h-[85vh] overflow-y-auto rounded-t-2xl p-4" style={{ background: 'var(--surface)' }} data-bottom-bar>
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{title}</h3>
          <button type="button" aria-label="Kapat" onClick={onClose} className="p-1 rounded-lg hover:bg-[var(--surface2)]" style={{ color: 'var(--text-s)' }}><X size={16} /></button>
        </div>
        {children}
      </div>
    </div>
  )
}
