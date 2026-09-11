import { useState, type ReactNode } from 'react'
import { ChevronDown, SlidersHorizontal } from 'lucide-react'
import { cn } from '@/lib/utils'
import { FieldRow } from './FilterBar'
import type { GridFilterField } from './filterUtils'
import type { GridStateApi } from './useGridState'

/**
 * Tam genişlik filtre akordeonu (2026-09-11, kullanıcı: eski panel /urun/urun-yonetim "Filtrele" kartıyla AYNI):
 * grid'in DIŞINDA, sayfa genişliğinde bir kart; başlangıçta kapalı, başlığa tıklayınca aşağı doğru genişler.
 * Grid araç çubuğunun (arama/kolonlar/export) satırına GİRMEZ — o yüzden DataGrid içinden değil sayfadan yerleştirilir.
 * Alanlar aynı grid durumuna yazar; çip ve mobil listesi için sayfa aynı alanları DataGrid.advancedFilters (layout 'external') ile de verir.
 */
export function FilterAccordion({ grid, fields, storageKey, title = 'Filtrele', note }: {
  grid: GridStateApi; fields: GridFilterField[]; storageKey: string; title?: string; note?: ReactNode
}) {
  const [open, setOpen] = useState<boolean>(() => { try { return localStorage.getItem(storageKey) === '1' } catch { return false } })
  const toggle = () => setOpen(o => { const n = !o; try { localStorage.setItem(storageKey, n ? '1' : '0') } catch { /* yok say */ } return n })
  const active = fields.filter(f => grid.state.filters.some(x => x.field === f.key)).length
  return (
    <div className="w-full rounded-xl overflow-hidden mb-4" style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderTop: '3px solid var(--brand)' }}>
      <button type="button" onClick={toggle} aria-expanded={open} aria-controls="filter-accordion-body"
        className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-[var(--surface2)]">
        <span className="inline-flex items-center gap-2 text-sm font-semibold" style={{ color: 'var(--text)' }}>
          <SlidersHorizontal size={15} /> {title}
          {active > 0 && <span className="text-xs px-1.5 rounded-full font-normal" style={{ background: 'var(--brand)', color: '#fff' }}>{active}</span>}
        </span>
        <ChevronDown size={16} className={cn('transition-transform', open && 'rotate-180')} style={{ color: 'var(--text-s)' }} />
      </button>
      {open && (
        <div id="filter-accordion-body" className="px-4 pb-4 pt-3" style={{ borderTop: '1px solid var(--border)' }}>
          <div className="grid gap-3" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(230px, 1fr))' }}>
            {fields.map(f => <FieldRow key={f.key} field={f} grid={grid} stacked />)}
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2 mt-3 pt-3" style={{ borderTop: '1px solid var(--border)' }}>
            <div className="text-xs" style={{ color: 'var(--text-s)' }}>{note}</div>
            <button type="button" onClick={() => grid.clearFilters({ keepSearch: true })} disabled={grid.state.filters.length === 0}
              className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40" style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Tümünü temizle</button>
          </div>
        </div>
      )}
    </div>
  )
}
